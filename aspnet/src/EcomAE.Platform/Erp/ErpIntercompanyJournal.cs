using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpIcPartyRef(long Id, string Code, string Trn, long LegalEntityId);

public sealed record ErpIcAccountIds(long ReceivableId, long PayableId, long InputVatId, long OutputVatId);

public sealed record ErpIcInvoiceDraft(
    string Scope,
    string SellerCode,
    string BuyerCode,
    string SellerTrn,
    string BuyerTrn,
    decimal Net,
    decimal Vat,
    decimal Gross,
    decimal Rate,
    string Treatment);

public sealed class ErpIcPreparedJournal
{
    public IReadOnlyList<ErpGlLine> Lines { get; init; } = [];

    public long HeaderBusinessUnitId { get; init; }

    public long HeaderLegalEntityId { get; init; }

    public string Note { get; init; } = string.Empty;

    public IReadOnlyList<ErpIcInvoiceDraft> Invoices { get; init; } = [];
}

/// <summary>
/// When a general journal debits one business unit or legal entity and credits another,
/// add the intercompany receivable and payable so each side still tallies.
/// VAT follows the TRN: the same registration number is not a tax invoice.
/// </summary>
public static class ErpIntercompanyJournal
{
    public static ErpIcPreparedJournal Expand(
        IReadOnlyList<ErpGlLine> lines,
        long headerBusinessUnitId,
        long headerLegalEntityId,
        IReadOnlyDictionary<long, ErpIcPartyRef> businessUnits,
        IReadOnlyDictionary<long, ErpIcPartyRef> legalEntities,
        ErpIcAccountIds accounts,
        decimal vatRate)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var stamped = lines
            .Select(line => Stamp(line, headerBusinessUnitId, headerLegalEntityId, businessUnits))
            .ToList();
        var nets = new Dictionary<(long Bu, long Le), decimal>();
        foreach (var line in stamped)
        {
            var key = (line.BusinessUnitId, line.LegalEntityId);
            nets[key] = nets.GetValueOrDefault(key) + ErpTaxAmountCalculator.Round2(line.Debit) - ErpTaxAmountCalculator.Round2(line.Credit);
        }

        var buyers = nets
            .Where(pair => pair.Value >= 0.005m)
            .Select(pair => (pair.Key, Left: pair.Value))
            .OrderByDescending(pair => pair.Left)
            .ToList();
        var sellers = nets
            .Where(pair => pair.Value <= -0.005m)
            .Select(pair => (pair.Key, Left: -pair.Value))
            .OrderByDescending(pair => pair.Left)
            .ToList();
        if (buyers.Count == 0 || sellers.Count == 0)
        {
            return Finish(stamped, headerBusinessUnitId, headerLegalEntityId, [], []);
        }

        if (accounts.ReceivableId <= 0 || accounts.PayableId <= 0)
        {
            throw new ErpWriteException("Intercompany receivable and payable accounts are not on the chart.");
        }

