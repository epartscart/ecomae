using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Bridge from the jewellery workspace into the general ledger: posts metal
/// purchase vouchers (Dr inventory / Cr payable), retail sale vouchers
/// (Dr receivable / Cr revenue / Cr 5% output VAT) and paid receipts into the
/// shared GL so the income statement, balance sheet and dashboard insight
/// figures include the jewellery numbers. Idempotent — vouchers already
/// journaled under their JW-* reference are skipped.
/// </summary>
public interface IErpJewelleryGlPostWriteService
{
    Task<string> PostAsync(long voucherId, string? period, CancellationToken cancellationToken = default);
}

public sealed class ErpJewelleryGlPostWriteService : IErpJewelleryGlPostWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpJewelleryGlPostWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<string> PostAsync(long voucherId, string? period, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — GL posting skipped.";
        }

        if (!string.IsNullOrWhiteSpace(period)
            && !DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "Invalid period — use YYYY-MM or leave it empty.";
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var defaultCompanyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        await ErpJewelleryGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);

        var purchaseTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases)) purchaseTypes.Add(t);
        var saleTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ErpJwVocTypes.SalesAnalysisAliases.Concat(ErpJwVocTypes.RetailAliases).Concat(ErpJwVocTypes.MetalSaleAliases)) saleTypes.Add(t);
        var returnTypes = new HashSet<string>(ErpJwVocTypes.SalesReturnAliases, StringComparer.Ordinal);
        var advanceTypes = new HashSet<string>(ErpJwVocTypes.AdvanceAliases, StringComparer.Ordinal);
        var journalTypes = new HashSet<string>(ErpJwVocTypes.JournalAliases, StringComparer.Ordinal);

        var purchases = 0;
        var sales = 0;
        var extras = 0;
        var skipped = 0;
        var failed = 0;

        var vouchers = new List<(long Id, string Type, decimal Net, decimal Vat, decimal Gross, long Date, long CompanyId, decimal OldGold)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`voc_type`,`net_amount`,`vat_amount`,`total_with_vat`,UNIX_TIMESTAMP(`voc_date`),`company_id`,`old_gold_exchange`"
                + " FROM `epc_jewel_voucher` WHERE `status` NOT IN ('draft','cancelled') AND IFNULL(`gl_mode`,'post')<>'history'"
                + " AND (?<=0 OR `id`=?) AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)>=?) AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)<?)"
                + " ORDER BY `id`");
            ErpDb.AddParameters(command, voucherId, voucherId, fromUtc, fromUtc, toUtc, toUtc);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                vouchers.Add((reader.GetInt64(0), reader.GetString(1), reader.GetDecimal(2),
                    reader.GetDecimal(3), reader.GetDecimal(4), reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                    reader.IsDBNull(6) ? 0 : reader.GetInt64(6), reader.GetDecimal(7)));
            }
        }

        async Task<int> PostOnceAsync(string reference, string description, long sourceId, long companyId, IReadOnlyList<ErpAutoPartsIfrs.JournalLine> recipe, long date)
        {
            if (recipe.Count == 0)
            {
                return 0;
            }

            if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, cancellationToken).ConfigureAwait(false) > 0)
            {
                skipped++;
                return 0;
            }

            if (recipe.Sum(l => l.Debit) != recipe.Sum(l => l.Credit))
            {
                failed++;
                return 0;
            }

            var id = await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId > 0 ? companyId : defaultCompanyId, sourceId, reference,
                description, recipe, date, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                failed++;
            }

            return id > 0 ? 1 : 0;
        }

        foreach (var v in vouchers)
        {
            try
            {
                var key = "JW-V-" + v.Id.ToString(CultureInfo.InvariantCulture);
                var label = v.Type + "-" + v.Id.ToString(CultureInfo.InvariantCulture);
                if (purchaseTypes.Contains(v.Type))
                {
                    var lineCost = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT " + ErpJewelleryGl.PurchaseLineCostSql + " FROM `epc_jewel_voucher_lines` WHERE `voucher_id`=?"), cancellationToken, v.Id).ConfigureAwait(false);
                    purchases += await PostOnceAsync(key + "-PUR", "Metal purchase voucher " + label, v.Id, v.CompanyId, ErpAutoPartsIfrs.PurchaseReceived(ErpJewelleryGl.PurchaseInventoryCost(lineCost, v.Net), v.Vat), v.Date).ConfigureAwait(false);
                    var rcm = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`rcm_vat`,0) FROM `epc_jewel_voucher` WHERE `id`=?"), cancellationToken, v.Id).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-RCM", "Reverse-charge VAT " + label, v.Id, v.CompanyId, ErpJewelleryGl.ReverseCharge(rcm), v.Date).ConfigureAwait(false);
                    var paid = await TendersAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-PAY", "Supplier payment " + label, v.Id, v.CompanyId, ErpJewelleryGl.Tenders(paid, ErpAutoPartsIfrs.ApAccount, tenderIsDebit: false), v.Date).ConfigureAwait(false);
                }
                else if (saleTypes.Contains(v.Type))
                {
                    var net = v.Vat > 0 ? v.Net : v.Gross - v.Vat;
                    sales += await PostOnceAsync(key + "-SAL", "Jewellery sale voucher " + label, v.Id, v.CompanyId, ErpAutoPartsIfrs.SaleInvoiced(net, v.Vat), v.Date).ConfigureAwait(false);
                    var cost = await CostAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    if (cost > 0) extras += await PostOnceAsync(key + "-COGS", "Cost of sale " + label, v.Id, v.CompanyId, ErpJewelleryGl.CostOfSale(cost), v.Date).ConfigureAwait(false);
                    if (v.OldGold > 0) extras += await PostOnceAsync(key + "-OGX", "Old gold exchange " + label, v.Id, v.CompanyId, ErpJewelleryGl.OldGoldExchange(v.OldGold), v.Date).ConfigureAwait(false);
                    var tenders = await TendersAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-RCV", "POS receipt " + label, v.Id, v.CompanyId, ErpJewelleryGl.Tenders(tenders, ErpAutoPartsIfrs.ArAccount, tenderIsDebit: true), v.Date).ConfigureAwait(false);
                }
                else if (returnTypes.Contains(v.Type))
                {
                    var net = v.Net != 0 ? v.Net : v.Gross - v.Vat;
                    extras += await PostOnceAsync(key + "-SRT", "Sales return " + label, v.Id, v.CompanyId, ErpJewelleryGl.SalesReturn(net, v.Vat), v.Date).ConfigureAwait(false);
                    var cost = await CostAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    if (cost > 0) extras += await PostOnceAsync(key + "-COGS", "Return to stock " + label, v.Id, v.CompanyId, ErpJewelleryGl.CostReturned(cost), v.Date).ConfigureAwait(false);
                    var refunds = await TendersAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-RFD", "Refund " + label, v.Id, v.CompanyId, ErpJewelleryGl.Tenders(refunds, ErpAutoPartsIfrs.ArAccount, tenderIsDebit: false), v.Date).ConfigureAwait(false);
                }
                else if (advanceTypes.Contains(v.Type))
                {
                    var tenders = await TendersAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-ADV", "POS advance " + label, v.Id, v.CompanyId, ErpJewelleryGl.Advance(tenders, v.Gross, v.Vat), v.Date).ConfigureAwait(false);
                }
                else if (journalTypes.Contains(v.Type))
                {
                    var recipe = await JournalLinesAsync(connection, v.Id, cancellationToken).ConfigureAwait(false);
                    extras += await PostOnceAsync(key + "-JV", "Journal voucher " + label, v.Id, v.CompanyId, recipe, v.Date).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
            }
        }

        if (voucherId <= 0)
        {
            foreach (var p in await PettyCashAsync(connection, fromUtc, toUtc, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var recipe = new List<ErpAutoPartsIfrs.JournalLine>();
                    if (p.Debit != 0)
                    {
                        recipe.Add(new(p.Account, p.Debit, 0m, "Petty cash expense"));
                        recipe.Add(new(ErpJewelleryGl.PettyCashAccount, 0m, p.Debit, "Petty cash paid"));
                    }

                    if (p.Credit != 0)
                    {
                        recipe.Add(new(ErpJewelleryGl.PettyCashAccount, p.Credit, 0m, "Petty cash received"));
                        recipe.Add(new(p.Account, 0m, p.Credit, "Petty cash credit"));
                    }

                    extras += await PostOnceAsync("JW-PC-" + p.Id.ToString(CultureInfo.InvariantCulture), "Petty cash voucher " + p.Id.ToString(CultureInfo.InvariantCulture), p.Id, p.CompanyId, recipe, p.Date).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                }
            }
        }

        return "Posted to GL: " + purchases + " metal purchase journals, " + sales + " sale journals"
            + (extras > 0 ? ", " + extras + " cost-of-sale / receipt / return / advance / journal / petty-cash journals" : "")
            + (skipped > 0 ? " (" + skipped + " already posted skipped)" : "")
            + (failed > 0 ? " (" + failed + " failed — check account codes, retry on next save)" : "")
            + " — income statement, balance sheet and dashboard figures now include the jewellery numbers.";
    }

    private static async Task<decimal> CostAsync(DbConnection connection, long voucherId, CancellationToken ct)
        => await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT IFNULL(SUM(`cost_amount`),0) FROM `epc_jewel_voucher_lines` WHERE `voucher_id`=?"), ct, voucherId).ConfigureAwait(false);

    private static async Task<List<(string Account, decimal Amount)>> TendersAsync(DbConnection connection, long voucherId, CancellationToken ct)
    {
        var list = new List<(string, decimal)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `receipt_mode`,`amount_lc` FROM `epc_jewel_voucher_receipts` WHERE `voucher_id`=? ORDER BY `id`");
        ErpDb.AddParameters(command, voucherId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add((ErpJewelleryGl.TenderAccount(reader.GetString(0)), reader.GetDecimal(1)));
        }

        return list;
    }

    private static async Task<List<ErpAutoPartsIfrs.JournalLine>> JournalLinesAsync(DbConnection connection, long voucherId, CancellationToken ct)
    {
        var list = new List<ErpAutoPartsIfrs.JournalLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `debit_ac`,`credit_ac`,`amount`,`narration` FROM `epc_jewel_voucher_addins` WHERE `voucher_id`=? ORDER BY `line_no`,`id`");
        ErpDb.AddParameters(command, voucherId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var amount = reader.GetDecimal(2);
            var note = reader.GetString(3);
            if (reader.GetString(0).Length > 0) list.Add(new(reader.GetString(0), amount, 0m, note));
            if (reader.GetString(1).Length > 0) list.Add(new(reader.GetString(1), 0m, amount, note));
        }

        return list;
    }

    private static async Task<List<(long Id, string Account, decimal Debit, decimal Credit, long Date, long CompanyId)>> PettyCashAsync(DbConnection connection, long fromUtc, long toUtc, CancellationToken ct)
    {
        var list = new List<(long, string, decimal, decimal, long, long)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`account_code`,`debit_amount`,`credit_amount`,UNIX_TIMESTAMP(`voc_date`),`company_id` FROM `epc_jewel_petty_cash`"
            + " WHERE `status` NOT IN ('draft','cancelled') AND `account_code`<>''"
            + " AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)>=?) AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)<?) ORDER BY `id`");
        ErpDb.AddParameters(command, fromUtc, fromUtc, toUtc, toUtc);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add((reader.GetInt64(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.IsDBNull(4) ? 0 : reader.GetInt64(4), reader.GetInt64(5)));
        }

        return list;
    }

    private static (long FromUtc, long ToUtc) PeriodWindow(string? period)
    {
        if (!string.IsNullOrWhiteSpace(period)
            && DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var from = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var to = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds();
            return (from, to);
        }
        return (0, 0);
    }
}
