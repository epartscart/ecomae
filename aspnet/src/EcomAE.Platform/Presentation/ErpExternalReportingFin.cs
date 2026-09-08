namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_fin_dataset</c> / <c>epc_ext_fin_projection</c> twin — statement model from revenue + PBT.</summary>
public sealed record ErpExtFinYear(
    decimal Rev, decimal Cogs, decimal Gross, decimal Opex, decimal Depr, decimal Interest,
    decimal Ebitda, decimal Pbt, decimal Tax, decimal Profit,
    decimal Ppe, decimal Intang, decimal Inventory, decimal Receivables, decimal Payables,
    decimal BorrowCur, decimal BorrowNon, decimal Lease, decimal Provisions,
    decimal ShareCap, decimal Reserves,
    decimal Retained, decimal Equity, decimal Liabs, decimal NonCashAssets, decimal Cash,
    decimal TotalAssets, decimal TotalLiab, decimal TotalEquity);

public sealed record ErpExtFinDataset(
    DateTime From,
    DateTime To,
    string CurLabel,
    string PriLabel,
    int CurYear,
    int PriYear,
    ErpExtFinYear Cur,
    ErpExtFinYear Pri,
    decimal Dividends,
    bool Live);

public sealed record ErpExtFinAssume(
    decimal Growth, decimal GrossMargin, decimal OpexPct, decimal DeprPct, decimal InterestPct,
    decimal CapexPct, decimal WcPct, decimal TaxRate, decimal TaxFree, decimal Wacc, decimal TerminalGrowth);

public sealed record ErpExtFinYearProj(
    int Year, int N, decimal Rev, decimal Gross, decimal Opex, decimal Ebitda, decimal Depr, decimal Ebit,
    decimal Interest, decimal Pbt, decimal Tax, decimal Profit, decimal Capex, decimal DWc, decimal Nopat, decimal Fcf);

public sealed record ErpExtFinProjection(
    ErpExtFinAssume Assume,
    IReadOnlyList<ErpExtFinYearProj> Years,
    ErpExtFinYearProj Base);

public static class ErpExternalReportingFin
{
    public static ErpExtFinDataset Dataset(DateTime from, DateTime to, decimal sales, decimal purch)
    {
        if (to < from) (from, to) = (to, from);
        var rev = sales;
        var exp = purch;
        if (rev <= 0.005m)
        {
            var samp = ErpExternalReportingBuild.PeriodSample(from, to);
            rev = samp.Rev;
            if (exp <= 0.005m) exp = samp.Exp;
        }
        else if (exp <= 0.005m)
        {
            exp = decimal.Round(rev * 0.68m, 2);
        }

        var live = sales > 0.005m;
        var pbt = decimal.Round(rev - exp, 2);
        const decimal growth = 1.12m;
        var revP = decimal.Round(rev / growth, 2);
        var pbtP = decimal.Round(pbt / growth, 2);
        var cur = Year(rev, pbt);
        var pri = Year(revP, pbtP);

        pri = BalancePrior(pri);
        var dividends = decimal.Round(cur.Profit * 0.30m, 2);
        cur = RollForward(cur, pri, dividends);

        return new(
            from, to,
            "FY" + to.Year,
            "FY" + (to.Year - 1),
            to.Year,
            to.Year - 1,
            cur, pri, dividends, live);
    }