        var rate = vatRate < 0m ? 0m : vatRate;
        var extra = new List<ErpGlLine>();
        var invoices = new List<ErpIcInvoiceDraft>();
        var buyerAt = 0;
        var sellerAt = 0;
        while (buyerAt < buyers.Count && sellerAt < sellers.Count)
        {
            var buyer = buyers[buyerAt];
            var seller = sellers[sellerAt];
            var net = ErpTaxAmountCalculator.Round2(Math.Min(buyer.Left, seller.Left));
            if (net >= 0.005m)
            {
                var sellerTrn = TrnOf(seller.Key.Bu, seller.Key.Le, businessUnits, legalEntities);
                var buyerTrn = TrnOf(buyer.Key.Bu, buyer.Key.Le, businessUnits, legalEntities);
                var charge = ErpIntercompanyTax.ChargeVatOnInvoice(sellerTrn, buyerTrn);
                var vat = charge ? ErpTaxAmountCalculator.Round2(net * rate / 100m) : 0m;
                if (vat > 0m && (accounts.InputVatId <= 0 || accounts.OutputVatId <= 0))
                {
                    throw new ErpWriteException("VAT input and VAT output accounts are not on the chart.");
                }

                var gross = ErpTaxAmountCalculator.Round2(net + vat);
                var useUnits = buyer.Key.Bu > 0 && seller.Key.Bu > 0 && buyer.Key.Bu != seller.Key.Bu;
                var sellerCode = CodeOf(seller.Key.Bu, seller.Key.Le, useUnits, businessUnits, legalEntities);
                var buyerCode = CodeOf(buyer.Key.Bu, buyer.Key.Le, useUnits, businessUnits, legalEntities);
                extra.Add(new ErpGlLine(
                    accounts.PayableId,
                    0m,
                    gross,
                    "Intercompany payable " + sellerCode,
                    buyer.Key.Bu,
                    buyer.Key.Le));
                extra.Add(new ErpGlLine(
                    accounts.ReceivableId,
                    gross,
                    0m,
                    "Intercompany receivable " + buyerCode,
                    seller.Key.Bu,
                    seller.Key.Le));
                if (vat > 0m)
                {
                    extra.Add(new ErpGlLine(
                        accounts.InputVatId,
                        vat,
                        0m,
                        "Input VAT " + ErpIntercompanyTax.FormatRate(rate) + " different TRN",
                        buyer.Key.Bu,
                        buyer.Key.Le));
                    extra.Add(new ErpGlLine(
                        accounts.OutputVatId,
                        0m,
                        vat,
                        "Output VAT " + ErpIntercompanyTax.FormatRate(rate) + " different TRN",
                        seller.Key.Bu,
                        seller.Key.Le));
                }

                var treatment = charge
                    ? "different_trn"
                    : sellerTrn.Length > 0 && sellerTrn == buyerTrn
                        ? "same_trn"
                        : "unregistered";
                invoices.Add(new ErpIcInvoiceDraft(
                    useUnits ? "business_unit" : "legal_entity",
                    sellerCode,
                    buyerCode,
                    sellerTrn,
                    buyerTrn,
                    net,
                    vat,
                    gross,
                    vat > 0m ? rate : 0m,
                    treatment));
            }

            buyer.Left -= net;
            seller.Left -= net;
            buyers[buyerAt] = buyer;
            sellers[sellerAt] = seller;
            if (buyer.Left < 0.005m)
            {
                buyerAt++;
            }

            if (seller.Left < 0.005m)
            {
                sellerAt++;
            }
        }

