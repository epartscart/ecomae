using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwIfrsCheck(string Area, string Standard, string Check, decimal Expected, decimal Actual, bool Passed, string Note)
{
    public decimal Difference => Math.Round(Actual - Expected, 2);
}

public sealed record ErpJwIfrsAccount(string Code, string Name, string Type, decimal Opening, decimal Debit, decimal Credit)
{
    public decimal Closing => Opening + Debit - Credit;
}

public sealed record ErpJwIfrsCashFlowLine(string Section, string Label, decimal Amount);

public sealed record ErpJwIfrsNrvLine(string Code, string Karat, decimal Purity, decimal Grams, decimal Carrying, decimal MetalNrv, decimal Shortfall);

public sealed record ErpJwIfrsEclBucket(string Bucket, decimal Exposure, decimal Rate, decimal Allowance);

public sealed record ErpJwIfrsReport(
    DateOnly From,
    DateOnly To,
    long CompanyId,
    IReadOnlyList<ErpJwIfrsCheck> Checks,
    IReadOnlyList<ErpJwIfrsAccount> Accounts,
    IReadOnlyList<ErpJwIfrsCashFlowLine> CashFlow,
    IReadOnlyList<(string Label, decimal Amount)> EquityMovements,
    IReadOnlyList<ErpJwIfrsNrvLine> Nrv,
    IReadOnlyList<ErpJwIfrsEclBucket> Ecl,
    IReadOnlyList<(string Reference, decimal Posted, decimal StockCost)> PurchaseGaps,
    string Message);

/// <summary>Pure IFRS / UAE VAT reconciliation rules — covered by unit tests.</summary>
public static class ErpJwIfrsRules
{
    public const decimal Tolerance = 0.01m;

    public static bool IsCash(string code) => code.StartsWith("10", StringComparison.Ordinal);

    public static bool IsPl(string type) => type is "revenue" or "expense";

    /// <summary>IAS 7 classification of the non-cash side of a cash journal.</summary>
    public static string CashFlowSection(string counterCode, string counterType)
    {
        if (counterType == "equity") return "Financing";
        if (counterType == "liability" && string.CompareOrdinal(counterCode, "2500") >= 0 && string.CompareOrdinal(counterCode, "2999") <= 0) return "Financing";
        if (counterType == "asset" && string.CompareOrdinal(counterCode, "1500") >= 0 && string.CompareOrdinal(counterCode, "1999") <= 0) return "Investing";
        return "Operating";
    }

    /// <summary>Simplified IFRS 9.5.5.15 provision matrix (illustrative rates — accountant to calibrate on loss history).</summary>
    public static readonly IReadOnlyList<(string Bucket, int MaxDays, decimal Rate)> EclMatrix =
    [
        ("Current (0–30 days)", 30, 0.01m),
        ("31–90 days", 90, 0.03m),
        ("91–180 days", 180, 0.10m),
        ("181–365 days", 365, 0.25m),
        ("Over 365 days", int.MaxValue, 0.50m),
    ];

    public static IReadOnlyList<ErpJwIfrsEclBucket> Ecl(IEnumerable<(int AgeDays, decimal Outstanding)> receivables)
    {
        var list = receivables.Where(r => r.Outstanding > 0).ToList();
        return EclMatrix.Select((b, i) =>
        {
            var min = i == 0 ? int.MinValue : EclMatrix[i - 1].MaxDays + 1;
            var exposure = list.Where(r => r.AgeDays >= min && r.AgeDays <= b.MaxDays).Sum(r => r.Outstanding);
            return new ErpJwIfrsEclBucket(b.Bucket, exposure, b.Rate, Math.Round(exposure * b.Rate, 2, MidpointRounding.AwayFromZero));
        }).ToList();
    }

    /// <summary>IAS 2.9 indicator: carrying cost above the metal-content NRV (24K rate × purity × grams, less costs to sell).</summary>
    public static ErpJwIfrsNrvLine Nrv(string code, string karat, decimal purity, decimal grams, decimal carrying, decimal rate24k, decimal costToSellPct)
    {
        var nrv = Math.Round(grams * purity * rate24k * (1 - costToSellPct), 2, MidpointRounding.AwayFromZero);
        return new(code, karat, purity, grams, carrying, nrv, Math.Max(0m, Math.Round(carrying - nrv, 2)));
    }

