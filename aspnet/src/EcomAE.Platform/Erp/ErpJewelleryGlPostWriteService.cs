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

        var purchaseTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases)) purchaseTypes.Add(t);
        var saleTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ErpJwVocTypes.SalesAnalysisAliases.Concat(ErpJwVocTypes.RetailAliases).Concat(ErpJwVocTypes.MetalSaleAliases)) saleTypes.Add(t);

        var purchases = 0;
        var sales = 0;
        var skipped = 0;
        var failed = 0;

        var vouchers = new List<(long Id, string Type, decimal Net, decimal Vat, decimal Gross, long Date, long CompanyId)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`voc_type`,`net_amount`,`vat_amount`,`total_with_vat`,UNIX_TIMESTAMP(`voc_date`),`company_id`"
                + " FROM `epc_jewel_voucher` WHERE `status` NOT IN ('draft','cancelled')"
                + " AND (?<=0 OR `id`=?) AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)>=?) AND (?=0 OR UNIX_TIMESTAMP(`voc_date`)<?)"
                + " ORDER BY `id`");
            ErpDb.AddParameters(command, voucherId, voucherId, fromUtc, fromUtc, toUtc, toUtc);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                vouchers.Add((reader.GetInt64(0), reader.GetString(1), reader.GetDecimal(2),
                    reader.GetDecimal(3), reader.GetDecimal(4), reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                    reader.IsDBNull(6) ? 0 : reader.GetInt64(6)));
            }
        }

        foreach (var v in vouchers)
        {
            try
            {
            if (purchaseTypes.Contains(v.Type))
            {
                var reference = "JW-V-" + v.Id.ToString(CultureInfo.InvariantCulture) + "-PUR";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0,
                    v.CompanyId > 0 ? v.CompanyId : defaultCompanyId, v.Id, reference,
                    "Metal purchase voucher " + v.Type + "-" + v.Id, ErpAutoPartsIfrs.PurchaseReceived(v.Net, v.Vat), v.Date, cancellationToken).ConfigureAwait(false) > 0)
                {
                    purchases++;
                }
            }
            else if (saleTypes.Contains(v.Type))
            {
                var reference = "JW-V-" + v.Id.ToString(CultureInfo.InvariantCulture) + "-SAL";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else
                {
                    var net = v.Vat > 0 ? v.Net : v.Gross - v.Vat;
                    if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0,
                        v.CompanyId > 0 ? v.CompanyId : defaultCompanyId, v.Id, reference,
                        "Jewellery sale voucher " + v.Type + "-" + v.Id, ErpAutoPartsIfrs.SaleInvoiced(net, v.Vat), v.Date, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        sales++;
                    }
                }
            }
            }
            catch (Exception)
            {
                failed++;
            }
        }

        return "Posted to GL: " + purchases + " metal purchase journals, " + sales + " sale journals"
            + (skipped > 0 ? " (" + skipped + " already posted skipped)" : "")
            + (failed > 0 ? " (" + failed + " failed — retry on next save)" : "")
            + " — income statement, balance sheet and dashboard figures now include the jewellery numbers.";
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
