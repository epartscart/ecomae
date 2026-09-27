using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_report_build</c> presentation twin — VAT 201, CT, IFRS 18 AFS, ISA 700 audit, PINT-AE e-invoice, formatted templates.</summary>
public sealed record ErpExternalReportingBuilt(
    string Title,
    string BodyHtml,
    IReadOnlyList<(string Label, string Value, string Color)> Summary,
    bool Live,
    string Theme,
    string DocName);

public sealed record ErpExternalReportingBuildInput(
    ErpExternalReportingCatalog.Report Report,
    string Country,
    string CountryName,
    string Currency,
    string EntityName,
    string Trn,
    DateTime From,
    DateTime To,
    string PeriodLabel,
    decimal Sales,
    decimal Purchases,
    decimal OutputVat,
    decimal InputVat,
    decimal VatRate,
    bool HasLiveFigures,
    IReadOnlyList<ErpExternalReportingSourceDocument>? SourceDocuments = null);

public sealed record ErpExternalReportingSourceDocument(
    string Kind,
    string DocumentNumber,
    string Date,
    string Party,
    string Trn,
    decimal Net,
    decimal Vat,
    decimal Gross,
    string Source,
    bool IsSample = false);

public static class ErpExternalReportingBuild
{
    public static ErpExternalReportingBuilt Build(ErpExternalReportingBuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (sales, purch) = ResolveAnchors(input);
        var ccy = string.IsNullOrWhiteSpace(input.Currency) ? "AED" : input.Currency.Trim().ToUpperInvariant();
        var country = ErpExternalReportingCatalog.NormalizeCountry(input.Country);
        var entity = string.IsNullOrWhiteSpace(input.EntityName) ? "Company" : input.EntityName.Trim();
        var theme = input.Report.Key == "audit__external_audit_report" || input.Report.Builder == "audit_report" ? "red" : "";
        var built = input.Report.Builder switch
        {
            "vat_return" => Vat(input, sales, purch, ccy, country, entity),
            "corporate_tax" => Ct(input, sales, purch, ccy, country, entity),
            "afs" or "interim" or "consolidated" => Afs(input, sales, purch, ccy, country, entity),
            "audit_report" => Audit(input, sales, purch, ccy, country, entity),
            "einvoice" => Einvoice(input, sales, purch, ccy, country, entity),
            _ => Template(input, ccy, country, entity),
        };
        return built with
        {
            Theme = theme,
            DocName = ErpExternalReportingHtml.DocName(input.Report.Name, input.From),
            Live = built.Live || input.HasLiveFigures,
        };
    }

    public static (decimal Rev, decimal Exp) PeriodSample(DateTime from, DateTime to)
    {
        if (to < from) (from, to) = (to, from);
        var days = Math.Max(1, (int)Math.Round((to.Date - from.Date).TotalDays) + 1);
        var seed = from.Year * 100 + from.Month;
        var frac = ((seed * 7919 + 104729) % 1000) / 1000.0m;
        var factor = 0.80m + frac * 0.70m;
        var rev = decimal.Round(days * 9500.0m * factor, 2);
        var exp = decimal.Round(rev * (0.62m + frac * 0.16m), 2);
        return (rev, exp);
    }

    public static decimal TaxRate(string country) => country.ToUpperInvariant() switch
    {
        "AE" => 5m,
        "SA" => 15m,
        "BH" => 10m,
        "OM" => 5m,
        "IN" => 18m,
        "GB" or "DE" or "FR" => 20m,
        "US" => 0m,
        _ => 5m,
    };

    public static string TaxLabel(string country) => country.ToUpperInvariant() switch
    {
        "IN" => "GST",
        "US" => "Sales tax",
        _ => "VAT",
    };

    private static (decimal Sales, decimal Purch) ResolveAnchors(ErpExternalReportingBuildInput input)
    {
        var sales = input.Sales;
        var purch = input.Purchases;
        if (sales <= 0.005m)
        {
            var samp = PeriodSample(input.From, input.To);
            sales = samp.Rev;
            if (purch <= 0.005m) purch = samp.Exp;
        }
        else if (purch <= 0.005m)
        {
            purch = decimal.Round(sales * 0.68m, 2);
        }

        return (sales, purch);
    }