    public static ErpJwIfrsCheck Check(string area, string standard, string check, decimal expected, decimal actual, string note = "")
    {
        expected = Math.Round(expected, 2);
        actual = Math.Round(actual, 2);
        return new(area, standard, check, expected, actual, Math.Abs(actual - expected) <= Tolerance, note);
    }

    /// <summary>Statement-level checks over the trial balance accounts.</summary>
    public static List<ErpJwIfrsCheck> LedgerChecks(IReadOnlyList<ErpJwIfrsAccount> accounts, int unbalancedJournals)
    {
        var checks = new List<ErpJwIfrsCheck>
        {
            Check("General ledger", "Conceptual Framework / double entry", "Journals whose debits ≠ credits", 0, unbalancedJournals),
            Check("Trial balance", "IAS 1.15 fair presentation", "Total debits − total credits (closing)", 0, accounts.Sum(a => a.Closing)),
        };
        decimal Sum(Func<ErpJwIfrsAccount, bool> f) => accounts.Where(f).Sum(a => a.Closing);
        var assets = Sum(a => a.Type == "asset");
        var liabilities = -Sum(a => a.Type == "liability");
        var equity = -Sum(a => a.Type == "equity");
        var result = -Sum(a => IsPl(a.Type));
        checks.Add(Check("Statement of financial position", "IAS 1.54", "Assets = liabilities + equity + retained result", liabilities + equity + result, assets));
        return checks;
    }

    /// <summary>IAS 7: cash flows by section; the total must equal the movement on cash accounts.</summary>
    public static List<ErpJwIfrsCashFlowLine> CashFlow(IEnumerable<(string Section, string Label, decimal Amount)> flows)
        => flows.GroupBy(f => (f.Section, f.Label))
            .Select(g => new ErpJwIfrsCashFlowLine(g.Key.Section, g.Key.Label, Math.Round(g.Sum(x => x.Amount), 2)))
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.Section switch { "Operating" => 0, "Investing" => 1, _ => 2 })
            .ThenBy(l => l.Label, StringComparer.Ordinal)
            .ToList();
}

public interface IErpJwIfrsReconciliationReadService
{
    Task<ErpJwIfrsReport> ReadAsync(DateOnly? from, DateOnly? to, decimal costToSellPct = 0.02m, CancellationToken cancellationToken = default);
}