    public static ErpExtFinProjection Project(ErpExtFinDataset d)
    {
        var cur = d.Cur;
        var assume = new ErpExtFinAssume(
            0.12m,
            decimal.Round(cur.Gross / Math.Max(1m, cur.Rev), 4),
            decimal.Round(cur.Opex / Math.Max(1m, cur.Rev), 4),
            decimal.Round(cur.Depr / Math.Max(1m, cur.Rev), 4),
            decimal.Round(cur.Interest / Math.Max(1m, cur.Rev), 4),
            0.06m, 0.04m, 0.09m, 375000m, 0.12m, 0.03m);
        var years = new List<ErpExtFinYearProj>(5);
        var rev = cur.Rev;
        for (var i = 1; i <= 5; i++)
        {
            var prevRev = rev;
            rev = decimal.Round(rev * (1 + assume.Growth), 2);
            var gross = decimal.Round(rev * assume.GrossMargin, 2);
            var opex = decimal.Round(rev * assume.OpexPct, 2);
            var depr = decimal.Round(rev * assume.DeprPct, 2);
            var interest = decimal.Round(rev * assume.InterestPct, 2);
            var ebitda = decimal.Round(gross - opex, 2);
            var ebit = decimal.Round(ebitda - depr, 2);
            var pbt = decimal.Round(ebit - interest, 2);
            var tax = decimal.Round(Math.Max(0m, pbt - assume.TaxFree) * assume.TaxRate, 2);
            var profit = decimal.Round(pbt - tax, 2);
            var capex = decimal.Round(rev * assume.CapexPct, 2);
            var dWc = decimal.Round((rev - prevRev) * assume.WcPct, 2);
            var nopat = decimal.Round(ebit * (1 - assume.TaxRate), 2);
            var fcf = decimal.Round(nopat + depr - capex - dWc, 2);
            years.Add(new(d.CurYear + i, i, rev, gross, opex, ebitda, depr, ebit, interest, pbt, tax, profit, capex, dWc, nopat, fcf));
        }

        var ebitBase = decimal.Round(cur.Ebitda - cur.Depr, 2);
        var baseY = new ErpExtFinYearProj(d.CurYear, 0, cur.Rev, cur.Gross, cur.Opex, cur.Ebitda, cur.Depr, ebitBase,
            cur.Interest, cur.Pbt, cur.Tax, cur.Profit, 0, 0, 0, 0);
        return new(assume, years, baseY);
    }

    private static ErpExtFinYear Year(decimal rev, decimal pbt)
    {
        var depr = decimal.Round(rev * 0.040m, 2);
        var cogs = decimal.Round(rev * 0.560m, 2);
        var interest = decimal.Round(rev * 0.012m, 2);
        var gross = decimal.Round(rev - cogs, 2);
        var opex = decimal.Round(rev - cogs - depr - interest - pbt, 2);
        var ebitda = decimal.Round(pbt + interest + depr, 2);
        var tax = decimal.Round(Math.Max(0m, pbt - 375000m) * 0.09m, 2);
        var profit = decimal.Round(pbt - tax, 2);
        return new(
            rev, cogs, gross, opex, depr, interest, ebitda, pbt, tax, profit,
            decimal.Round(rev * 0.42m, 2), decimal.Round(rev * 0.05m, 2), decimal.Round(rev * 0.11m, 2),
            decimal.Round(rev * 0.16m, 2), decimal.Round(rev * 0.13m, 2),
            decimal.Round(rev * 0.05m, 2), decimal.Round(rev * 0.18m, 2),
            decimal.Round(rev * 0.03m, 2), decimal.Round(rev * 0.02m, 2),
            500000m, decimal.Round(rev * 0.01m, 2),
            0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static ErpExtFinYear BalancePrior(ErpExtFinYear pri)
    {
        var nonCash = pri.Ppe + pri.Intang + pri.Inventory + pri.Receivables;
        var liabs = pri.Payables + pri.Tax + pri.BorrowCur + pri.BorrowNon + pri.Lease + pri.Provisions;
        var retained = decimal.Round(pri.Profit * 2.6m, 2);
        var equity = pri.ShareCap + pri.Reserves + retained;
        var cash = decimal.Round((equity + liabs) - nonCash, 2);
        return pri with
        {
            NonCashAssets = nonCash,
            Liabs = liabs,
            Retained = retained,
            Equity = equity,
            Cash = cash,
            TotalAssets = decimal.Round(nonCash + cash, 2),
            TotalLiab = liabs,
            TotalEquity = equity,
        };
    }

    private static ErpExtFinYear RollForward(ErpExtFinYear cur, ErpExtFinYear pri, decimal dividends)
    {
        var retained = decimal.Round(pri.Retained + cur.Profit - dividends, 2);
        var equity = cur.ShareCap + cur.Reserves + retained;
        var liabs = cur.Payables + cur.Tax + cur.BorrowCur + cur.BorrowNon + cur.Lease + cur.Provisions;
        var nonCash = cur.Ppe + cur.Intang + cur.Inventory + cur.Receivables;
        var cash = decimal.Round((equity + liabs) - nonCash, 2);
        return cur with
        {
            Retained = retained,
            Equity = equity,
            Liabs = liabs,
            NonCashAssets = nonCash,
            Cash = cash,
            TotalAssets = decimal.Round(nonCash + cash, 2),
            TotalLiab = liabs,
            TotalEquity = equity,
        };
    }
}