        stamped.AddRange(extra);
        return Finish(stamped, headerBusinessUnitId, headerLegalEntityId, invoices, extra);
    }

    public static async Task<ErpIcPreparedJournal> PrepareAsync(
        DbConnection connection,
        IReadOnlyList<ErpGlLine> lines,
        long headerBusinessUnitId,
        long headerLegalEntityId,
        CancellationToken cancellationToken)
    {
        await ErpIntercompanySetupService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePmMastersAsync(connection, cancellationToken).ConfigureAwait(false);
        var businessUnits = await LoadPartiesAsync(
            connection,
            """
            SELECT b.`id`, IFNULL(b.`code`, ''), IFNULL(b.`trn`, ''), IFNULL(b.`legal_entity_id`, 0)
            FROM `epc_erp_pm_business_units` b
            WHERE b.`active` = 1
            """,
            cancellationToken).ConfigureAwait(false);
        var legalEntities = await LoadPartiesAsync(
            connection,
            """
            SELECT `id`, IFNULL(`code`, ''), IFNULL(`trn`, ''), 0
            FROM `epc_erp_pm_legal_entities`
            WHERE `active` = 1
            """,
            cancellationToken).ConfigureAwait(false);
        var accounts = await LoadAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var rate = await ErpDb.DecimalAsync(
            connection,
            null,
            "SELECT `sales_vat_rate` FROM `epc_erp_ic_policy` WHERE `id` = 1",
            cancellationToken).ConfigureAwait(false);
        if (rate <= 0m)
        {
            rate = 5m;
        }

        return Expand(lines, headerBusinessUnitId, headerLegalEntityId, businessUnits, legalEntities, accounts, rate);
    }

    public static async Task PersistAsync(
        DbConnection connection,
        long journalId,
        ErpIcPreparedJournal prepared,
        long companyId,
        CancellationToken cancellationToken)
    {
        if (prepared.Invoices.Count == 0 || journalId <= 0)
        {
            return;
        }

        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_erp_ic_invoices` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `journal_id` int(11) NOT NULL DEFAULT 0,
              `scope` varchar(32) NOT NULL DEFAULT '',
              `seller_code` varchar(40) NOT NULL DEFAULT '',
              `buyer_code` varchar(40) NOT NULL DEFAULT '',
              `seller_trn` varchar(40) NOT NULL DEFAULT '',
              `buyer_trn` varchar(40) NOT NULL DEFAULT '',
              `net_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
              `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
              `gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
              `vat_rate` decimal(6,2) NOT NULL DEFAULT 0.00,
              `treatment` varchar(32) NOT NULL DEFAULT '',
              `invoice_no` varchar(40) NOT NULL DEFAULT '',
              `time_created` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              KEY `x_journal` (`journal_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var n = 0;
        foreach (var invoice in prepared.Invoices)
        {
            n++;
            var prefix = invoice.Treatment == "different_trn" ? "IC-TAX-" : "IC-INT-";
            var number = prefix + journalId.ToString(CultureInfo.InvariantCulture) + "-" + n.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_ic_invoices` (`journal_id`,`scope`,`seller_code`,`buyer_code`,`seller_trn`,`buyer_trn`,"
                    + " `net_amount`,`vat_amount`,`gross_amount`,`vat_rate`,`treatment`,`invoice_no`,`time_created`)"
                    + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                journalId,
                invoice.Scope,
                invoice.SellerCode,
                invoice.BuyerCode,
                invoice.SellerTrn,
                invoice.BuyerTrn,
                invoice.Net,
                invoice.Vat,
                invoice.Gross,
                invoice.Rate,
                invoice.Treatment,
                number,
                now).ConfigureAwait(false);
            if (invoice.SellerCode.Length == 0
                || invoice.BuyerCode.Length == 0
                || string.Equals(invoice.SellerCode, invoice.BuyerCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = invoice.Scope + "|" + invoice.SellerCode + "|" + invoice.BuyerCode;
            if (!seen.Add(key))
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_ic_relationships` (`scope`,`from_code`,`to_code`,`role`,`active`,`company_id`,`time_created`)"
                    + " VALUES (?,?,?,'both',1,?,?)"
                    + " ON DUPLICATE KEY UPDATE `role` = 'both', `active` = 1"),
                cancellationToken,
                invoice.Scope,
                invoice.SellerCode,
                invoice.BuyerCode,
                companyId,
                now).ConfigureAwait(false);
        }
    }

    private static ErpGlLine Stamp(
        ErpGlLine line,
        long headerBusinessUnitId,
        long headerLegalEntityId,
        IReadOnlyDictionary<long, ErpIcPartyRef> businessUnits)
    {
        var bu = line.BusinessUnitId > 0 ? line.BusinessUnitId : headerBusinessUnitId;
        var le = line.LegalEntityId;
        if (le <= 0 && bu > 0 && businessUnits.TryGetValue(bu, out var unit) && unit.LegalEntityId > 0)
        {
            le = unit.LegalEntityId;
        }

        if (le <= 0)
        {
            le = headerLegalEntityId;
        }

        return line with { BusinessUnitId = bu, LegalEntityId = le };
    }

    private static string TrnOf(
        long businessUnitId,
        long legalEntityId,
        IReadOnlyDictionary<long, ErpIcPartyRef> businessUnits,
        IReadOnlyDictionary<long, ErpIcPartyRef> legalEntities)
    {
        if (businessUnitId > 0
            && businessUnits.TryGetValue(businessUnitId, out var unit)
            && ErpIntercompanyTax.TrnDigits(unit.Trn).Length > 0)
        {
            return ErpIntercompanyTax.TrnDigits(unit.Trn);
        }

        if (legalEntityId > 0 && legalEntities.TryGetValue(legalEntityId, out var entity))
        {
            var trn = ErpIntercompanyTax.TrnDigits(entity.Trn);
            if (trn.Length > 0)
            {
                return trn;
            }
        }

        if (businessUnitId > 0
            && businessUnits.TryGetValue(businessUnitId, out var parentUnit)
            && parentUnit.LegalEntityId > 0
            && legalEntities.TryGetValue(parentUnit.LegalEntityId, out var parent))
        {
            return ErpIntercompanyTax.TrnDigits(parent.Trn);
        }

        return string.Empty;
    }

    private static string CodeOf(
        long businessUnitId,
        long legalEntityId,
        bool useUnits,
        IReadOnlyDictionary<long, ErpIcPartyRef> businessUnits,
        IReadOnlyDictionary<long, ErpIcPartyRef> legalEntities)
    {
        if (useUnits
            && businessUnitId > 0
            && businessUnits.TryGetValue(businessUnitId, out var unit)
            && unit.Code.Length > 0)
        {
            return unit.Code;
        }

        if (legalEntityId > 0
            && legalEntities.TryGetValue(legalEntityId, out var entity)
            && entity.Code.Length > 0)
        {
            return entity.Code;
        }

        if (businessUnitId > 0)
        {
            return "BU-" + businessUnitId.ToString(CultureInfo.InvariantCulture);
        }

        return legalEntityId > 0
            ? "LE-" + legalEntityId.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static ErpIcPreparedJournal Finish(
        List<ErpGlLine> lines,
        long headerBusinessUnitId,
        long headerLegalEntityId,
        IReadOnlyList<ErpIcInvoiceDraft> invoices,
        IReadOnlyList<ErpGlLine> extra)
    {
        var units = lines.Select(line => line.BusinessUnitId).Where(id => id > 0).Distinct().ToList();
        var entities = lines.Select(line => line.LegalEntityId).Where(id => id > 0).Distinct().ToList();
        var note = string.Empty;
        if (extra.Count > 0)
        {
            var tax = invoices.Any(row => row.Treatment == "different_trn" && row.Vat > 0m);
            var rate = invoices.FirstOrDefault(row => row.Vat > 0m)?.Rate ?? 0m;
            note = tax
                ? "GL journal posted. Different TRN: tax invoice at "
                    + ErpIntercompanyTax.FormatRate(rate)
                    + ". Intercompany receivable and payable were added so each business unit and legal entity stays in balance."
                : "GL journal posted. Same TRN: not a supply and not a tax invoice. Intercompany receivable and payable were added so each business unit and legal entity stays in balance.";
        }

        return new ErpIcPreparedJournal
        {
            Lines = lines,
            HeaderBusinessUnitId = units.Count switch
            {
                0 => headerBusinessUnitId,
                1 => units[0],
                _ => 0L,
            },
            HeaderLegalEntityId = entities.Count switch
            {
                0 => headerLegalEntityId,
                1 => entities[0],
                _ => 0L,
            },
            Note = note,
            Invoices = invoices,
        };
    }

    private static async Task<Dictionary<long, ErpIcPartyRef>> LoadPartiesAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<long, ErpIcPartyRef>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                rows[id] = new ErpIcPartyRef(
                    id,
                    Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
            return rows;
        }

        return rows;
    }

    private static async Task<ErpIcAccountIds> LoadAccountsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `code`, `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1150','1180','2100','2180') AND `active` = 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty] = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        return new ErpIcAccountIds(
            ids.GetValueOrDefault("1180"),
            ids.GetValueOrDefault("2180"),
            ids.GetValueOrDefault("1150"),
            ids.GetValueOrDefault("2100"));
    }
}