public sealed class ErpJwIfrsReconciliationReadService : IErpJwIfrsReconciliationReadService
{
    private const string JournalUnix =
        "(CASE WHEN j.`journal_date` IS NULL THEN 0 WHEN j.`journal_date` < 100000000 THEN IFNULL(UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR), '%Y%m%d')), 0) ELSE j.`journal_date` END)";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwIfrsReconciliationReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwIfrsReport> ReadAsync(DateOnly? from, DateOnly? to, decimal costToSellPct = 0.02m, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, 1, 1);
        if (!_connections.IsConfigured)
        {
            return new(start, end, 0, [], [], [], [], [], [], [], "Database is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpDb.TableKnownAsync(c, "epc_erp_gl_lines", cancellationToken).ConfigureAwait(false))
        {
            return new(start, end, 0, [], [], [], [], [], [], [], "The general ledger is not provisioned yet.");
        }

        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(c, 0, cancellationToken).ConfigureAwait(false);
        var fromUnix = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var toUnix = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        const string scope = " j.`active`=1 AND IFNULL(j.`company_id`,0) IN (0,?) ";

        var accounts = new List<ErpJwIfrsAccount>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT a.`code`,a.`name`,a.`account_type`,"
                + " SUM(CASE WHEN " + JournalUnix + "<? THEN l.`debit`-l.`credit` ELSE 0 END),"
                + " SUM(CASE WHEN " + JournalUnix + ">=? AND " + JournalUnix + "<? THEN l.`debit` ELSE 0 END),"
                + " SUM(CASE WHEN " + JournalUnix + ">=? AND " + JournalUnix + "<? THEN l.`credit` ELSE 0 END)"
                + " FROM `epc_erp_gl_lines` l JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` JOIN `epc_erp_coa_accounts` a ON a.`id`=l.`coa_id`"
                + " WHERE" + scope + "AND " + JournalUnix + "<? GROUP BY a.`id`,a.`code`,a.`name`,a.`account_type` ORDER BY a.`code`");
            ErpDb.AddParameters(cmd, fromUnix, fromUnix, toUnix, fromUnix, toUnix, companyId, toUnix);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                accounts.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), D(r, 3), D(r, 4), D(r, 5)));
            }
        }

        var unbalanced = (int)await ErpDb.LongAsync(c, null, ErpDb.Positional(
            "SELECT COUNT(*) FROM (SELECT l.`journal_id` FROM `epc_erp_gl_lines` l JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` WHERE" + scope
            + " GROUP BY l.`journal_id` HAVING ABS(SUM(l.`debit`)-SUM(l.`credit`))>0.004) x"), cancellationToken, companyId).ConfigureAwait(false);
        var checks = ErpJwIfrsRules.LedgerChecks(accounts, unbalanced);

        decimal Bal(Func<ErpJwIfrsAccount, bool> f) => accounts.Where(f).Sum(a => a.Closing);
        decimal Mov(Func<ErpJwIfrsAccount, bool> f) => accounts.Where(f).Sum(a => a.Debit - a.Credit);

        var jewel = await ErpDb.TableKnownAsync(c, "epc_jewel_voucher", cancellationToken).ConfigureAwait(false);
        var nrv = new List<ErpJwIfrsNrvLine>();
        var ecl = new List<ErpJwIfrsEclBucket>();
        var gaps = new List<(string, decimal, decimal)>();
        if (jewel)
        {
            var stockValue = await ErpDb.DecimalAsync(c, null, ErpDb.Positional("SELECT IFNULL(SUM(`stock_value`),0) FROM `epc_jewel_metal_stock` WHERE `company_id` IN (0,?)"), cancellationToken, companyId).ConfigureAwait(false);
            checks.Add(ErpJwIfrsRules.Check("Inventory", "IAS 2.36", "GL inventory 1300 + 1320 vs stock sub-ledger value",
                stockValue + Bal(a => a.Code == "1320"), Bal(a => a.Code is "1300" or "1320"),
                "Differences come from non-jewellery journals to 1300 or vouchers whose posted cost differs from the stock moved (listed below)."));

            async Task<decimal> VoucherSum(string expr, IEnumerable<string> types)
            {
                var t = types.Distinct().ToArray();
                if (t.Length == 0) return 0;
                var args = new List<object?> { companyId };
                args.AddRange(t);
                args.Add(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                args.Add(end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return await ErpDb.DecimalAsync(c, null, ErpDb.Positional(
                    "SELECT IFNULL(SUM(" + expr + "),0) FROM `epc_jewel_voucher` v WHERE v.`company_id` IN (0,?) AND v.`status` NOT IN ('draft','cancelled')"
                    + " AND IFNULL(v.`gl_mode`,'post')<>'history' AND v.`voc_type` IN (" + string.Join(",", t.Select(_ => "?")) + ")"
                    + " AND DATE(v.`voc_date`)>=? AND DATE(v.`voc_date`)<=?"), cancellationToken, args.ToArray()).ConfigureAwait(false);
            }

            var sales = ErpJwVocTypes.SalesAnalysisAliases.Concat(ErpJwVocTypes.RetailAliases).Concat(ErpJwVocTypes.MetalSaleAliases).ToArray();
            var purchases = ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases).ToArray();
            var outVat = await VoucherSum("v.`vat_amount`", sales).ConfigureAwait(false)
                + await VoucherSum("v.`vat_amount`", ErpJwVocTypes.AdvanceAliases).ConfigureAwait(false)
                - await VoucherSum("v.`vat_amount`", ErpJwVocTypes.SalesReturnAliases).ConfigureAwait(false);
            var rcm = await VoucherSum("v.`rcm_vat`", purchases).ConfigureAwait(false);
            var inVat = await VoucherSum("v.`vat_amount`", purchases).ConfigureAwait(false);
            checks.Add(ErpJwIfrsRules.Check("VAT", "UAE VAT Law Art. 25 / FTA VAT201 box 1", "Output VAT 2100 period credit vs jewellery documents (sales + advances − credit notes + RCM)",
                outVat + rcm, -Mov(a => a.Code == "2100"), "Other ERP modules posting to 2100 also show here."));
            checks.Add(ErpJwIfrsRules.Check("VAT", "UAE VAT Law Art. 54 / VAT201 box 9-10", "Input VAT 1150 period debit vs purchase documents (+ reverse charge)",
                inVat + rcm, Mov(a => a.Code == "1150")));
            var cogs = await ErpDb.DecimalAsync(c, null, ErpDb.Positional(
                "SELECT IFNULL(SUM(CASE WHEN v.`voc_type` IN (" + string.Join(",", ErpJwVocTypes.SalesReturnAliases.Select(_ => "?")) + ") THEN -l.`cost_amount` ELSE l.`cost_amount` END),0)"
                + " FROM `epc_jewel_voucher_lines` l JOIN `epc_jewel_voucher` v ON v.`id`=l.`voucher_id` WHERE v.`company_id` IN (0,?) AND v.`status` NOT IN ('draft','cancelled')"
                + " AND IFNULL(v.`gl_mode`,'post')<>'history' AND v.`voc_type` IN (" + string.Join(",", sales.Concat(ErpJwVocTypes.SalesReturnAliases).Select(_ => "?")) + ")"
                + " AND DATE(v.`voc_date`)>=? AND DATE(v.`voc_date`)<=?"),
                cancellationToken,
                ErpJwVocTypes.SalesReturnAliases.Cast<object?>().Append(companyId).Concat(sales).Concat(ErpJwVocTypes.SalesReturnAliases)
                    .Append(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray()).ConfigureAwait(false);
            checks.Add(ErpJwIfrsRules.Check("Cost of sales", "IAS 2.34", "COGS 5000 period movement vs cost of stock issued less returns", cogs, Mov(a => a.Code == "5000")));

            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT CONCAT(v.`voc_type`,'-',v.`voc_no`,' (#',v.`id`,')'),IFNULL((SELECT SUM(gl.`debit`) FROM `epc_erp_gl_journals` gj JOIN `epc_erp_gl_lines` gl ON gl.`journal_id`=gj.`id`"
                    + " JOIN `epc_erp_coa_accounts` ga ON ga.`id`=gl.`coa_id` WHERE gj.`reference`=CONCAT('JW-V-',v.`id`,'-PUR') AND ga.`code`='1300'),0),"
                    + " IFNULL((SELECT SUM(x.`cost_amount`) FROM `epc_jewel_voucher_lines` x WHERE x.`voucher_id`=v.`id`),0)"
                    + " FROM `epc_jewel_voucher` v WHERE v.`company_id` IN (0,?) AND v.`voc_type` IN (" + string.Join(",", purchases.Select(_ => "?")) + ") ORDER BY v.`id`");
                ErpDb.AddParameters(cmd, purchases.Cast<object?>().Prepend(companyId).ToArray());
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var posted = D(r, 1);
                    var cost = D(r, 2);
                    if (posted > 0 && cost > 0 && Math.Abs(posted - cost) > ErpJwIfrsRules.Tolerance) gaps.Add((r.GetString(0), posted, cost));
                }
            }

            var rate24 = await ErpGoldRateBoard.ResolveSellAsync(c, (int)companyId, "24K", "AED", end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            if (rate24 > 0)
            {
                await using var cmd = c.CreateCommand();
                cmd.CommandText = ErpDb.Positional("SELECT `item_code`,IFNULL(`karat`,''),IFNULL(`purity`,0),IFNULL(`stock_gms`,0),IFNULL(`stock_value`,0) FROM `epc_jewel_metal_stock` WHERE `company_id` IN (0,?) AND `stock_gms`>0 AND `stock_value`>0 ORDER BY `item_code`");
                ErpDb.AddParameters(cmd, companyId);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var purity = D(r, 2);
                    if (purity > 1) purity /= 1000m;
                    nrv.Add(ErpJwIfrsRules.Nrv(r.GetString(0), r.GetString(1), purity, D(r, 3), D(r, 4), rate24, costToSellPct));
                }
            }

            var ar = new List<(int, decimal)>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT DATEDIFF(?,DATE(v.`voc_date`)),v.`total_with_vat`-IFNULL(v.`receipt_total`,0)-IFNULL(v.`old_gold_exchange`,0) FROM `epc_jewel_voucher` v"
                    + " WHERE v.`company_id` IN (0,?) AND v.`status` NOT IN ('draft','cancelled') AND DATE(v.`voc_date`)<=? AND v.`voc_type` IN (" + string.Join(",", sales.Select(_ => "?")) + ")");
                ErpDb.AddParameters(cmd, new object?[] { end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), companyId, end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }.Concat(sales).ToArray());
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ar.Add((r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture), D(r, 1)));
                }
            }

            ecl.AddRange(ErpJwIfrsRules.Ecl(ar));
        }

        var flows = new List<(string, string, decimal)>();
        var lines = new List<(long J, string Code, string Name, string Type, decimal Net)>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT l.`journal_id`,a.`code`,a.`name`,a.`account_type`,l.`debit`-l.`credit` FROM `epc_erp_gl_lines` l JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                + " JOIN `epc_erp_coa_accounts` a ON a.`id`=l.`coa_id` WHERE" + scope + "AND " + JournalUnix + ">=? AND " + JournalUnix + "<?"
                + " AND l.`journal_id` IN (SELECT l2.`journal_id` FROM `epc_erp_gl_lines` l2 JOIN `epc_erp_coa_accounts` a2 ON a2.`id`=l2.`coa_id` WHERE a2.`code` LIKE '10%')");
            ErpDb.AddParameters(cmd, companyId, fromUnix, toUnix);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add((r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), D(r, 4)));
            }
        }

        foreach (var j in lines.GroupBy(l => l.J))
        {
            var cash = j.Where(l => ErpJwIfrsRules.IsCash(l.Code)).Sum(l => l.Net);
            var others = j.Where(l => !ErpJwIfrsRules.IsCash(l.Code)).ToList();
            var otherTotal = others.Sum(l => -l.Net);
            if (cash == 0 || others.Count == 0 || otherTotal == 0) continue;
            foreach (var o in others)
            {
                flows.Add((ErpJwIfrsRules.CashFlowSection(o.Code, o.Type), o.Code + " " + o.Name, Math.Round(cash * (-o.Net / otherTotal), 2)));
            }
        }

        var cashFlow = ErpJwIfrsRules.CashFlow(flows);
        checks.Add(ErpJwIfrsRules.Check("Cash flow", "IAS 7.45", "Net cash flow vs movement on cash and bank (10xx)", Mov(a => ErpJwIfrsRules.IsCash(a.Code)), cashFlow.Sum(f => f.Amount),
            "Transfers between cash accounts net to zero."));

        var openingEquity = -accounts.Where(a => a.Type == "equity" || ErpJwIfrsRules.IsPl(a.Type)).Sum(a => a.Opening);
        var profit = -Mov(a => ErpJwIfrsRules.IsPl(a.Type));
        var other = -Mov(a => a.Type == "equity");
        var equityMoves = new List<(string, decimal)>
        {
            ("Equity at " + start.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " (incl. retained earnings)", Math.Round(openingEquity, 2)),
            ("Profit / (loss) for the period", Math.Round(profit, 2)),
            ("Capital and other equity movements", Math.Round(other, 2)),
            ("Equity at " + end.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), Math.Round(openingEquity + profit + other, 2)),
        };
        var closingEquity = -accounts.Where(a => a.Type == "equity" || ErpJwIfrsRules.IsPl(a.Type)).Sum(a => a.Closing);
        checks.Add(ErpJwIfrsRules.Check("Changes in equity", "IAS 1.106", "Opening equity + profit + movements = closing equity", closingEquity, openingEquity + profit + other));

        return new(start, end, companyId, checks, accounts, cashFlow, equityMoves, nrv, ecl, gaps, "");
    }

    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);
}
