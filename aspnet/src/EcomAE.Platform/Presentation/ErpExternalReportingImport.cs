using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_parse_*</c> / <c>epc_ext_import_map</c> / <c>epc_ext_b_*_summary</c> twin — off-system CSV/XLSX → statutory pack.</summary>
public sealed record ErpExternalReportingImportMap(
    Dictionary<string, string> Meta,
    Dictionary<string, (decimal Amount, decimal Vat, decimal Adj)> Vat,
    Dictionary<string, decimal> Values,
    Dictionary<string, (decimal Cur, decimal Pri)> Fin);

public static class ErpExternalReportingImport
{
    private static readonly HashSet<string> CtCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACCT_PROFIT", "REVENUE", "FINES", "ENTERTAINMENT", "DONATIONS",
        "PROVISIONS", "ACCT_DEP", "TAX_DEP", "EXEMPT_INCOME", "NET_INTEREST",
        "EBITDA", "LOSSES_BF", "FTC",
    };

    public static ErpExternalReportingImportMap ParseFile(ReadOnlySpan<byte> bytes, string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        IReadOnlyList<IReadOnlyList<string>> rows = ext == "xlsx"
            ? ParseXlsx(bytes)
            : ParseCsv(Encoding.UTF8.GetString(bytes));
        return MapRows(rows);
    }

    public static ErpExternalReportingImportMap ParseCsvText(string csv) => MapRows(ParseCsv(csv));

    public static string DetectKind(ErpExternalReportingImportMap map, string preferred)
    {
        var pref = (preferred ?? "vat").Trim().ToLowerInvariant();
        if (pref is not ("vat" or "ct" or "fin")) pref = "vat";
        if (pref == "fin" && map.Fin.Count == 0 && map.Vat.Count > 0) return "vat";
        if (pref == "fin" && map.Fin.Count == 0 && map.Values.Count > 0) return "ct";
        if (pref == "vat" && map.Vat.Count == 0 && map.Fin.Count > 0) return "fin";
        if (pref == "vat" && map.Vat.Count == 0 && map.Values.Count > 0) return "ct";
        if (pref == "ct" && map.Values.Count == 0 && map.Fin.Count > 0) return "fin";
        if (pref == "ct" && map.Values.Count == 0 && map.Vat.Count > 0) return "vat";
        return pref;
    }

    public static string? ValidateKind(ErpExternalReportingImportMap map, string kind) => kind switch
    {
        "fin" when map.Fin.Count == 0 => "No financial-statement lines found. Use the IFRS Financials template (Code column: FIN_REVENUE, FIN_PPE, …).",
        "ct" when map.Values.Count == 0 => "No CT computation lines found. Use the CT template (Code column: ACCT_PROFIT, FINES, …).",
        "vat" when map.Vat.Count == 0 => "No VAT boxes found. Use the VAT template (Code column: BOX1A, BOX9, …).",
        _ => null,
    };

    public static ErpExternalReportingBuilt Build(string kind, ErpExternalReportingImportMap map, string ccy, string country)
    {
        return kind switch
        {
            "ct" => BuildCt(map, ccy),
            "fin" => BuildFin(map, ccy, country),
            _ => BuildVat(map, ccy),
        };
    }

    public static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string raw)
    {
        raw = raw ?? "";
        if (raw.Length > 0 && raw[0] == '\uFEFF') raw = raw[1..];
        var delim = raw.Count(c => c == '\t') > raw.Count(c => c == ',') ? '\t' : ',';
        var rows = new List<IReadOnlyList<string>>();
        using var reader = new StringReader(raw);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(SplitCsvLine(line, delim));
        }

        return rows;
    }

    public static IReadOnlyList<IReadOnlyList<string>> ParseXlsx(ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
        var shared = ReadSharedStrings(zip);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")
            ?? zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (sheet is null) return [];
        using var stream = sheet.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in doc.Descendants(ns + "row"))
        {
            var cells = new Dictionary<int, string>();
            var max = -1;
            foreach (var c in row.Elements(ns + "c"))
            {
                var idx = ColIndex((string?)c.Attribute("r") ?? "");
                var type = (string?)c.Attribute("t") ?? "";
                string val;
                if (type == "s")
                {
                    var si = int.TryParse((string?)c.Element(ns + "v"), out var n) ? n : -1;
                    val = si >= 0 && si < shared.Count ? shared[si] : "";
                }
                else if (type == "inlineStr")
                {
                    val = (string?)c.Element(ns + "is")?.Element(ns + "t") ?? "";
                }
                else
                {
                    val = (string?)c.Element(ns + "v") ?? "";
                }

                cells[idx] = val;
                if (idx > max) max = idx;
            }

            var line = new string[Math.Max(0, max + 1)];
            for (var i = 0; i <= max; i++) line[i] = cells.TryGetValue(i, out var v) ? v : "";
            if (line.Length > 0) rows.Add(line);
        }

        return rows;
    }

    public static ErpExternalReportingImportMap MapRows(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var vat = new Dictionary<string, (decimal, decimal, decimal)>(StringComparer.OrdinalIgnoreCase);
        var fin = new Dictionary<string, (decimal, decimal)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (r.Count == 0) continue;
            var code = (r[0] ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0 || code == "CODE") continue;
            if (code.StartsWith("META_", StringComparison.Ordinal) || code.StartsWith("DISC_", StringComparison.Ordinal))
            {
                meta[code] = Cell(r, 2).Length > 0 ? Cell(r, 2) : Cell(r, 1);
                continue;
            }

            if (code.StartsWith("FIN_", StringComparison.Ordinal))
            {
                fin[code] = (Num(Cell(r, 2)), Num(Cell(r, 3)));
                continue;
            }

            if (code.StartsWith("BOX", StringComparison.Ordinal))
            {
                vat[code] = (Num(Cell(r, 2)), Num(Cell(r, 3)), Num(Cell(r, 4)));
                continue;
            }

            if (CtCodes.Contains(code))
            {
                var cell = Cell(r, 2).Length > 0 ? Cell(r, 2) : Cell(r, 1);
                var trimmed = NormalizeNum(cell);
                if (!decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
                    && values.ContainsKey(code))
                {
                    continue;
                }

                values[code] = Num(cell);
            }
        }

        return new(meta, vat, values, fin);
    }

    public static ErpExternalReportingBuilt BuildVat(ErpExternalReportingImportMap map, string ccy)
    {
        (decimal Amount, decimal Vat, decimal Adj) G(string k) =>
            map.Vat.TryGetValue(k, out var v) ? v : (0, 0, 0);

        var emirates = new (string Code, string Box, string Desc)[]
        {
            ("BOX1A", "1a", "Standard-rated supplies — Abu Dhabi"),
            ("BOX1B", "1b", "Standard-rated supplies — Dubai"),
            ("BOX1C", "1c", "Standard-rated supplies — Sharjah"),
            ("BOX1D", "1d", "Standard-rated supplies — Ajman"),
            ("BOX1E", "1e", "Standard-rated supplies — Umm Al Quwain"),
            ("BOX1F", "1f", "Standard-rated supplies — Ras Al Khaimah"),
            ("BOX1G", "1g", "Standard-rated supplies — Fujairah"),
        };
        decimal outNet = 0, outVat = 0, outAdj = 0;
        var rows = new StringBuilder(ErpExternalReportingHtml.VatHeader());
        foreach (var em in emirates)
        {
            var d = G(em.Code);
            outNet += d.Amount; outVat += d.Vat; outAdj += d.Adj;
            rows.Append(ErpExternalReportingHtml.VatBox(em.Box, em.Desc, d.Amount, d.Vat, ccy));
        }

        var b2 = G("BOX2"); var b3 = G("BOX3"); var b4 = G("BOX4"); var b5 = G("BOX5");
        var b6 = G("BOX6"); var b7 = G("BOX7");
        rows.Append(ErpExternalReportingHtml.VatBox("2", "Tax refunds provided to tourists", b2.Amount, b2.Vat, ccy));
        rows.Append(ErpExternalReportingHtml.VatBox("3", "Supplies subject to the reverse charge", b3.Amount, b3.Vat, ccy));
        rows.Append(ErpExternalReportingHtml.VatBox("4", "Zero-rated supplies", b4.Amount, 0, ccy));
        rows.Append(ErpExternalReportingHtml.VatBox("5", "Exempt supplies", b5.Amount, 0, ccy));
        rows.Append(ErpExternalReportingHtml.VatBox("6", "Goods imported into the UAE", b6.Amount, b6.Vat, ccy));
        rows.Append(ErpExternalReportingHtml.VatBox("7", "Adjustments to goods imported", b7.Amount, b7.Vat, ccy));
        var totOutNet = outNet + b2.Amount + b3.Amount + b4.Amount + b5.Amount + b6.Amount + b7.Amount;
        var totOutVat = outVat + b2.Vat + b3.Vat + b6.Vat + b7.Vat;
        var b9 = G("BOX9"); var b10 = G("BOX10");
        var totInVat = b9.Vat + b10.Vat;
        var net = decimal.Round(totOutVat - totInVat, 2);
        var pay = net >= 0;
        var trnOk = (map.Meta.TryGetValue("META_TRN", out var trn) ? trn : "").Length > 0;
        var implied = decimal.Round(outNet * 0.05m, 2);
        var rateOk = Math.Abs(implied - outVat) <= Math.Max(1m, outNet * 0.002m);
        var body = "<div class=\"alert alert-info\" style=\"font-size:12px;\"><i class=\"fa fa-upload\"></i> Built from your <strong>uploaded file</strong> (off-system, summary figures only — no invoice detail). This is for checking / reporting other clients; it does not read or write ERP data.</div>"
            + ErpExternalReportingHtml.FieldGuide("Field guide — what goes in each VAT 201 box (and why)",
                "Plain-language explanation of every box. Governing law: Federal Decree-Law 8/2017 & Executive Regulations (FTA).",
                [
                    ("Box 1a–1g", "Standard-rated supplies by Emirate."),
                    ("Box 12 / 13 / 14", "Output VAT − input VAT = net payable / reclaimable."),
                ], open: true)
            + ErpExternalReportingHtml.BarChart("Imported VAT 201",
            [
                ("Output VAT (Box 12)", totOutVat, "#2b6cb0"),
                ("Input VAT (Box 13)", totInVat, "#2f855a"),
                ("Net Box 14", Math.Abs(net), pay ? "#c53030" : "#805ad5"),
            ])
            + "<h4 style=\"color:#1d2740;margin-top:14px;\">VAT on sales &amp; all other outputs</h4>"
            + "<div style=\"border:1px solid #e6eaf1;border-radius:4px;overflow:hidden;\">" + rows + "</div>"
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">VAT on expenses &amp; all other inputs</h4>"
            + "<div style=\"border:1px solid #e6eaf1;border-radius:4px;overflow:hidden;\">"
            + ErpExternalReportingHtml.VatHeader()
            + ErpExternalReportingHtml.VatBox("9", "Standard-rated expenses (recoverable input VAT)", b9.Amount, b9.Vat, ccy)
            + ErpExternalReportingHtml.VatBox("10", "Supplies subject to reverse charge (input VAT)", b10.Amount, b10.Vat, ccy)
            + "</div>"
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">Net VAT due</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("Box 12 — Total output tax due", ErpExternalReportingHtml.Money(totOutVat, ccy), false),
                ("Box 13 — Total recoverable input tax", ErpExternalReportingHtml.Money(totInVat, ccy), false),
                ("Box 14 — Net VAT " + (pay ? "payable" : "reclaimable"), ErpExternalReportingHtml.Money(Math.Abs(net), ccy), true),
            })
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">Compliance checks</h4>"
            + ErpExternalReportingHtml.CheckTable(new[]
            {
                (trnOk ? "ok" : "warn", trnOk ? "TRN present on the uploaded data." : "TRN missing in the uploaded file — add it before filing."),
                ("ok", "Return reconciles: Box 14 = Box 12 − Box 13."),
                (rateOk ? "ok" : "warn", rateOk
                    ? "Standard-rated output VAT ≈ 5% of net (consistent)."
                    : "Standard-rated output VAT is not ≈ 5% of net — verify the rate / scheme treatment."),
            });
        return new("VAT Return (FTA VAT 201) — imported", body,
        [
            ("Output VAT", ErpExternalReportingHtml.Money(totOutVat, ccy), "#2b6cb0"),
            ("Input VAT", ErpExternalReportingHtml.Money(totInVat, ccy), "#2f855a"),
            ("Net VAT " + (pay ? "payable" : "reclaimable"), ErpExternalReportingHtml.Money(Math.Abs(net), ccy), "#c53030"),
        ], false, "", ErpExternalReportingHtml.DocName("VAT_Return_imported", DateTime.UtcNow));
    }

    public static ErpExternalReportingBuilt BuildCt(ErpExternalReportingImportMap map, string ccy)
    {
        decimal V(string k) => map.Values.TryGetValue(k, out var v) ? v : 0m;
        var profit = V("ACCT_PROFIT");
        var revenue = V("REVENUE");
        var fines = V("FINES");
        var entTotal = V("ENTERTAINMENT");
        var entAdd = decimal.Round(entTotal * 0.5m, 2);
        var donations = V("DONATIONS");
        var provisions = V("PROVISIONS");
        var acctDep = V("ACCT_DEP");
        var taxDep = V("TAX_DEP");
        var exempt = V("EXEMPT_INCOME");
        var interest = V("NET_INTEREST");
        var lossesBf = V("LOSSES_BF");
        var ftcInput = V("FTC");
        var additions = fines + entAdd + donations + provisions + acctDep;
        var adjProfit = profit + additions - taxDep - exempt;
        var ebitda = adjProfit + interest + acctDep;
        var interestCap = Math.Max(12000000m, decimal.Round(ebitda * 0.30m, 2));
        var interestDisallowed = Math.Max(0m, decimal.Round(interest - interestCap, 2));
        var taxableBeforeLoss = Math.Max(0m, adjProfit + interestDisallowed);
        var lossCap = decimal.Round(taxableBeforeLoss * 0.75m, 2);
        var lossUsed = Math.Min(lossesBf, lossCap);
        var taxable = Math.Max(0m, taxableBeforeLoss - lossUsed);
        var sbr = revenue > 0 && revenue <= 3000000m;
        var taxableAfterSbr = sbr ? 0m : taxable;
        const decimal threshold = 375000m;
        var above = Math.Max(0m, taxableAfterSbr - threshold);
        var ct = decimal.Round(above * 0.09m, 2);
        var ftc = decimal.Round(Math.Min(ftcInput, ct), 2);
        var netCt = Math.Max(0m, decimal.Round(ct - ftc, 2));
        var t = new StringBuilder();
        t.Append("<table class=\"table table-bordered table-condensed\" style=\"font-size:12.5px;max-width:860px;\">");
        t.Append("<thead><tr style=\"background:#f0f3f8;\"><th>Computation of taxable income</th><th style=\"text-align:right;\">Amount</th><th>Basis</th></tr></thead><tbody>");
        void Row(string label, decimal? amt, string kind, string basis)
        {
            var bg = kind == "head" ? "background:#eef2f8;font-weight:700;" : kind == "sub" ? "background:#f5f7fa;font-weight:700;" : "";
            t.Append("<tr style=\"").Append(bg).Append("\"><td>").Append(ErpExternalReportingHtml.H(label))
                .Append("</td><td style=\"text-align:right;\">")
                .Append(amt is null ? "" : ErpExternalReportingHtml.H(ErpExternalReportingHtml.Money(amt.Value, ccy)))
                .Append("</td><td style=\"font-size:11px;color:#777;\">").Append(ErpExternalReportingHtml.H(basis)).Append("</td></tr>");
        }

        Row("Accounting net profit (per financials)", profit, "sub", "From uploaded data");
        Row("Add back: non-deductible & timing items", null, "head", "");
        Row("Fines & administrative penalties", fines, "add", "100% non-deductible — Art. 33");
        Row("Entertainment expenditure (50% disallowed)", entAdd, "add", "Art. 32");
        Row("Donations to non-approved bodies", donations, "add", "Art. 33");
        Row("General (non-specific) provisions", provisions, "add", "Timing");
        Row("Accounting depreciation", acctDep, "add", "Replaced by tax depreciation");
        Row("Less: deductions & exempt income", null, "head", "");
        Row("Tax depreciation / capital allowances", taxDep, "less", "Art. 28");
        Row("Exempt dividends / participation", exempt, "less", "Art. 22–23");
        Row("Adjusted profit before interest limitation", adjProfit, "sub", "");
        Row("Interest disallowed (over 30% EBITDA cap)", interestDisallowed, "add", interestDisallowed > 0 ? "Carried forward" : "Within cap");
        Row("Less: tax losses brought forward (max 75%)", lossUsed, "less", "Art. 37");
        Row("Taxable income", taxable, "sub", "");
        if (sbr) Row("Small Business Relief applied (revenue ≤ AED 3m)", null, "info", "MD 73/2023");
        Row("Taxable income subject to tax", taxableAfterSbr, "sub", "");
        t.Append("</tbody></table>");
        var trnOk = (map.Meta.TryGetValue("META_TRN", out var trn) ? trn : "").Length > 0;
        var body = "<div class=\"alert alert-info\" style=\"font-size:12px;\"><i class=\"fa fa-upload\"></i> Built from your <strong>uploaded file</strong> (off-system, summary figures only). For checking / reporting other clients; it does not read or write ERP data.</div>"
            + ErpExternalReportingHtml.BarChart("Imported CT bands",
            [
                ("0% band", Math.Min(taxableAfterSbr, threshold), "#2f855a"),
                ("9% band", above, "#b7791f"),
                ("Net CT payable", netCt, "#c53030"),
            ])
            + "<h4 style=\"color:#1d2740;margin-top:14px;\">Computation of taxable income</h4>" + t
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">Tax bands, credits &amp; net liability</h4>"
            + ErpExternalReportingHtml.KvTable(new[]
            {
                ("0% band — first AED 375,000", ErpExternalReportingHtml.Money(Math.Min(taxableAfterSbr, threshold), ccy), false),
                ("9% band — taxable income above AED 375,000", ErpExternalReportingHtml.Money(above, ccy), false),
                ("Corporate tax before credits", ErpExternalReportingHtml.Money(ct, ccy), false),
                ("Less: foreign tax credit (Art. 47)", "(" + ErpExternalReportingHtml.Money(ftc, ccy) + ")", false),
                ("Net corporate tax payable", ErpExternalReportingHtml.Money(netCt, ccy), true),
            })
            + "<h4 style=\"color:#1d2740;margin-top:18px;\">Corporate tax compliance checks</h4>"
            + ErpExternalReportingHtml.CheckTable(new[]
            {
                (trnOk ? "ok" : "warn", trnOk ? "CT TRN present on the uploaded data." : "CT TRN missing — add it before filing."),
                ("ok", fines > 0 ? "Fines & penalties added back (Art. 33)." : "No fines to add back."),
                (interestDisallowed > 0 ? "warn" : "ok", interestDisallowed > 0
                    ? "Net interest exceeds 30% EBITDA cap (Art. 30)."
                    : "Net interest within the 30% EBITDA / AED 12m cap (Art. 30)."),
                ("ok", sbr ? "Small Business Relief available (revenue ≤ AED 3m) — MD 73/2023." : "Standard 0% / 9% bands applied."),
            });
        return new("Corporate Income Tax Return — imported", body,
        [
            ("Taxable income", ErpExternalReportingHtml.Money(taxableAfterSbr, ccy), "#2b6cb0"),
            ("Rate", "0% / 9%", "#5b6577"),
            ("Net CT payable", ErpExternalReportingHtml.Money(netCt, ccy), "#c53030"),
        ], false, "", ErpExternalReportingHtml.DocName("CT_return_imported", DateTime.UtcNow));
    }

    public static ErpExternalReportingBuilt BuildFin(ErpExternalReportingImportMap map, string ccy, string country)
    {
        var entity = map.Meta.TryGetValue("META_LEGAL_NAME", out var n) && n.Length > 0 ? n : "Uploaded client";
        var toRaw = map.Meta.TryGetValue("META_PERIOD_TO", out var pto) ? pto : "";
        var to = DateTime.TryParse(toRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : new DateTime(DateTime.UtcNow.Year, 12, 31);
        var fromRaw = map.Meta.TryGetValue("META_PERIOD_FROM", out var pfrom) ? pfrom : "";
        var from = DateTime.TryParse(fromRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var pf)
            ? pf
            : new DateTime(to.Year, 1, 1);
        var sales = map.Fin.TryGetValue("FIN_REVENUE", out var rev) ? rev.Cur : 0m;
        var cogs = map.Fin.TryGetValue("FIN_COGS", out var cg) ? cg.Cur : 0m;
        var purch = cogs > 0 ? cogs : decimal.Round(sales * 0.68m, 2);
        var def = ErpExternalReportingCatalog.Find("fin__annual_financial_statements")!;
        var auditDef = ErpExternalReportingCatalog.Find("audit__external_audit_report")!;
        var afs = ErpExternalReportingBuild.Build(new(
            def, country, ErpExternalReportingCatalog.CountryName(country), ccy, entity,
            map.Meta.TryGetValue("META_TRN", out var trn) ? trn : "", from, to, "FY" + to.Year, sales, purch, 0, 0, 5, false));
        var audit = ErpExternalReportingBuild.Build(new(
            auditDef, country, ErpExternalReportingCatalog.CountryName(country), ccy, entity,
            map.Meta.TryGetValue("META_TRN", out var trn2) ? trn2 : "", from, to, "FY" + to.Year, sales, purch, 0, 0, 5, false));
        var body = "<div class=\"alert alert-info\" style=\"font-size:12px;\"><i class=\"fa fa-upload\"></i> Built from your <strong>uploaded file</strong> (off-system). IFRS 18 early-applied when the period ends in FY2026+.</div>"
            + audit.BodyHtml + afs.BodyHtml;
        return new(afs.Title + " — imported", body, afs.Summary, false, "red",
            ErpExternalReportingHtml.DocName(entity + "_IFRS_FY" + to.Year, from));
    }

    public static ErpExternalReportingBuilt BuildIntake(string entity, int year, string units, string ccy, string country)
    {
        var name = string.IsNullOrWhiteSpace(entity) ? "Client entity" : entity.Trim();
        var from = new DateTime(year, 1, 1);
        var to = new DateTime(year, 12, 31);
        var samp = ErpExternalReportingBuild.PeriodSample(from, to);
        var def = ErpExternalReportingCatalog.Find("audit__external_audit_report")!;
        var built = ErpExternalReportingBuild.Build(new(
            def, country, ErpExternalReportingCatalog.CountryName(country), ccy, name, "", from, to, "FY" + year,
            samp.Rev, samp.Exp, 0, 0, 5, false));
        var note = string.IsNullOrWhiteSpace(units)
            ? ""
            : "<div class=\"alert alert-info\" style=\"font-size:12.5px;\"><strong>Business units:</strong> "
                + ErpExternalReportingHtml.H(units) + " — consolidated for the report (IFRS 8).</div>";
        var body = "<div class=\"alert alert-success\" style=\"font-size:12px;\"><i class=\"fa fa-magic\"></i> Built from guided intake · IFRS 18-compliant. PDF figures are optional — this pack uses the trial-balance / period sample you confirmed. Off-system, not stored.</div>"
            + note + built.BodyHtml;
        return built with
        {
            Title = built.Title + " — guided intake",
            BodyHtml = body,
            Theme = "red",
            DocName = ErpExternalReportingHtml.DocName(name + "_IFRS_FY" + year, from),
        };
    }

    private static string Cell(IReadOnlyList<string> r, int i) => i < r.Count ? (r[i] ?? "") : "";

    private static string NormalizeNum(string v) =>
        (v ?? "").Trim().Replace(",", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).Replace("\u00A0", "", StringComparison.Ordinal);

    private static decimal Num(string v)
    {
        var t = NormalizeNum(v);
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0m;
    }

    private static List<string> SplitCsvLine(string line, char delim)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else sb.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == delim) { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }

        cells.Add(sb.ToString());
        return cells;
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var list = new List<string>();
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return list;
        using var s = entry.Open();
        var doc = XDocument.Load(s);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var si in doc.Descendants(ns + "si"))
        {
            var t = si.Element(ns + "t");
            if (t is not null) { list.Add((string?)t ?? ""); continue; }
            list.Add(string.Concat(si.Descendants(ns + "t").Select(x => (string?)x ?? "")));
        }

        return list;
    }

    private static int ColIndex(string cellRef)
    {
        var col = new string((cellRef ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var n = 0;
        foreach (var ch in col) n = n * 26 + (ch - 64);
        return Math.Max(0, n - 1);
    }
}