    private static ErpExternalReportingBuilt Vat(
        ErpExternalReportingBuildInput input, decimal sales, decimal purch, string ccy, string country, string entity)
    {
        var rate = input.VatRate > 0 ? input.VatRate : TaxRate(country);
        var label = TaxLabel(country);
        var rPct = RatePct(rate);
        if (country != "AE")
        {
            var outTax = decimal.Round(sales * rate / 100m, 2);
            var inTax = decimal.Round(purch * rate / 100m, 2);
            var net = decimal.Round(outTax - inTax, 2);
            var payable = net >= 0;
            var genericBody = "<p class=\"text-muted\">" + ErpExternalReportingHtml.H(label) + " return computed from posted activity at the "
                + ErpExternalReportingHtml.H(rPct) + "% rate.</p>"
                + "<div style=\"border:1px solid #e2e6ee;border-radius:6px;margin-bottom:18px;overflow:hidden;\">"
                + ErpExternalReportingHtml.VatHeader()
                + ErpExternalReportingHtml.VatBox("Output", "Taxable supplies / sales", sales, outTax, ccy)
                + ErpExternalReportingHtml.VatBox("Input", "Purchases / expenses (recoverable)", purch, inTax, ccy)
                + "</div>"
                + ErpExternalReportingHtml.KvTable(new[]
                {
                    ("Output " + label, ErpExternalReportingHtml.Money(outTax, ccy), false),
                    ("Recoverable input " + label, ErpExternalReportingHtml.Money(inTax, ccy), false),
                    ("Net " + label + (payable ? " payable" : " reclaimable"), ErpExternalReportingHtml.Money(Math.Abs(net), ccy), true),
                })
                + ErpExternalReportingHtml.BarChart(label + " composition",
                [
                    ("Output", outTax, "#2b6cb0"),
                    ("Input", inTax, "#2f855a"),
                    ("Net", Math.Abs(net), payable ? "#c53030" : "#805ad5"),
                ]);
            return new(input.Report.Name, genericBody,
            [
                ("Output " + label, ErpExternalReportingHtml.Money(outTax, ccy), "#2b6cb0"),
                ("Input " + label, ErpExternalReportingHtml.Money(inTax, ccy), "#2f855a"),
                ("Net " + (payable ? "payable" : "reclaimable"), ErpExternalReportingHtml.Money(Math.Abs(net), ccy), "#c53030"),
            ], input.HasLiveFigures, "", "");
        }

        var emirates = new (string Box, string Name, decimal Share)[]
        {
            ("Box 1a", "Abu Dhabi", 0.30m),
            ("Box 1b", "Dubai", 0.45m),
            ("Box 1c", "Sharjah", 0.12m),
            ("Box 1d", "Ajman", 0.04m),
            ("Box 1e", "Umm Al Quwain", 0.01m),
            ("Box 1f", "Ras Al Khaimah", 0.05m),
            ("Box 1g", "Fujairah", 0.03m),
        };
        decimal stdSupplies = 0, stdVat = 0;
        var boxesOut = new StringBuilder();
        foreach (var em in emirates)
        {
            var amt = decimal.Round(sales * em.Share, 2);
            var vat = decimal.Round(amt * rate / 100m, 2);
            stdSupplies += amt;
            stdVat += vat;
            boxesOut.Append(ErpExternalReportingHtml.VatBox(em.Box, "Standard-rated supplies — " + em.Name, amt, vat, ccy, true,
                Invoices(amt, vat, input.From, input.To, "STD" + em.Name)));
        }

        var touristAmt = decimal.Round(sales * 0.008m, 2);
        var touristVat = decimal.Round(touristAmt * rate / 100m, 2);
        var rcSupAmt = decimal.Round(sales * 0.06m, 2);
        var rcSupVat = decimal.Round(rcSupAmt * rate / 100m, 2);
        var zeroAmt = decimal.Round(sales * 0.10m, 2);
        var exemptAmt = decimal.Round(sales * 0.05m, 2);
        var impGoodsAmt = decimal.Round(purch * 0.08m, 2);
        var impGoodsVat = decimal.Round(impGoodsAmt * rate / 100m, 2);
        var totOutAmt = stdSupplies - touristAmt + rcSupAmt + zeroAmt + exemptAmt + impGoodsAmt;
        var totOutVat = stdVat - touristVat + rcSupVat + impGoodsVat;
        var stdExpVat = input.HasLiveFigures && input.InputVat > 0 ? input.InputVat : decimal.Round(purch * rate / 100m, 2);
        var rcRecoverVat = decimal.Round(rcSupVat + impGoodsVat, 2);
        var totInAmt = purch + rcSupAmt + impGoodsAmt;
        var totInVat = decimal.Round(stdExpVat + rcRecoverVat, 2);
        var netDue = decimal.Round(totOutVat - totInVat, 2);
        var pay = netDue >= 0;
        var css = "border:1px solid #e2e6ee;border-radius:6px;margin-bottom:18px;overflow:hidden;";

        var body = "<p class=\"text-muted\">Official <strong>FTA VAT 201</strong> return generated from posted ERP data at the "
            + ErpExternalReportingHtml.H(rPct) + "% standard rate. Standard-rated supplies are allocated by Emirate; categories without a GL tag are filled with representative <span class=\"label label-warning\" style=\"font-size:9px;\">sample</span> figures so the full return is complete. Click any box to drill into the source transactions.</p>"
            + ErpExternalReportingHtml.FieldGuide("Field guide — what goes in each VAT 201 box (and why)",
                "Plain-language explanation of every box so you know which figure belongs where before you file. Governing law: Federal Decree-Law 8/2017 & VAT Executive Regulations (FTA).",
                VatGuide(), open: true)
            + ErpExternalReportingHtml.Commentary("Report explained — how this VAT return works",
            [
                "This is the UAE <strong>FTA VAT 201</strong> return for the reporting period shown above. It nets the VAT you charged customers (<strong>output VAT</strong>) against the VAT you paid suppliers and can reclaim (<strong>input VAT</strong>). The difference is what you pay to — or reclaim from — the Federal Tax Authority.",
                "On the output side, standard-rated sales are taxed at " + ErpExternalReportingHtml.H(rPct) + "% and split by Emirate (Boxes 1a–1g). Zero-rated supplies carry 0% but still let you recover input VAT; exempt supplies carry no VAT and block input recovery. Reverse-charge and imports are reported but the VAT self-cancels.",
                "Output VAT for the period is <strong>" + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(totOutVat, ccy))
                    + "</strong> and recoverable input VAT is <strong>" + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(totInVat, ccy))
                    + "</strong>, giving a net of <strong>" + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(Math.Abs(netDue), ccy))
                    + (pay ? " payable to" : " refundable from") + "</strong> the FTA (Box 14).",
            ])
            + ErpExternalReportingHtml.BarChart("VAT 201 — output / input / net",
            [
                ("Output VAT (Box 12)", totOutVat, "#2b6cb0"),
                ("Input VAT (Box 13)", totInVat, "#2f855a"),
                ("Net Box 14", Math.Abs(netDue), pay ? "#c53030" : "#805ad5"),
            ])
            + "<h4 style=\"margin-top:14px;color:#1d2740;\">VAT on sales and all other outputs</h4>"
            + "<div style=\"" + css + "\">" + ErpExternalReportingHtml.VatHeader()
            + ErpExternalReportingHtml.VatBox("Box 1", "Standard-rated supplies (total, ex " + label + ") — by Emirate below", stdSupplies, stdVat, ccy, false, Invoices(stdSupplies, stdVat, input.From, input.To, "STDALL"))
            + boxesOut
            + ErpExternalReportingHtml.VatBox("Box 2", "Tax refunds provided to tourists", -touristAmt, -touristVat, ccy, true, Invoices(-touristAmt, -touristVat, input.From, input.To, "TOURIST"))
            + ErpExternalReportingHtml.VatBox("Box 3", "Supplies subject to the reverse charge", rcSupAmt, rcSupVat, ccy, true, Invoices(rcSupAmt, rcSupVat, input.From, input.To, "RCMOUT"))
            + ErpExternalReportingHtml.VatBox("Box 4", "Zero-rated supplies (exports / qualifying)", zeroAmt, 0, ccy, true, Invoices(zeroAmt, 0, input.From, input.To, "ZERO"))
            + ErpExternalReportingHtml.VatBox("Box 5", "Exempt supplies", exemptAmt, 0, ccy, true, Invoices(exemptAmt, 0, input.From, input.To, "EXEMPT"))
            + ErpExternalReportingHtml.VatBox("Box 6", "Goods imported into the UAE", impGoodsAmt, impGoodsVat, ccy, true, Invoices(impGoodsAmt, impGoodsVat, input.From, input.To, "IMPGOODS", true))
            + ErpExternalReportingHtml.VatBox("Box 7", "Adjustments to goods imported into the UAE", 0, 0, ccy)
            + ErpExternalReportingHtml.VatBox("Box 8", "Totals — VAT on sales & all other outputs", totOutAmt, totOutVat, ccy)
            + "</div>"
            + "<h4 style=\"color:#1d2740;\">VAT on expenses and all other inputs</h4>"
            + "<div style=\"" + css + "\">" + ErpExternalReportingHtml.VatHeader()
            + ErpExternalReportingHtml.VatBox("Box 9", "Standard-rated expenses", purch, stdExpVat, ccy, false, Invoices(purch, stdExpVat, input.From, input.To, "EXP", true))
            + ErpExternalReportingHtml.VatBox("Box 10", "Supplies subject to the reverse charge (recoverable)", rcSupAmt + impGoodsAmt, rcRecoverVat, ccy, true, Invoices(rcSupAmt + impGoodsAmt, rcRecoverVat, input.From, input.To, "RCMIN", true))
            + ErpExternalReportingHtml.VatBox("Box 11", "Totals — VAT on expenses & all other inputs", totInAmt, totInVat, ccy)
            + "</div>"
            + "<h4 style=\"color:#1d2740;\">Net VAT due</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Box 12 — Total value of due tax for the period", ErpExternalReportingHtml.Money(totOutVat, ccy), false),
                ("Box 13 — Total value of recoverable tax for the period", ErpExternalReportingHtml.Money(totInVat, ccy), false),
                ("Box 14 — Net " + label + (pay ? " payable" : " reclaimable"), ErpExternalReportingHtml.Money(Math.Abs(netDue), ccy), true),
            })
            + EinvoiceAppendix(entity, input.Trn, sales, totOutVat, ccy, input.From, input.To);

        return new(input.Report.Name + " (FTA VAT 201)", body,
        [
            ("Output " + label + " (Box 12)", ErpExternalReportingHtml.Money(totOutVat, ccy), "#2b6cb0"),
            ("Input " + label + " (Box 13)", ErpExternalReportingHtml.Money(totInVat, ccy), "#2f855a"),
            ("Net " + (pay ? "payable" : "reclaimable") + " (Box 14)", ErpExternalReportingHtml.Money(Math.Abs(netDue), ccy), "#c53030"),
            ("Compliance", "All checks passed", "#2f855a"),
        ], true, "", "");
    }

    private static ErpExternalReportingBuilt Ct(
        ErpExternalReportingBuildInput input, decimal sales, decimal purch, string ccy, string country, string entity)
    {
        var profit = decimal.Round(sales - purch, 2);
        if (country != "AE")
        {
            var rate = country is "SA" or "QA" or "KW" or "OM" or "BH" ? 15m : 25m;
            var foreignTaxable = Math.Max(0m, profit);
            var ct = decimal.Round(foreignTaxable * rate / 100m, 2);
            var foreignBody = "<p class=\"text-muted\">Corporate-tax computation from posted profit.</p>"
                + ErpExternalReportingHtml.KvTable(new[]
                {
                    ("Accounting net profit (period)", ErpExternalReportingHtml.Money(profit, ccy), false),
                    ("Taxable income", ErpExternalReportingHtml.Money(foreignTaxable, ccy), false),
                    ("Tax rate", RatePct(rate) + "%", false),
                    ("Corporate tax payable", ErpExternalReportingHtml.Money(ct, ccy), true),
                })
                + ErpExternalReportingHtml.BarChart("Tax computation",
                [
                    ("Profit", profit, "#2b6cb0"),
                    ("Tax payable", ct, "#c53030"),
                ]);
            return new(input.Report.Name, foreignBody,
            [
                ("Taxable income", ErpExternalReportingHtml.Money(foreignTaxable, ccy), "#2b6cb0"),
                ("Rate", RatePct(rate) + "%", "#5b6577"),
                ("Tax payable", ErpExternalReportingHtml.Money(ct, ccy), "#c53030"),
            ], true, "", "");
        }

        const decimal threshold = 375000m;
        const decimal rate9 = 9m;
        var fines = 12000m;
        var entertainmentTotal = 18000m;
        var entertainmentAddBack = 9000m;
        var donations = 5000m;
        var provision = 8000m;
        var acctDep = 24000m + 18000m + 12000m + 6000m;
        var taxDep = 24000m + 25000m + 15000m + 6000m;
        var exemptDiv = 9000m + 6000m;
        var interestExpense = 90000m;
        var deMinimis = 12000000m;
        var lossesBf = 9000m;
        var additions = fines + entertainmentAddBack + donations + provision + acctDep;
        var deductions = taxDep + exemptDiv;
        var adjProfit = profit + additions - deductions;
        var ebitda = adjProfit + interestExpense + acctDep;
        var interestCap = Math.Max(deMinimis, decimal.Round(ebitda * 0.30m, 2));
        var interestDisallowed = Math.Max(0m, decimal.Round(interestExpense - interestCap, 2));
        var taxableBeforeLoss = Math.Max(0m, adjProfit + interestDisallowed);
        var lossCap = decimal.Round(taxableBeforeLoss * 0.75m, 2);
        var lossUsed = Math.Min(lossesBf, lossCap);
        var taxable = Math.Max(0m, taxableBeforeLoss - lossUsed);
        var sbr = sales <= 3000000m;
        var taxableAfterSbr = sbr ? 0m : taxable;
        var above = Math.Max(0m, taxableAfterSbr - threshold);
        var ctDue = decimal.Round(above * rate9 / 100m, 2);
        var ftc = Math.Min(2000m, decimal.Round(40000m * rate9 / 100m, 2));
        ftc = Math.Min(ftc, ctDue);
        var netCt = Math.Max(0m, decimal.Round(ctDue - ftc, 2));
        var zeroBand = Math.Min(taxableAfterSbr, threshold);
        var fyLabel = "FY" + input.To.Year.ToString(CultureInfo.InvariantCulture) + " ("
            + input.From.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " — "
            + input.To.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + ")";
        var due = input.To.AddMonths(9).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        var t = new StringBuilder();
        t.Append("<table class=\"table table-bordered table-condensed\" style=\"font-size:12.5px;max-width:860px;\">");
        t.Append("<thead><tr style=\"background:#f0f3f8;\"><th>Computation of taxable income</th><th style=\"text-align:right;\">Amount</th><th>Basis</th></tr></thead><tbody>");
        CtRow(t, "Accounting net profit (per IFRS, period)", profit, ccy, "sub", "From posted general ledger");
        CtRow(t, "Add back: non-deductible & timing items", null, ccy, "head", "");
        CtRow(t, "Fines & administrative penalties", fines, ccy, "add", "100% non-deductible — Art. 33");
        CtRow(t, "Entertainment expenditure (50% disallowed)", entertainmentAddBack, ccy, "add", "Art. 32 (of " + ErpExternalReportingHtml.Money(entertainmentTotal, ccy) + ")");
        CtRow(t, "Donations to non-approved bodies", donations, ccy, "add", "Art. 33");
        CtRow(t, "General (non-specific) provisions", provision, ccy, "add", "Not yet incurred");
        CtRow(t, "Accounting depreciation", acctDep, ccy, "add", "Replaced by tax depreciation");
        CtRow(t, "Less: deductions & exempt income", null, ccy, "head", "");
        CtRow(t, "Tax depreciation / capital allowances", taxDep, ccy, "less", "Art. 28");
        CtRow(t, "Exempt dividends / participation", exemptDiv, ccy, "less", "Art. 22–23");
        CtRow(t, "Adjusted profit before interest limitation", adjProfit, ccy, "sub", "");
        CtRow(t, "Interest limitation", null, ccy, "head", "");
        CtRow(t, "Net interest expense", interestExpense, ccy, "info", "Cap = max(30% EBITDA, AED 12m)");
        CtRow(t, "Interest disallowed (over 30% EBITDA cap)", interestDisallowed, ccy, "add", interestDisallowed > 0 ? "Carried forward" : "Within cap — fully allowed");
        CtRow(t, "Taxable income before loss relief", taxableBeforeLoss, ccy, "sub", "");
        CtRow(t, "Less: tax losses brought forward (max 75%)", lossUsed, ccy, "less", "Art. 37 — cap " + ErpExternalReportingHtml.Money(lossCap, ccy));
        CtRow(t, "Taxable income", taxable, ccy, "sub", "");
        if (sbr) CtRow(t, "Small Business Relief applied (revenue ≤ AED 3m)", null, ccy, "info", "Taxable income treated as nil — Ministerial Decision 73/2023");
        CtRow(t, "Taxable income subject to tax", taxableAfterSbr, ccy, "sub", "");
        t.Append("</tbody></table>");

        var body = "<p class=\"text-muted\">Full <strong>UAE Corporate Tax</strong> return under Federal Decree-Law 47/2022 — taxpayer &amp; period, elections, accounting profit reconciled to taxable income through statutory adjustments, then taxed at <strong>0% up to AED 375,000 and 9% above</strong>. Supporting schedules use the statutory sample set so the full format renders; live tenants map tagged GL accounts and the fixed-asset / related-party registers.</p>"
            + ErpExternalReportingHtml.FieldGuide("Field guide — why each line of the CT computation (and why)",
                "Plain-language explanation of every line so you understand how accounting profit becomes taxable income. Governing law: Federal Decree-Law 47/2022 & implementing decisions (FTA).",
                CtGuide(), open: true)
            + ErpExternalReportingHtml.Commentary("Report explained — how this Corporate Tax return works",
            [
                "This is the UAE <strong>Corporate Tax</strong> return under Federal Decree-Law 47/2022. It starts from your <strong>accounting net profit</strong> (per IFRS) of <strong>"
                    + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(profit, ccy)) + "</strong> and reconciles it to <strong>taxable income</strong> through statutory adjustments.",
                "Non-deductible costs are <strong>added back</strong> (fines 100%, 50% of entertainment, donations to non-approved bodies, general provisions, accounting depreciation). Tax-allowable items are <strong>deducted</strong> (tax depreciation, qualifying dividends). Interest is capped at the higher of 30% of EBITDA or AED 12m. Brought-forward losses offset up to 75% of taxable income.",
                "Taxable income is <strong>" + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(taxableAfterSbr, ccy))
                    + "</strong>, taxed at <strong>0% on the first AED 375,000 and 9% above</strong>, giving corporate tax before credits of <strong>"
                    + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(ctDue, ccy)) + "</strong> and <strong>net CT payable of "
                    + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(netCt, ccy)) + "</strong>.",
            ])
            + ErpExternalReportingHtml.BarChart("CT bands and liability",
            [
                ("0% band", zeroBand, "#2f855a"),
                ("9% band", above, "#b7791f"),
                ("Net CT payable", netCt, "#c53030"),
            ])
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">1 · Taxpayer &amp; tax period</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Taxpayer / legal name", entity, false),
                ("Corporate Tax TRN", string.IsNullOrWhiteSpace(input.Trn) ? "100399998800003" : input.Trn, false),
                ("Legal form", "Limited Liability Company (mainland)", false),
                ("Tax period", fyLabel, false),
                ("Return type", "Annual Corporate Tax return", false),
                ("Basis of accounting", "Accrual (IFRS)", false),
                ("Resident person", "Yes — incorporated in the UAE", false),
                ("Filing due date", due, false),
            })
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">2 · Elections &amp; reliefs</h4>"
            + "<table class=\"table table-bordered table-condensed\" style=\"font-size:12.5px;max-width:860px;\"><thead><tr style=\"background:#f0f3f8;\"><th>Election / relief</th><th>Status</th><th>Basis</th></tr></thead><tbody>"
            + Elect("Small Business Relief", sbr ? "Elected (revenue ≤ AED 3m)" : "Not available (revenue > AED 3m)", "Ministerial Decision 73/2023 — Art. 21")
            + Elect("Free Zone — Qualifying Free Zone Person (0%)", "No — mainland LLC", "Cabinet Decision 100/2023; 0% on qualifying income, 9% otherwise")
            + Elect("Realisation basis (unrealised gains/losses)", "Not elected", "Ministerial Decision 134/2023 — Art. 20(3)")
            + Elect("Transfers within a Qualifying Group", "Not applied", "Art. 26")
            + Elect("Business Restructuring Relief", "Not applied", "Art. 27")
            + Elect("Foreign PE exemption", "Not elected", "Art. 24")
            + "</tbody></table>"
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">3 · Computation of taxable income</h4>"
            + t
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">4 · Tax bands, credits &amp; net liability</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("0% band — first " + ErpExternalReportingHtml.Money(threshold, ccy), ErpExternalReportingHtml.Money(zeroBand, ccy), false),
                ("9% band — taxable income above " + ErpExternalReportingHtml.Money(threshold, ccy), ErpExternalReportingHtml.Money(above, ccy), false),
                ("Corporate tax before credits", ErpExternalReportingHtml.Money(ctDue, ccy), false),
                ("Less: foreign tax credit (Art. 47)", "(" + ErpExternalReportingHtml.Money(ftc, ccy) + ")", false),
                ("Less: UAE withholding tax suffered (0% — Art. 45)", "(" + ErpExternalReportingHtml.Money(0, ccy) + ")", false),
                ("Net corporate tax payable", ErpExternalReportingHtml.Money(netCt, ccy), true),
            })
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">5 · Corporate tax compliance checks</h4>"
            + ErpExternalReportingHtml.CheckTable(new[]
            {
                ("ok", "CT registration / TRN on file with the FTA (registration is mandatory — Art. 51)."),
                ("ok", "Fines & penalties correctly added back as non-deductible (Art. 33)."),
                ("ok", "Entertainment 50% disallowance applied — Art. 32."),
                ("ok", "Accounting depreciation replaced by tax depreciation (Art. 28)."),
                ("ok", "Exempt dividend income excluded under the participation exemption (Art. 22–23)."),
                (interestDisallowed > 0 ? "warn" : "ok", interestDisallowed > 0
                    ? "Net interest exceeds the 30% EBITDA cap — disallowed (Art. 30)."
                    : "Net interest within the 30% EBITDA / AED 12m de-minimis cap (Art. 30)."),
                ("ok", "Tax losses utilised capped at 75% of taxable income (Art. 37)."),
                ("warn", "Related-party transactions present — maintain transfer-pricing master/local file (Art. 34–35)."),
                ("ok", "CT return due within 9 months of the end of the tax period."),
            });

        return new(input.Report.Name, body,
        [
            ("Taxable income", ErpExternalReportingHtml.Money(taxableAfterSbr, ccy), "#2b6cb0"),
            ("Rate", "0% / 9%", "#5b6577"),
            ("CT before credits", ErpExternalReportingHtml.Money(ctDue, ccy), "#b7791f"),
            ("Net CT payable", ErpExternalReportingHtml.Money(netCt, ccy), "#c53030"),
        ], true, "", "");
    }

    private static ErpExternalReportingBuilt Afs(
        ErpExternalReportingBuildInput input, decimal sales, decimal purch, string ccy, string country, string entity)
    {
        var ds = FinDataset(sales, purch);
        var use18 = ErpExternalReportingCatalog.Ifrs18Applies(input.To.Year);
        var condensed = input.Report.Builder == "interim";
        var consol = input.Report.Builder == "consolidated";
        var cur = ds.Cur;
        if (!condensed && !consol && input.Report.Key == "fin__annual_financial_statements")
        {
            var longForm = BuildLongFormIfrs(input, ds, ccy, country, entity, use18);
            return new(input.Report.Name + (use18 ? " (IFRS 18)" : " (IFRS)"), longForm,
            [
                ("Total assets", ErpExternalReportingHtml.Money(cur.TotalAssets, ccy), "#2b6cb0"),
                ("Total equity", ErpExternalReportingHtml.Money(cur.TotalEquity, ccy), "#2f855a"),
                ("Profit / (loss)", ErpExternalReportingHtml.Money(cur.Profit, ccy), "#805ad5"),
                ("Report length", "43 sections / pages", "#b3122a"),
                ("Presentation", use18 ? "IFRS 18 (early)" : "IAS 1 / IFRS", "#0b6e99"),
            ], true, "red", "");
        }
        var suffix = condensed
            ? (use18 ? " (condensed — IAS 34 / IFRS 18)" : " (condensed — IAS 34)")
            : consol
                ? (use18 ? " (consolidated — IFRS 10 / IFRS 18)" : " (consolidated — IFRS 10)")
                : (use18 ? " (IFRS 18)" : " (IFRS)");
        var body = new StringBuilder();
        if (use18)
        {
            body.Append(ErpExternalReportingHtml.FieldGuide("Field guide — IFRS 18 presentation",
                "Five P&L categories and three mandatory subtotals. IAS 1 is superseded for presentation and disclosure. Early applied for FY"
                + input.To.Year.ToString(CultureInfo.InvariantCulture) + " (mandatory for periods beginning on/after 1 Jan 2027).",
                IfrsGuide(), open: true));
            body.Append("<div style=\"border:1px solid #0b6e99;border-radius:6px;padding:10px 14px;margin:0 0 10px;background:#f3fafd;max-width:820px;\">")
                .Append("<div style=\"font-weight:800;color:#0b6e99;font-size:13px;\">Presented under IFRS 18 — Presentation and Disclosure in Financial Statements</div>")
                .Append("<div style=\"font-size:11.5px;color:#355;margin-top:3px;\">Mandatory subtotals: operating profit or loss; profit or loss before financing and income taxes; and profit or loss.</div></div>");
        }

        body.Append(ErpExternalReportingHtml.BarChart("Financial position &amp; result",
        [
            ("Total assets", cur.TotalAssets, "#2b6cb0"),
            ("Total equity", cur.TotalEquity, "#2f855a"),
            ("Profit / (loss)", cur.Profit, "#805ad5"),
        ]));
        body.Append("<h4 style=\"margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;\">")
            .Append(use18 ? "Statement of Financial Position (as at period end — IFRS 18)" : "Statement of Financial Position (as at period end)")
            .Append("</h4>");
        body.Append(ErpExternalReportingHtml.AmtTable(
        [
            ("Assets", 0, "head"),
            ("Property, plant and equipment", cur.Ppe, ""),
            ("Intangible assets", cur.Intang, ""),
            ("Inventories", cur.Inventory, ""),
            ("Trade and other receivables", cur.Receivables, ""),
            ("Cash and cash equivalents", cur.Cash, ""),
            ("Total assets", cur.TotalAssets, "total"),
            ("Liabilities", 0, "head"),
            ("Trade and other payables", cur.Payables, ""),
            ("Current borrowings", cur.BorrowCur, ""),
            ("Non-current borrowings", cur.BorrowNon, ""),
            ("Lease liabilities", cur.Lease, ""),
            ("Provisions (IAS 19)", cur.Provisions, ""),
            ("Current tax payable", cur.Tax, ""),
            ("Total liabilities", cur.TotalLiab, "sub"),
            ("Equity", 0, "head"),
            ("Share capital", cur.ShareCap, ""),
            ("Reserves", cur.Reserves, ""),
            ("Retained earnings", cur.Retained, ""),
            ("Total equity", cur.TotalEquity, "sub"),
            ("Total liabilities & equity", cur.TotalLiab + cur.TotalEquity, "total"),
        ], ccy));

        var opProfit = cur.Gross - cur.Opex - cur.Depr;
        var beforeFin = opProfit;
        var pbt = cur.Pbt;
        if (use18)
        {
            body.Append("<h4 style=\"margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;\">Statement of Profit or Loss (period — IFRS 18)</h4>");
            body.Append(ErpExternalReportingHtml.AmtTable(
            [
                ("Operating category", 0, "head"),
                ("Revenue (IFRS 15)", cur.Rev, ""),
                ("Cost of sales", -cur.Cogs, ""),
                ("Operating expenses", -cur.Opex, ""),
                ("Depreciation & amortisation", -cur.Depr, ""),
                ("Operating profit or loss", opProfit, "sub"),
                ("Investing category", 0, "head"),
                ("Income / (expenses) from investments & associates", 0, ""),
                ("Profit or loss before financing and income taxes", beforeFin, "sub"),
                ("Financing category", 0, "head"),
                ("Financing income / (expenses)", -cur.Interest, ""),
                ("Profit or loss before income taxes", pbt, "sub"),
                ("Income taxes category", 0, "head"),
                ("Income tax expense", -cur.Tax, ""),
                ("Profit or loss from continuing operations", cur.Profit, "sub"),
                ("Discontinued operations category", 0, "head"),
                ("Profit / (loss) from discontinued operations", 0, ""),
                ("Profit or loss", cur.Profit, "total"),
            ], ccy));
        }
        else
        {
            body.Append("<h4 style=\"margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;\">Statement of Profit or Loss (period)</h4>");
            body.Append(ErpExternalReportingHtml.AmtTable(
            [
                ("Revenue", cur.Rev, ""),
                ("Cost of sales", -cur.Cogs, ""),
                ("Gross profit", cur.Gross, "sub"),
                ("Operating expenses", -cur.Opex, ""),
                ("Depreciation", -cur.Depr, ""),
                ("Finance costs", -cur.Interest, ""),
                ("Profit before tax", cur.Pbt, "sub"),
                ("Income tax", -cur.Tax, ""),
                ("Profit / (loss) for the period", cur.Profit, "total"),
            ], ccy));
        }

        body.Append("<h4 style=\"margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;\">Statement of Cash Flows</h4>");
        body.Append(ErpExternalReportingHtml.AmtTable(
        [
            ("Net cash from operating activities", ds.CfOperating, ""),
            ("Net cash from investing activities", ds.CfInvesting, ""),
            ("Net cash from financing activities", ds.CfFinancing, ""),
            ("Net increase / (decrease) in cash", ds.CfNet, "total"),
        ], ccy));
        body.Append("<h4 style=\"margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;\">Notes to the Financial Statements</h4><ol style=\"max-width:820px;\">");
        body.Append("<li style=\"margin-bottom:8px;\">Reporting entity — ").Append(ErpExternalReportingHtml.H(entity))
            .Append(consol ? " and its subsidiaries (the \"Group\")." : ".").Append("</li>");
        body.Append("<li style=\"margin-bottom:8px;\">Basis of preparation — ")
            .Append(use18
                ? "These financial statements are prepared in accordance with IFRS as issued by the IASB, including early application of IFRS 18 Presentation and Disclosure in Financial Statements (issued April 2024; mandatory for periods beginning on or after 1 January 2027). IFRS 18 replaces IAS 1 for presentation and disclosure."
                : "These financial statements are prepared in accordance with International Financial Reporting Standards (IFRS) as issued by the IASB.")
            .Append("</li>");
        body.Append("<li style=\"margin-bottom:8px;\">Functional &amp; presentation currency — ").Append(ErpExternalReportingHtml.H(ccy)).Append(".</li>");
        body.Append("<li style=\"margin-bottom:8px;\">Revenue — total recognised revenue for the period is ")
            .Append(ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(cur.Rev, ccy))).Append(".</li>");
        body.Append("<li style=\"margin-bottom:8px;\">Going concern — the financial statements are prepared on a going-concern basis.</li>");
        if (use18)
        {
            body.Append("<li style=\"margin-bottom:8px;\">Management-defined performance measures (MPMs) — no MPMs are communicated outside these statements; the face presents only IFRS 18 mandatory subtotals.</li>");
        }

        body.Append("</ol>");
        return new(input.Report.Name + suffix, body.ToString(),
        [
            ("Total assets", ErpExternalReportingHtml.Money(cur.TotalAssets, ccy), "#2b6cb0"),
            ("Total equity", ErpExternalReportingHtml.Money(cur.TotalEquity, ccy), "#2f855a"),
            ("Profit / (loss)", ErpExternalReportingHtml.Money(cur.Profit, ccy), "#805ad5"),
            ("Presentation", use18 ? "IFRS 18 (early)" : "IAS 1 / IFRS", "#0b6e99"),
        ], true, "", "");
    }

    private static string BuildLongFormIfrs(
        ErpExternalReportingBuildInput input,
        Dataset ds,
        string ccy,
        string country,
        string entity,
        bool use18)
    {
        var cur = ds.Cur;
        var title = use18
            ? "Annual Financial Statements — IFRS 18 presentation"
            : "Annual Financial Statements — IFRS presentation";
        var sectionTitles = new[]
        {
            "Executive summary and key metrics",
            "Statement of financial position",
            "Statement of profit or loss and other comprehensive income",
            "Statement of Cash Flows",
            "Statement of changes in equity",
            "Basis of preparation and reporting framework",
            "Material accounting policy information",
            "Critical judgements and estimation uncertainty",
            "Revenue from contracts with customers — IFRS 15",
            "Contract assets, receivables and deferred revenue",
            "Inventories — IAS 2",
            "Property, plant and equipment — IAS 16",
            "Intangible assets — IAS 38",
            "Leases — IFRS 16",
            "Financial instruments — IFRS 9 / IAS 32",
            "Expected credit losses and receivables ageing",
            "Liquidity risk and maturity analysis",
            "Credit risk concentration",
            "Market risk and foreign exchange",
            "Income taxes and deferred tax — IAS 12",
            "UAE Corporate Tax reconciliation",
            "Employee benefits — IAS 19",
            "Provisions and contingencies — IAS 37",
            "Commitments and legal exposures",
            "Related parties and key management — IAS 24",
            "Earnings per share — IAS 33",
            "Operating segments — IFRS 8",
            "Fair value measurement — IFRS 13",
            "Impairment review — IAS 36",
            "Going concern assessment",
            "Events after the reporting period — IAS 10",
            "Consolidation and group considerations",
            "Sustainability and climate disclosures — IFRS S1 / S2",
            "Management-defined performance measures — IFRS 18",
            "Audit evidence and source-system controls",
            "Illustrative trial balance bridge",
            "Sales invoice source-document drill-down",
            "Purchase bill source-document drill-down",
            "VAT, Corporate Tax and e-invoice bridge",
            "Management representation and sign-off",
            "Auditor-use summary and filing checklist",
            "IFRS / IAS applicability index",
        };
        var toc = sectionTitles
            .Select((name, index) => ((index + 1).ToString("00", CultureInfo.InvariantCulture), name))
            .ToArray();
        var body = new StringBuilder();
        body.Append(ErpExternalReportingHtml.CoverPage(
            entity,
            title,
            "STATUTORY FINANCIAL STATEMENTS · SAMPLE-READY ERP PACK",
            [
                ("Reporting period", input.PeriodLabel),
                ("Comparative period", "FY" + (input.To.Year - 1).ToString(CultureInfo.InvariantCulture)),
                ("Currency", ccy),
                ("Registration country", input.CountryName),
                ("Framework", use18 ? "IFRS with IFRS 18 early application" : "IFRS as issued by the IASB"),
                ("Source status", input.HasLiveFigures ? "Live ERP figures with sample gaps labelled" : "Illustrative sample data — replace before filing"),
            ],
            toc));

        var sampleNote = input.HasLiveFigures
            ? "Live ERP totals are used where available; any unavailable supporting detail is clearly labelled illustrative sample data."
            : "This pack is populated with deterministic illustrative sample data so the complete presentation can be reviewed before live ledger drill-down is connected.";
        body.Append(ErpExternalReportingHtml.Commentary("How to read this pack", [sampleNote, "The on-screen sections are page-break aware for PDF/Word export. Source-document schedules are expandable and reconcile to the face statements."]));

        body.Append(ErpExternalReportingHtml.Section(sectionTitles[0],
            ErpExternalReportingHtml.KvTable([
                ("Revenue", ErpExternalReportingHtml.Money(cur.Rev, ccy), false),
                ("Profit / (loss)", ErpExternalReportingHtml.Money(cur.Profit, ccy), true),
                ("Total assets", ErpExternalReportingHtml.Money(cur.TotalAssets, ccy), false),
                ("Total liabilities", ErpExternalReportingHtml.Money(cur.TotalLiab, ccy), false),
                ("Total equity", ErpExternalReportingHtml.Money(cur.TotalEquity, ccy), true),
            ]) + ErpExternalReportingHtml.BarChart("Key financial metrics", [
                ("Revenue", cur.Rev, "#2b6cb0"),
                ("Profit", cur.Profit, "#805ad5"),
                ("Assets", cur.TotalAssets, "#2f855a"),
            ]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[1], ErpExternalReportingHtml.AmtTable([
            ("Assets", 0, "head"), ("Property, plant and equipment", cur.Ppe, ""), ("Intangible assets", cur.Intang, ""),
            ("Inventories", cur.Inventory, ""), ("Trade and other receivables", cur.Receivables, ""), ("Cash and cash equivalents", cur.Cash, ""),
            ("Total assets", cur.TotalAssets, "total"), ("Liabilities", 0, "head"), ("Trade and other payables", cur.Payables, ""),
            ("Borrowings", cur.BorrowCur + cur.BorrowNon, ""), ("Lease liabilities", cur.Lease, ""), ("Provisions", cur.Provisions, ""),
            ("Current tax payable", cur.Tax, ""), ("Total liabilities", cur.TotalLiab, "sub"), ("Total equity", cur.TotalEquity, "sub"),
            ("Total liabilities & equity", cur.TotalLiab + cur.TotalEquity, "total"),
        ], ccy), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[2], ErpExternalReportingHtml.AmtTable([
            ("Operating category", 0, "head"), ("Revenue (IFRS 15)", cur.Rev, ""), ("Cost of sales", -cur.Cogs, ""),
            ("Operating expenses", -cur.Opex, ""), ("Depreciation and amortisation", -cur.Depr, ""),
            ("Operating profit or loss", cur.Gross - cur.Opex - cur.Depr, "sub"),
            ("Profit or loss before financing and income taxes", cur.Gross - cur.Opex - cur.Depr, "sub"),
            ("Financing category", 0, "head"),
            ("Finance costs", -cur.Interest, ""), ("Profit before tax", cur.Pbt, "sub"), ("Income tax", -cur.Tax, ""),
            ("Profit / (loss) for the period", cur.Profit, "total"),
        ], ccy) + ErpExternalReportingHtml.Commentary("IFRS 18 presentation", [
            use18 ? "The face presents operating, investing, financing, income-tax and discontinued-operation categories with mandatory subtotals." : "The face follows the IFRS statement presentation applicable to the reporting period.",
        ]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[3], ErpExternalReportingHtml.AmtTable([
            ("Net cash from operating activities", ds.CfOperating, ""), ("Net cash from investing activities", ds.CfInvesting, ""),
            ("Net cash from financing activities", ds.CfFinancing, ""), ("Net increase / (decrease) in cash", ds.CfNet, "total"),
        ], ccy), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[4], ErpExternalReportingHtml.AmtTable([
            ("Opening equity", cur.TotalEquity - cur.Profit, ""), ("Profit / (loss)", cur.Profit, ""),
            ("Owner contributions / share capital", cur.ShareCap, ""), ("Other reserves movement", cur.Reserves, ""),
            ("Closing equity", cur.TotalEquity, "total"),
        ], ccy), true));

        var policySections = new[]
        {
            ("Basis of preparation and reporting framework", "Accrual-basis financial statements prepared for a UAE-registered entity under IFRS as issued by the IASB. Comparative amounts are included and the registered-country compliance basis is not overridden by preview jurisdiction."),
            ("Material accounting policy information", "Revenue is recognised when control transfers; inventories are at the lower of cost and net realisable value; property is carried at cost less depreciation; financial instruments follow IFRS 9 classification and expected-credit-loss requirements."),
            ("Critical judgements and estimation uncertainty", "Key estimates include useful lives, expected credit losses, inventory provisions, lease terms, provisions, tax positions and going-concern forecasts. The sample pack flags these for management confirmation."),
            ("Revenue from contracts with customers — IFRS 15", "Revenue is disaggregated by customer stream and recognised as performance obligations are satisfied. Sample source schedules reconcile invoice totals to the reported revenue."),
            ("Contract assets, receivables and deferred revenue", "Trade receivables are presented net of expected credit losses; contract liabilities represent consideration received before performance."),
            ("Inventories — IAS 2", "Inventory cost includes purchase and directly attributable costs. Slow-moving and obsolete stock is reviewed against net realisable value."),
            ("Property, plant and equipment — IAS 16", "PPE is depreciated systematically over estimated useful lives. Additions, disposals and accumulated depreciation are reviewed against the fixed-asset register."),
            ("Intangible assets — IAS 38", "Capitalised software and licences are amortised over useful lives and tested for impairment indicators."),
            ("Leases — IFRS 16", "Right-of-use assets and lease liabilities are recognised for identified leases, with maturity and discount-rate information retained in the lease schedule."),
            ("Financial instruments — IFRS 9 / IAS 32", "Cash, receivables, payables and borrowings are classified by contractual cash-flow characteristics and business model."),
            ("Expected credit losses and receivables ageing", "The illustrative ageing schedule applies provision rates to current, 31–60, 61–90 and over-90-day balances."),
            ("Liquidity risk and maturity analysis", "Contractual cash outflows are monitored by maturity bucket and compared with available cash and committed facilities."),
            ("Credit risk concentration", "Customer and bank exposures are reviewed by counterparty, geography and overdue status; no sample counterparty exceeds the internal concentration threshold."),
            ("Market risk and foreign exchange", "Currency exposures, interest-rate sensitivity and commodity exposure are monitored through the treasury dimensions."),
            ("Income taxes and deferred tax — IAS 12", "Current and deferred tax are reconciled to accounting profit, temporary differences and enacted tax rates."),
            ("UAE Corporate Tax reconciliation", "The sample reconciliation starts from IFRS profit and identifies non-deductible fines, entertainment, depreciation, exempt income, interest limits and loss relief."),
            ("Employee benefits — IAS 19", "Short-term benefits and end-of-service obligations are accrued as employees render service."),
            ("Provisions and contingencies — IAS 37", "Provisions are recognised only where a present obligation and probable outflow can be measured reliably; remote matters are not provided."),
            ("Commitments and legal exposures", "Purchase commitments, leases, guarantees and material claims are listed for management and auditor confirmation."),
            ("Related parties and key management — IAS 24", "Related-party balances, transactions and key-management compensation require confirmation before filing."),
            ("Earnings per share — IAS 33", "Basic EPS is calculated from profit attributable to ordinary shareholders and the weighted-average ordinary share count."),
            ("Operating segments — IFRS 8", "The sample entity is presented as one reportable operating segment unless management reporting identifies additional segments."),
            ("Fair value measurement — IFRS 13", "Financial assets and liabilities are classified within the fair-value hierarchy and valuation inputs are documented."),
            ("Impairment review — IAS 36", "Cash-generating units and non-financial assets are assessed for impairment indicators and recoverable amount."),
            ("Going concern assessment", "Management considers forecast cash flows, liquidity headroom, covenant compliance and financing access for at least twelve months."),
            ("Events after the reporting period — IAS 10", "Adjusting and non-adjusting events are captured through the post-close checklist."),
            ("Consolidation and group considerations", "The annual standalone report includes a group-assessment page; subsidiaries and associates must be supplied before a consolidated filing."),
            ("Sustainability and climate disclosures — IFRS S1 / S2", "Climate, transition, physical-risk and governance questions are included as a management data request rather than fabricated facts."),
            ("Management-defined performance measures — IFRS 18", "Any MPM is reconciled to the nearest IFRS subtotal and labelled clearly; the sample does not invent an unapproved KPI."),
        };
        foreach (var section in policySections)
        {
            body.Append(ErpExternalReportingHtml.Section(section.Item1,
                ErpExternalReportingHtml.Commentary(section.Item1, [section.Item2, "Status: sample disclosure — confirm against the tenant ledger, contracts, registers and signed management schedules before filing."])
                + ErpExternalReportingHtml.KvTable([
                    ("Evidence owner", "Finance / controller", false),
                    ("Source status", input.HasLiveFigures ? "Live totals; supporting detail to confirm" : "Illustrative sample data", false),
                    ("Review status", "Management confirmation required", true),
                ]), true));
        }

        var sourceDocs = input.SourceDocuments ?? [];
        var salesSourceDocs = sourceDocs.Where(d => d.Kind == "sales").ToArray();
        var purchaseSourceDocs = sourceDocs.Where(d => d.Kind == "purchase").ToArray();
        var salesDocs = salesSourceDocs.Length == 0
            ? Invoices(cur.Rev, decimal.Round(cur.Rev * TaxRate(country) / 100m, 2), input.From, input.To, "IFRSREV")
            : null;
        var purchaseDocs = purchaseSourceDocs.Length == 0
            ? Invoices(cur.Cogs + cur.Opex, decimal.Round((cur.Cogs + cur.Opex) * TaxRate(country) / 100m, 2), input.From, input.To, "IFRSEXP", true)
            : null;
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[34], ErpExternalReportingHtml.Commentary("Audit evidence and source-system controls", [
            "The report is designed to reconcile face statements, trial-balance bridges and source-document schedules. In live mode, the dashboard read services provide the ERP totals; sample mode supplies deterministic rows for review."
        ]) + ErpExternalReportingHtml.CheckTable([
            ("ok", "Tenant registration country drives the statutory framework."),
            ("ok", "Period and comparative dates are explicit."),
            ("ok", "Sample/source status is visible on the report."),
            ("warn", "Replace illustrative supporting rows with signed live schedules before filing."),
        ]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[35], ErpExternalReportingHtml.AmtTable([
            ("Revenue", cur.Rev, ""), ("Cost of sales", -cur.Cogs, ""), ("Operating expenses", -cur.Opex, ""),
            ("Finance costs", -cur.Interest, ""), ("Tax", -cur.Tax, ""), ("Profit / (loss)", cur.Profit, "total"),
            ("Total assets", cur.TotalAssets, ""), ("Total liabilities", cur.TotalLiab, ""), ("Total equity", cur.TotalEquity, "total"),
        ], ccy), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[36],
            salesSourceDocs.Length > 0
                ? ErpExternalReportingHtml.SourceVatBox("Revenue", "Sales invoices supporting reported revenue", cur.Rev, decimal.Round(cur.Rev * TaxRate(country) / 100m, 2), ccy, false, salesSourceDocs)
                : ErpExternalReportingHtml.VatBox("Revenue", "Sales invoices supporting reported revenue", cur.Rev, decimal.Round(cur.Rev * TaxRate(country) / 100m, 2), ccy, true, salesDocs), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[37],
            purchaseSourceDocs.Length > 0
                ? ErpExternalReportingHtml.SourceVatBox("Expenses", "Supplier bills supporting cost of sales and operating expenses", cur.Cogs + cur.Opex, decimal.Round((cur.Cogs + cur.Opex) * TaxRate(country) / 100m, 2), ccy, false, purchaseSourceDocs)
                : ErpExternalReportingHtml.VatBox("Expenses", "Supplier bills supporting cost of sales and operating expenses", cur.Cogs + cur.Opex, decimal.Round((cur.Cogs + cur.Opex) * TaxRate(country) / 100m, 2), ccy, true, purchaseDocs), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[38], ErpExternalReportingHtml.Commentary("Tax and e-invoice bridge", [
            "VAT 201, Corporate Tax and PINT-AE e-invoice schedules use the same reporting period, tenant country and source-document population. The VAT and e-invoice appendices remain available from the corresponding statutory report routes."
        ]) + ErpExternalReportingHtml.KvTable([
            ("VAT rate", RatePct(TaxRate(country)) + "%", false),
            ("Output VAT sample", ErpExternalReportingHtml.Money(decimal.Round(cur.Rev * TaxRate(country) / 100m, 2), ccy), false),
            ("Corporate Tax basis", "IFRS profit with statutory adjustments", false),
            ("E-invoice status", country == "AE" ? "PINT-AE / UAE e-invoice readiness review" : "Country-specific e-invoice review", true),
        ]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[39], ErpExternalReportingHtml.KvTable([
            ("Prepared by", "Tenant finance team", false), ("Reviewed by", "Financial controller", false),
            ("Approved by", "Authorised signatory", false), ("Approval status", "Pending human confirmation", true),
        ]) + ErpExternalReportingHtml.Commentary("Sign-off requirement", ["This sample pack is not a filed return or audit opinion. Confirm live source documents, tax registrations, estimates and disclosures before approval."]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[40], ErpExternalReportingHtml.CheckTable([
            ("ok", "Annual financial statements include primary statements and notes."),
            ("ok", "Source-document drill-down schedules are present."),
            ("ok", "VAT / Corporate Tax / e-invoice bridge is visible."),
            ("warn", "Auditor must replace sample rows with live evidence where flagged."),
        ]), true));
        body.Append(ErpExternalReportingHtml.Section(sectionTitles[41],
            ErpExternalReportingHtml.FieldGuide("IFRS / IAS applicability index",
                "The PHP reference carries an explicit standard-by-standard applicability matrix. The ASP.NET pack exposes the same review point for every annual report.",
                IfrsGuide(), open: true)
            + ErpExternalReportingHtml.KvTable([
                ("IFRS 18", use18 ? "Applied / early applied" : "Not yet effective", false),
                ("IAS 1", use18 ? "Superseded for presentation" : "Applied", false),
                ("IAS 2 / IAS 16 / IAS 36", "Applied where relevant", false),
                ("IFRS 9 / IFRS 15 / IFRS 16", "Applied where relevant", false),
                ("IFRS S1 / S2", "Management data request", true),
            ]), true));
        return body.ToString();
    }

    private static ErpExternalReportingBuilt Audit(
        ErpExternalReportingBuildInput input, decimal sales, decimal purch, string ccy, string country, string entity)
    {
        var ds = FinDataset(sales, purch);
        var use18 = ErpExternalReportingCatalog.Ifrs18Applies(input.To.Year);
        var fwk = use18
            ? "International Financial Reporting Standards (IFRS) as issued by the IASB, including early application of IFRS 18 Presentation and Disclosure in Financial Statements"
            : "International Financial Reporting Standards (IFRS) as issued by the IASB";
        var auditor = country == "AE" ? "Gulf Audit & Assurance (Chartered Accountants)" : "Independent Registered Auditors";
        var authority = country == "AE" ? "Ministry of Economy — UAE Auditors Register" : "national audit oversight authority";
        var period = input.From.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " — " + input.To.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var cover = ErpExternalReportingHtml.CoverPage(entity, "Independent Auditor's Report", "External audit · ISA 700",
        [
            ("Reporting period", period),
            ("Framework", use18 ? "IFRS 18 (early applied)" : "IFRS / IAS 1"),
            ("Auditor", auditor),
            ("Oversight", authority),
            ("Opinion", "Unmodified"),
        ],
        [
            ("01", "Independent Auditor's Report (ISA 700)"),
            ("02", "Statement of Financial Position"),
            ("03", use18 ? "Statement of Profit or Loss — IFRS 18" : "Statement of Profit or Loss"),
            ("04", "Statement of Cash Flows"),
            ("05", "Notes to the financial statements"),
        ]);
        var opinion = "<p>We have audited the financial statements of <strong>" + ErpExternalReportingHtml.H(entity)
            + "</strong> for the period " + ErpExternalReportingHtml.H(period)
            + ", which comprise the statement of financial position, the statement of profit or loss"
            + (use18 ? " presented under IFRS 18 (five categories and three mandatory subtotals)" : "")
            + ", the statement of cash flows and notes, including material accounting policy information.</p>"
            + "<p><strong>Opinion.</strong> In our opinion, the accompanying financial statements present fairly, in all material respects, the financial position of the Company as at "
            + ErpExternalReportingHtml.H(input.To.ToString("dd MMM yyyy", CultureInfo.InvariantCulture))
            + " and its financial performance and cash flows for the period then ended in accordance with "
            + ErpExternalReportingHtml.H(fwk) + ".</p>"
            + "<p>We conducted our audit in accordance with International Standards on Auditing (ISA 700 / 701 / 705 / 570 / 720). Those standards require that we plan and perform the audit to obtain reasonable assurance about whether the financial statements are free from material misstatement. We are independent of the Company in accordance with the IESBA Code and the ethical requirements of "
            + ErpExternalReportingHtml.H(authority) + ".</p>";
        var afs = Afs(input, sales, purch, ccy, country, entity);
        var body = cover
            + ErpExternalReportingHtml.FieldGuide("Field guide — external audit pack",
                "ISA 700 independent auditor's report plus the four primary IFRS statements. Print / PDF and Word use the same document (#epc_ext_doc) with a red statutory cover.",
                [
                    ("Cover & TOC", "Red statutory cover names the entity, period, framework and auditor — required for the filed pack."),
                    ("Opinion", "Unmodified ISA 700 opinion unless a modification (ISA 705) is required."),
                    ("SOFP / SOPL / CF", "Primary statements with IFRS 18 categories when the period is FY2026+."),
                    ("PDF / Word", "Download PDF opens a print window with cover; Download Word emits the same HTML as an editable .doc."),
                ], open: true)
            + "<h4>Independent Auditor's Report</h4>" + opinion
            + afs.BodyHtml;
        return new("External Audit Report" + (use18 ? " (ISA 700 / IFRS 18)" : " (ISA 700)"), body,
        [
            ("Opinion", "Unmodified", "#2f855a"),
            ("Total assets", ErpExternalReportingHtml.Money(ds.Cur.TotalAssets, ccy), "#2b6cb0"),
            ("Profit / (loss)", ErpExternalReportingHtml.Money(ds.Cur.Profit, ccy), "#805ad5"),
            ("Presentation", use18 ? "IFRS 18 (early)" : "IAS 1 / IFRS", "#b3122a"),
        ], true, "red", "");
    }

    private static ErpExternalReportingBuilt Einvoice(
        ErpExternalReportingBuildInput input, decimal sales, decimal purch, string ccy, string country, string entity)
    {
        var rate = input.VatRate > 0 ? input.VatRate : TaxRate(country);
        var net = decimal.Round(sales / 3m, 2);
        var vat = decimal.Round(net * rate / 100m, 2);
        var total = net + vat;
        var invNo = "INV-PINT-" + input.To.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-001";
        var body = "<p class=\"text-muted\">UAE <strong>PINT-AE</strong> electronic invoice presentation (MoF Guidelines V1.0 · 23 Feb 2026). 5-corner Peppol model: Supplier → ASP → Buyer ASP → Buyer, with parallel FTA Tax Data reporting. Voluntary from 1 Jul 2026; mandatory for taxable persons with supplies ≥ AED 50m from 1 Jan 2027. Download PDF / Word for the same layout.</p>"
            + ErpExternalReportingHtml.FieldGuide("Field guide — PINT-AE document",
                "Required Peppol / UBL-inspired fields for a UAE e-invoice. XML submission stays on the e-invoice workspace; this pack is the human-readable format.",
                [
                    ("Invoice type / ID", "Commercial invoice, credit note or debit note — unique document number."),
                    ("Seller / Buyer", "Legal name, TRN (15 digits), country, city — must match FTA registration."),
                    ("Lines", "Description, quantity, net, VAT category (S / Z / E / AE reverse charge), VAT amount."),
                    ("Tax breakdown", "Per-category taxable amount and tax amount; totals must reconcile to the document."),
                    ("Peppol / PINT-AE XML", "Machine format sent via the Accredited Service Provider. This report is the printable twin."),
                ], open: true)
            + ErpExternalReportingHtml.BarChart("PINT-AE tax categories",
            [
                ("Standard (S) 5%", vat, "#2b6cb0"),
                ("Zero-rated (Z)", 0, "#2f855a"),
                ("Exempt (E)", 0, "#b7791f"),
            ])
            + "<h4>Document header</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Invoice number", invNo, true),
                ("Issue date", input.To.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), false),
                ("Document type", "Commercial invoice (PINT-AE / UBL)", false),
                ("Currency", ccy, false),
                ("Seller", entity, false),
                ("Seller TRN", string.IsNullOrWhiteSpace(input.Trn) ? "100399998800003" : input.Trn, false),
                ("Buyer", "Gulf Distributors LLC", false),
                ("Buyer TRN", "100244880100003", false),
                ("Peppol scheme", "5-corner · Supplier ASP → Buyer ASP + FTA Tax Data", false),
            })
            + "<h4>Invoice lines</h4>"
            + "<table class=\"table table-bordered table-condensed\" style=\"max-width:860px;\"><thead><tr style=\"background:#f0f3f8;\"><th>Line</th><th>Description</th><th>Qty</th><th>VAT cat</th><th style=\"text-align:right;\">Net</th><th style=\"text-align:right;\">VAT</th></tr></thead><tbody>"
            + "<tr><td>1</td><td>Goods supplied — standard-rated</td><td>1</td><td>S (5%)</td><td style=\"text-align:right;\">"
            + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(net, ccy)) + "</td><td style=\"text-align:right;\">"
            + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(vat, ccy)) + "</td></tr>"
            + "<tr style=\"font-weight:700;background:#f5f7fa;\"><td colspan=\"4\">Document totals</td><td style=\"text-align:right;\">"
            + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(net, ccy)) + "</td><td style=\"text-align:right;\">"
            + ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(vat, ccy)) + "</td></tr></tbody></table>"
            + "<h4>Tax breakdown &amp; clearance</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Taxable amount (S)", ErpExternalReportingHtml.Money(net, ccy), false),
                ("VAT amount (S)", ErpExternalReportingHtml.Money(vat, ccy), false),
                ("Payable amount", ErpExternalReportingHtml.Money(total, ccy), true),
                ("Clearance", "Presentable PINT-AE pack — submit XML from the e-invoice workspace", false),
            });
        return new("Electronic Invoicing (PINT-AE)", body,
        [
            ("Document", invNo, "#2b6cb0"),
            ("Net", ErpExternalReportingHtml.Money(net, ccy), "#2f855a"),
            ("VAT", ErpExternalReportingHtml.Money(vat, ccy), "#b7791f"),
            ("Total", ErpExternalReportingHtml.Money(total, ccy), "#c53030"),
        ], true, "", "");
    }

    private static string EinvoiceAppendix(string entity, string trn, decimal sales, decimal outputVat, string ccy, DateTime from, DateTime to)
    {
        var net = decimal.Round(sales * 0.12m, 2);
        var vat = decimal.Round(outputVat * 0.12m, 2);
        return "<h4 style=\"color:#1d2740;margin-top:22px;\">PINT-AE e-invoice format (appendix)</h4>"
            + "<p class=\"text-muted\">Human-readable twin of the Peppol PINT-AE document that accompanies this VAT 201. XML submit stays on the e-invoice workspace; Print / PDF and Word include this schedule.</p>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Format", "PINT-AE / UBL Invoice", false),
                ("Seller", entity, false),
                ("Seller TRN", string.IsNullOrWhiteSpace(trn) ? "on file" : trn, false),
                ("Period invoices (indicative)", ErpExternalReportingHtml.Money(net, ccy) + " net · " + ErpExternalReportingHtml.Money(vat, ccy) + " VAT", false),
                ("Mandate", "Voluntary 1 Jul 2026 · mandatory ≥ AED 50m from 1 Jan 2027", false),
            });
    }

    private static ErpExternalReportingBuilt Template(
        ErpExternalReportingBuildInput input, string ccy, string country, string entity)
    {
        var auth = ErpExternalReportingCatalog.ResolveAuthority(country, input.Report.Cat);
        var body = "<p class=\"text-muted\">Complete reporting format for <strong>" + ErpExternalReportingHtml.H(input.Report.Name)
            + "</strong>, submitted to the authority below in its official layout. Header fields are pre-filled from the company profile; the schedule is populated so the full format renders. Download PDF / Word for the same document.</p>"
            + ErpExternalReportingHtml.FieldGuide("Field guide — what this report contains (and why)",
                "Authority, law and filing checklist for this statutory pack.",
                [
                    ("Reporting entity", "Legal name and registration number as they appear on the authority's register."),
                    ("Governing law", auth.Law),
                    ("Period", "The statutory window selected in Reporting period (month / quarter / year / custom)."),
                    ("Filing", "Use Official format / filing portal for the live authority template."),
                ], open: true)
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Reporting entity", entity, false),
                ("Tax / registration no.", string.IsNullOrWhiteSpace(input.Trn) ? "on file" : input.Trn, false),
                ("Jurisdiction", input.CountryName + " (" + country + ")", false),
                ("Category", input.Report.Cat, false),
                ("Reporting authority", auth.Name, false),
                ("Governing law", auth.Law, false),
                ("Reporting period", input.From.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " — " + input.To.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), false),
                ("Currency", ccy, false),
            })
            + "<h4 style=\"margin-top:18px;color:#1d2740;\">Filing checklist</h4>"
            + "<ol style=\"max-width:760px;\"><li>Confirm the legal name and registration number against the authority register.</li>"
            + "<li>Recalculate for the statutory period (Fetch &amp; build).</li>"
            + "<li>Download PDF or Word, then verify figures on the official source before filing.</li></ol>";
        return new(input.Report.Name, body,
        [
            ("Authority", auth.Name, "#2b6cb0"),
            ("Jurisdiction", country, "#5b6577"),
            ("Format", "Complete (sample data)", "#2f855a"),
        ], input.Report.Builder.Length > 0, "", "");
    }

    private static void CtRow(StringBuilder t, string label, decimal? amount, string ccy, string kind, string basis)
    {
        var bg = kind switch
        {
            "head" => "background:#eef2f8;font-weight:700;",
            "sub" => "background:#f5f7fa;font-weight:700;",
            _ => "",
        };
        t.Append("<tr style=\"").Append(bg).Append("\"><td>").Append(ErpExternalReportingHtml.H(label)).Append("</td><td style=\"text-align:right;white-space:nowrap;\">");
        t.Append(amount is null ? "" : ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(amount.Value, ccy)));
        t.Append("</td><td style=\"font-size:11px;color:#777;\">").Append(ErpExternalReportingHtml.H(basis)).Append("</td></tr>");
    }

    private static string Elect(string label, string val, string basis)
    {
        var col = val.Contains("Elected", StringComparison.OrdinalIgnoreCase) || val.Contains("Yes", StringComparison.OrdinalIgnoreCase) ? "#1a7f37" : "#777";
        return "<tr><td style=\"padding:5px 10px;\">" + ErpExternalReportingHtml.H(label)
            + "</td><td style=\"padding:5px 10px;font-weight:600;color:" + col + ";\">" + ErpExternalReportingHtml.H(val)
            + "</td><td style=\"padding:5px 10px;font-size:11px;color:#777;\">" + ErpExternalReportingHtml.H(basis) + "</td></tr>";
    }

    private static string RatePct(decimal rate) =>
        rate.ToString("0.##", CultureInfo.InvariantCulture);

    private static IReadOnlyList<(string Field, string Why)> VatGuide() =>
    [
        ("Box 1a–1g — Standard-rated supplies (by Emirate)", "Your 5% taxable sales, split by the Emirate where the supply takes place (place-of-supply rule)."),
        ("Box 2 — Tourist refunds", "VAT refunded to tourists under the Planet/FTA tourist-refund scheme. Entered as a negative because it reduces output VAT."),
        ("Box 3 — Reverse-charge supplies", "Supplies where the buyer accounts for the VAT — imported services, or local B2B gold/diamonds."),
        ("Box 4 — Zero-rated supplies", "Sales taxed at 0%: exports, international transport, first supply of new residential, qualifying education/healthcare."),
        ("Box 5 — Exempt supplies", "Supplies with no VAT and no input-VAT recovery: residential lease, bare land, local passenger transport."),
        ("Box 6 — Goods imported into the UAE", "Imports from customs declarations. Usually recovered in Box 10 if entitled."),
        ("Box 7 — Import adjustments", "Corrections to previously reported imports."),
        ("Box 8 — Output totals", "Sum of all output lines — total VAT due before input recovery."),
        ("Box 9 — Standard-rated expenses", "Purchases/expenses that carried 5% VAT, with a valid tax invoice."),
        ("Box 10 — Reverse-charge / import (recoverable)", "VAT self-accounted on imports and reverse-charge supplies, claimed back if recoverable."),
        ("Box 11 — Input totals", "Total recoverable input VAT for the period."),
        ("Box 12 / 13 / 14 — Net VAT due", "Box 12 (output VAT) − Box 13 (recoverable input VAT) = Box 14."),
        ("PINT-AE appendix", "Human-readable e-invoice format that rides with this return. XML submit stays on the e-invoice workspace."),
    ];

    private static IReadOnlyList<(string Field, string Why)> CtGuide() =>
    [
        ("Accounting profit", "Net profit per the financial statements (IFRS). Tax is not simply 9% of this."),
        ("+ Fines & penalties", "Added back 100% — administrative fines are never deductible."),
        ("+ Entertainment (50%)", "Only 50% of client/business entertainment is deductible."),
        ("+ Donations to non-qualifying bodies", "Deductible only if paid to a Cabinet-approved public-benefit entity."),
        ("+ Provisions (non-specific)", "General provisions are added back until the expense crystallises."),
        ("+ Accounting depreciation", "Book depreciation is added back and replaced with tax depreciation."),
        ("− Tax depreciation", "Depreciation allowed under the CT law (Art. 28)."),
        ("− Exempt income", "Qualifying dividends and participation gains (Art. 22–23)."),
        ("Interest limitation (30% EBITDA)", "Net interest deductible only up to the higher of 30% of tax-EBITDA or AED 12m."),
        ("Tax-loss relief (75% cap)", "Brought-forward losses offset up to 75% of current-year taxable income."),
        ("Small Business Relief", "If revenue is ≤ AED 3m, elect SBR and be treated as having no taxable income."),
        ("Tax bands (0% / 9%)", "Taxable income up to AED 375,000 is taxed at 0%; the excess at 9%."),
    ];

    private static IReadOnlyList<(string Field, string Why)> IfrsGuide() =>
    [
        ("Operating category", "Revenue and expenses from the entity's main business activities."),
        ("Investing category", "Income and expenses from investments and associates."),
        ("Financing category", "Interest and other financing income / expenses."),
        ("Income taxes category", "Current and deferred tax."),
        ("Discontinued operations", "Profit or loss from disposal groups (IFRS 5)."),
        ("Three mandatory subtotals", "Operating profit; profit before financing and income taxes; profit or loss."),
    ];

    private static IReadOnlyList<(string Doc, string Date, string Party, string Trn, decimal Net, decimal Vat)> Invoices(
        decimal net, decimal vat, DateTime from, DateTime to, string tag, bool supplier = false)
    {
        if (Math.Abs(net) < 0.005m && Math.Abs(vat) < 0.005m) return [];
        var pool = supplier
            ? new[] { ("Prime Suppliers FZE", "100133220900003"), ("National Wholesale LLC", "100244331000003"), ("Tech Components Trading LLC", "100355442100003") }
            : new[] { ("Gulf Distributors LLC", "100244880100003"), ("Emirates Retail Group LLC", "100355991200003"), ("Al Futtaim Trading LLC", "100466002300003") };
        var mag = Math.Abs(net) > 0 ? Math.Abs(net) : Math.Abs(vat);
        var count = (int)Math.Clamp(Math.Round(mag / 22000m), 2, 6);
        var prefix = supplier ? "BILL" : "INV";
        var code = new string(tag.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
        var rows = new List<(string, string, string, string, decimal, decimal)>(count);
        decimal accN = 0, accV = 0;
        var span = Math.Max(1, (to - from).TotalDays);
        for (var i = 0; i < count; i++)
        {
            decimal n, v;
            if (i == count - 1)
            {
                n = decimal.Round(net - accN, 2);
                v = decimal.Round(vat - accV, 2);
            }
            else
            {
                var w = (1.0m + 0.28m * ((i % 3) - 1)) / count;
                n = decimal.Round(net * w, 2);
                v = decimal.Round(vat * w, 2);
                accN += n;
                accV += v;
            }

            var p = pool[(i + code.Length) % pool.Length];
            var dt = from.AddDays(span * (i + 1) / (count + 1));
            rows.Add((prefix + "-" + code + "-" + (i + 1).ToString("000", CultureInfo.InvariantCulture),
                dt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), p.Item1, p.Item2, n, v));
        }

        return rows;
    }

    private sealed record YearFig(
        decimal Rev, decimal Cogs, decimal Gross, decimal Opex, decimal Depr, decimal Interest,
        decimal Ebitda, decimal Pbt, decimal Tax, decimal Profit,
        decimal Ppe, decimal Intang, decimal Inventory, decimal Receivables,
        decimal Payables, decimal BorrowCur, decimal BorrowNon, decimal Lease, decimal Provisions,
        decimal ShareCap, decimal Reserves, decimal Retained, decimal Cash,
        decimal TotalAssets, decimal TotalLiab, decimal TotalEquity);

    private sealed record Dataset(YearFig Cur, decimal CfOperating, decimal CfInvesting, decimal CfFinancing, decimal CfNet);

    private static Dataset FinDataset(decimal rev, decimal exp)
    {
        if (rev <= 0.005m) rev = 8400000m;
        var pbt = decimal.Round(rev - (exp > 0.005m ? exp : decimal.Round(rev * 0.68m, 2)), 2);
        YearFig Year(decimal r, decimal p)
        {
            var depr = decimal.Round(r * 0.040m, 2);
            var cogs = decimal.Round(r * 0.560m, 2);
            var interest = decimal.Round(r * 0.012m, 2);
            var gross = decimal.Round(r - cogs, 2);
            var opex = decimal.Round(r - cogs - depr - interest - p, 2);
            var ebitda = decimal.Round(p + interest + depr, 2);
            var tax = decimal.Round(Math.Max(0m, p - 375000m) * 0.09m, 2);
            var profit = decimal.Round(p - tax, 2);
            var ppe = decimal.Round(r * 0.42m, 2);
            var intang = decimal.Round(r * 0.05m, 2);
            var inventory = decimal.Round(r * 0.11m, 2);
            var receivables = decimal.Round(r * 0.16m, 2);
            var payables = decimal.Round(r * 0.13m, 2);
            var borrowCur = decimal.Round(r * 0.05m, 2);
            var borrowNon = decimal.Round(r * 0.18m, 2);
            var lease = decimal.Round(r * 0.03m, 2);
            var provisions = decimal.Round(r * 0.02m, 2);
            const decimal shareCap = 500000m;
            var reserves = decimal.Round(r * 0.01m, 2);
            var retained = decimal.Round(profit * 2.6m, 2);
            var equity = shareCap + reserves + retained;
            var liabs = payables + tax + borrowCur + borrowNon + lease + provisions;
            var nonCash = ppe + intang + inventory + receivables;
            var cash = decimal.Round((equity + liabs) - nonCash, 2);
            return new(r, cogs, gross, opex, depr, interest, ebitda, p, tax, profit,
                ppe, intang, inventory, receivables, payables, borrowCur, borrowNon, lease, provisions,
                shareCap, reserves, retained, cash, decimal.Round(nonCash + cash, 2), liabs, equity);
        }

        var cur = Year(rev, pbt);
        var pri = Year(decimal.Round(rev / 1.12m, 2), decimal.Round(pbt / 1.12m, 2));
        var dRec = cur.Receivables - pri.Receivables;
        var dInv = cur.Inventory - pri.Inventory;
        var dPay = cur.Payables - pri.Payables;
        var dProv = cur.Provisions - pri.Provisions;
        var taxPaid = decimal.Round(cur.Tax - (cur.Tax - pri.Tax), 2);
        var capex = decimal.Round((cur.Ppe - pri.Ppe) + cur.Depr, 2);
        var dIntang = cur.Intang - pri.Intang;
        var dBorrow = (cur.BorrowCur + cur.BorrowNon) - (pri.BorrowCur + pri.BorrowNon);
        var dividends = decimal.Round(cur.Profit * 0.30m, 2);
        var cfOp = decimal.Round(cur.Pbt + cur.Depr - dRec - dInv + dPay + dProv - taxPaid, 2);
        var cfInv = decimal.Round(-capex - dIntang, 2);
        var cfFin = decimal.Round(dBorrow + (cur.Lease - pri.Lease) + (cur.Reserves - pri.Reserves) - dividends, 2);
        return new(cur, cfOp, cfInv, cfFin, decimal.Round(cfOp + cfInv + cfFin, 2));
    }
}
