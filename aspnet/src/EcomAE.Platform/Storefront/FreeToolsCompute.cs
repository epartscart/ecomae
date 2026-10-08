using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static EcomAE.Platform.Storefront.FreeToolsEngines;
using static EcomAE.Platform.Storefront.FreeToolsPhp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>epc_free_tools_compute($tool, $country, $in)</c> and its fourteen tool calculators. Output arrays keep
/// the PHP value types: <c>long</c> where PHP has an int, <c>double</c> where it has a float.
/// </summary>
public static class FreeToolsCompute
{
    public static readonly string[] Tools =
        ["vat", "ct", "payroll", "ifrs", "einvoice", "extreport", "customs", "insurance", "docexpiry", "valuation", "finmodel", "taxkit", "hrcompliance", "workflow"];

    public static PhpArray Compute(string tool, string country, PhpArray input, long now)
    {
        var prof = CountryProfile(country);
        return tool switch
        {
            "vat" => Vat(prof, input),
            "ct" => Ct(prof, input),
            "payroll" => Payroll(prof, input),
            "ifrs" => Ifrs(prof, input),
            "einvoice" => Einvoice(prof, input, now),
            "extreport" => ExtReport(prof, input),
            "customs" => Customs(prof, input),
            "insurance" => Insurance(prof, input, now),
            "docexpiry" => DocExpiry(prof, input, now),
            "valuation" => Valuation(prof, input),
            "finmodel" => FinModel(prof, input, now),
            "taxkit" => TaxKit(prof),
            "hrcompliance" => HrCompliance(prof, input, now),
            "workflow" => Workflow(prof, input),
            _ => new PhpArray { { "ok", false }, { "message", "Unknown tool" } },
        };
    }

    private static double R2(object? v) => Round(Float(v), 2);

    /// <summary>PHP <c>epc_free_tools_num()</c>: "1,234.50", "(500)" and "AED 10" to a float.</summary>
    public static double Num(object? v)
    {
        var s = Trim(Str(v));
        if (s.Length == 0)
        {
            return 0.0;
        }

        var neg = (s.Contains('(') && s.Contains(')')) || s.StartsWith('-');
        s = Regex.Replace(s, "[^0-9.]", string.Empty);
        var f = s.Length == 0 ? 0.0 : Float(s);
        return neg ? -f : f;
    }

    private static double OrDefault(double v, double fallback) => v == 0 ? fallback : v;

    /// <summary>PHP <c>epc_free_tools_parse_csv()</c>: rows keyed by lower-cased, trimmed headers; blank rows skipped.</summary>
    public static List<PhpArray> ParseCsvRows(string text)
    {
        text = Trim(text);
        var output = new List<PhpArray>();
        if (text.Length == 0)
        {
            return output;
        }

        var rows = ParseCsv(text);
        if (rows.Count == 0)
        {
            return output;
        }

        var header = rows[0].Select(h => Lower(Trim(h))).ToList();
        foreach (var r in rows.Skip(1))
        {
            if (r.All(v => Trim(v).Length == 0))
            {
                continue;
            }

            var assoc = new PhpArray();
            for (var i = 0; i < header.Count; i++)
            {
                if (header[i].Length == 0)
                {
                    continue;
                }

                assoc[header[i]] = i < r.Count ? Trim(r[i]) : string.Empty;
            }

            output.Add(assoc);
        }

        return output;
    }

    private static PhpArray Finding(string level, string message) => new() { { "level", level }, { "message", message } };

    private static PhpArray Row(string label, object? value) => PhpArray.List(label, value);

    private static PhpArray ListOf(IEnumerable<PhpArray> items) => PhpArray.List(items.Cast<object?>().ToArray());

    private static string Csv(PhpArray input) => Trim(Str(input["csv"] ?? string.Empty));

    private static PhpArray Vat(PhpArray prof, PhpArray input)
    {
        var rate = Float(prof["tax_rate"]);
        var label = Str(prof["tax_label"]);
        var findings = new List<PhpArray>();
        var rowCount = 0L;
        double? standardSales = null;
        double zeroSales = 0, exemptSales = 0, standardPurch = 0, importVat = 0;
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            var rows = ParseCsvRows(csv);
            if (rows.Count == 0)
            {
                findings.Add(Finding("fail", "Could not read any data rows from the CSV. Include a header row (type, category, amount, trn)."));
            }
            else
            {
                double ss = 0;
                long missingTrn = 0, negatives = 0, unknownCat = 0;
                foreach (var r in rows)
                {
                    var type = Lower(Str(r["type"] ?? string.Empty));
                    var cat = Lower(Str(r["category"] ?? "standard"));
                    var amt = Num(r["amount"] ?? r["net"] ?? r["value"] ?? 0L);
                    if (amt < 0)
                    {
                        negatives++;
                    }

                    var isPurchase = type.Contains("purchase") || type.Contains("input") || type.Contains("expense");
                    var isSale = type.Contains("sale") || type.Contains("output") || type.Contains("revenue");
                    if (!isPurchase && !isSale)
                    {
                        isSale = true;
                    }

                    if (isSale)
                    {
                        if (cat.Contains("zero"))
                        {
                            zeroSales += amt;
                        }
                        else if (cat.Contains("exempt"))
                        {
                            exemptSales += amt;
                        }
                        else
                        {
                            ss += amt;
                            if (!(cat.Contains("standard") || cat.Length == 0 || cat == "std"))
                            {
                                unknownCat++;
                            }
                        }

                        if (Trim(Str(r["trn"] ?? r["tax_reg"] ?? r["vat_no"] ?? string.Empty)).Length == 0)
                        {
                            missingTrn++;
                        }
                    }
                    else if (cat.Contains("import"))
                    {
                        importVat += amt;
                    }
                    else
                    {
                        standardPurch += amt;
                    }

                    rowCount++;
                }

                standardSales = ss;
                if (missingTrn > 0)
                {
                    findings.Add(Finding("warn", missingTrn + " sales row(s) have no tax registration number (TRN) \u2014 required on standard-rated tax invoices."));
                }

                if (negatives > 0)
                {
                    findings.Add(Finding("warn", negatives + " row(s) have a negative amount \u2014 confirm these are credit notes, not data errors."));
                }

                if (unknownCat > 0)
                {
                    findings.Add(Finding("warn", unknownCat + " row(s) had an unrecognised category and were treated as standard-rated."));
                }
            }
        }

        if (standardSales is null)
        {
            standardSales = Num(input["standard_sales"] ?? 0L);
            zeroSales = Num(input["zero_sales"] ?? 0L);
            exemptSales = Num(input["exempt_sales"] ?? 0L);
            standardPurch = Num(input["standard_purchases"] ?? 0L);
            importVat = Num(input["import_vat"] ?? 0L);
        }

        var sales = standardSales.Value;
        var outputTax = sales * rate / 100.0;
        var inputTax = (standardPurch * rate / 100.0) + importVat;
        var net = outputTax - inputTax;
        findings.Add(rate <= 0
            ? Finding("warn", "No standard VAT/GST rate is configured for this country \u2014 output tax is 0.")
            : Finding("pass", "Output and input " + label + " computed at the " + Str(rate) + "% country rate."));
        if (rowCount > 0)
        {
            findings.Add(Finding("pass", rowCount + " ledger line(s) classified and totalled."));
        }

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "label", prof["tax_label"] },
            { "rate", rate },
            {
                "rows", PhpArray.List(
                    Row("Standard-rated sales", Round(sales, 2)),
                    Row("Zero-rated sales", Round(zeroSales, 2)),
                    Row("Exempt sales", Round(exemptSales, 2)),
                    Row("Output " + label + " (" + Str(rate) + "%)", Round(outputTax, 2)),
                    Row("Standard-rated purchases", Round(standardPurch, 2)),
                    Row("Import " + label, Round(importVat, 2)),
                    Row("Recoverable input " + label, Round(inputTax, 2)))
            },
            { "net", Round(net, 2) },
            { "net_label", net >= 0 ? label + " payable" : label + " refundable" },
            { "scheme", prof["einvoice"] },
            { "compliance", ListOf(findings) },
        };
    }

    private sealed record CtTotals(double Revenue, double Expenses, double Adjustments, List<PhpArray> Findings, long Count);

    private static CtTotals CtFromCsv(List<PhpArray> rows)
    {
        double revenue = 0, expenses = 0, adjustments = 0;
        long unknown = 0, n = 0;
        foreach (var r in rows)
        {
            var type = Lower(Str(r["type"] ?? r["category"] ?? string.Empty));
            var amt = Num(r["amount"] ?? r["value"] ?? 0L);
            if (type.Contains("revenue") || type.Contains("income") || type.Contains("sales") || type.Contains("turnover"))
            {
                revenue += amt;
            }
            else if (type.Contains("adjust") || type.Contains("addback") || type.Contains("add-back") || type.Contains("disallow"))
            {
                adjustments += amt;
            }
            else if (type.Contains("expense") || type.Contains("cost") || type.Contains("opex") || type.Contains("cogs"))
            {
                expenses += amt;
            }
            else
            {
                unknown++;
            }

            n++;
        }

        var findings = new List<PhpArray>();
        if (unknown > 0)
        {
            findings.Add(Finding("warn", unknown + " row(s) had an unrecognised type and were ignored \u2014 use revenue / expense / adjustment."));
        }

        return new CtTotals(revenue, expenses, adjustments, findings, n);
    }

    private static PhpArray Ct(PhpArray prof, PhpArray input)
    {
        var cit = CitRate(Str(prof["country"]));
        var findings = new List<PhpArray>();
        var rowCount = 0L;
        double revenue, expenses, adjustments;
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            var rows = ParseCsvRows(csv);
            if (rows.Count == 0)
            {
                findings.Add(Finding("fail", "Could not read any data rows from the CSV. Include a header row (type, amount)."));
                revenue = expenses = adjustments = 0.0;
            }
            else
            {
                var agg = CtFromCsv(rows);
                revenue = agg.Revenue;
                expenses = agg.Expenses;
                adjustments = agg.Adjustments;
                findings.AddRange(agg.Findings);
                rowCount = agg.Count;
            }
        }
        else
        {
            revenue = Num(input["revenue"] ?? 0L);
            expenses = Num(input["expenses"] ?? 0L);
            adjustments = Num(input["adjustments"] ?? 0L);
        }

        var profit = revenue - expenses + adjustments;
        var taxable = Math.Max(0.0, profit);
        var threshold = cit.Threshold;
        var rate = cit.Rate;
        var reliefApplied = 0.0;
        double tax;
        if (threshold > 0 && taxable <= threshold)
        {
            tax = 0.0;
            reliefApplied = taxable;
        }
        else if (threshold > 0)
        {
            tax = (taxable - threshold) * rate / 100.0;
            reliefApplied = threshold;
        }
        else
        {
            tax = taxable * rate / 100.0;
        }

        if (profit < 0)
        {
            findings.Add(Finding("warn", "A tax loss was computed \u2014 no tax is due; the loss may be carried forward subject to local rules."));
        }

        if (reliefApplied > 0)
        {
            findings.Add(Finding("pass", "Small-business / relief band applied at 0% up to " + Str(Round(threshold, 2)) + "."));
        }

        if (rowCount > 0)
        {
            findings.Add(Finding("pass", rowCount + " ledger line(s) classified into revenue / expenses / adjustments."));
        }

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "rate", rate },
            {
                "rows", PhpArray.List(
                    Row("Revenue", Round(revenue, 2)),
                    Row("Deductible expenses", Round(expenses, 2)),
                    Row("Tax adjustments", Round(adjustments, 2)),
                    Row("Accounting / taxable profit", Round(profit, 2)),
                    Row("Relief (0%) band", Round(reliefApplied, 2)),
                    Row("Taxable above relief", Round(Math.Max(0.0, taxable - reliefApplied), 2)))
            },
            { "net", Round(tax, 2) },
            { "net_label", "Estimated corporate tax (" + Str(rate) + "%)" },
            { "note", cit.Note },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray Payroll(PhpArray prof, PhpArray input)
    {
        var basic = Float(input["basic"] ?? 0L);
        var allowances = Float(input["allowances"] ?? 0L);
        var deductions = Float(input["deductions"] ?? 0L);
        var years = Float(input["years"] ?? 0L);
        var gross = basic + allowances;
        var net = gross - deductions;
        var g = HrGratuity(Str(prof["hr_country"]), basic, years);
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            {
                "rows", PhpArray.List(
                    Row("Basic salary (monthly)", Round(basic, 2)),
                    Row("Allowances", Round(allowances, 2)),
                    Row("Gross pay", Round(gross, 2)),
                    Row("Deductions", Round(deductions, 2)),
                    Row("Net pay", Round(net, 2)),
                    Row("Years of service", Round(years, 2)),
                    Row("Gratuity days", Round(g.Days, 2)),
                    Row("End-of-service gratuity", Round(g.Amount, 2)))
            },
            { "net", Round(net, 2) },
            { "net_label", "Net monthly pay" },
            { "note", g.Notes },
        };
    }

    private static readonly string[] IfrsKeys =
        ["revenue", "cogs", "opex", "other_income", "non_current_assets", "current_assets", "equity", "non_current_liabilities", "current_liabilities"];

    private static readonly string[] IfrsCreditNatured =
        ["revenue", "other_income", "equity", "non_current_liabilities", "current_liabilities"];

    private static readonly Dictionary<string, string> IfrsAlias = new(StringComparer.Ordinal)
    {
        ["sales"] = "revenue", ["turnover"] = "revenue", ["income"] = "revenue",
        ["cost_of_sales"] = "cogs", ["costofsales"] = "cogs", ["cos"] = "cogs",
        ["expenses"] = "opex", ["operating_expenses"] = "opex", ["admin"] = "opex", ["overheads"] = "opex",
        ["other"] = "other_income", ["otherincome"] = "other_income",
        ["fixed_assets"] = "non_current_assets", ["ppe"] = "non_current_assets", ["nca"] = "non_current_assets",
        ["ca"] = "current_assets", ["receivables"] = "current_assets", ["cash"] = "current_assets", ["inventory"] = "current_assets",
        ["capital"] = "equity", ["reserves"] = "equity",
        ["ncl"] = "non_current_liabilities", ["loans"] = "non_current_liabilities",
        ["cl"] = "current_liabilities", ["payables"] = "current_liabilities",
    };

    private static PhpArray Ifrs(PhpArray prof, PhpArray input)
    {
        var findings = new List<PhpArray>();
        var rowCount = 0L;
        Dictionary<string, double>? buckets = null;
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            var rows = ParseCsvRows(csv);
            if (rows.Count == 0)
            {
                findings.Add(Finding("fail", "Could not read any data rows from the CSV. Include a header row (account, debit, credit, classification)."));
            }
            else
            {
                buckets = IfrsKeys.ToDictionary(k => k, _ => 0.0, StringComparer.Ordinal);
                double debit = 0, credit = 0;
                long unknown = 0;
                foreach (var r in rows)
                {
                    var cls = Lower(Str(r["classification"] ?? r["class"] ?? r["type"] ?? string.Empty).Replace(' ', '_').Replace('-', '_'));
                    if (IfrsAlias.TryGetValue(cls, out var alias))
                    {
                        cls = alias;
                    }

                    var dr = Num(r["debit"] ?? 0L);
                    var cr = Num(r["credit"] ?? 0L);
                    if ((r["debit"] ?? string.Empty) is "" && (r["credit"] ?? string.Empty) is "")
                    {
                        var amt = Num(r["amount"] ?? r["balance"] ?? 0L);
                        if (amt >= 0)
                        {
                            dr = amt;
                        }
                        else
                        {
                            cr = -amt;
                        }
                    }

                    debit += dr;
                    credit += cr;
                    rowCount++;
                    if (!buckets.ContainsKey(cls))
                    {
                        unknown++;
                        continue;
                    }

                    buckets[cls] += IfrsCreditNatured.Contains(cls) ? cr - dr : dr - cr;
                }

                if (unknown > 0)
                {
                    findings.Add(Finding("warn", unknown + " row(s) had no recognised classification and were skipped \u2014 add a classification column."));
                }

                var tbDiff = Round(debit - credit, 2);
                findings.Add(Math.Abs(tbDiff) < 0.01
                    ? Finding("pass", "Trial balance is in balance (total debits = total credits = " + Str(Round(debit, 2)) + ").")
                    : Finding("fail", "Trial balance is OUT by " + Str(Round(tbDiff, 2)) + " (debits " + Str(Round(debit, 2)) + " vs credits " + Str(Round(credit, 2)) + ")."));
            }
        }

        double G(string k) => buckets is not null ? buckets[k] : Num(input[k] ?? 0L);
        var revenue = G("revenue");
        var cogs = G("cogs");
        var opex = G("opex");
        var other = G("other_income");
        var grossProfit = revenue - cogs;
        var operatingProfit = grossProfit - opex + other;
        var nonCurrentAssets = G("non_current_assets");
        var currentAssets = G("current_assets");
        var equity = G("equity");
        var nonCurrentLiab = G("non_current_liabilities");
        var currentLiab = G("current_liabilities");
        var totalAssets = nonCurrentAssets + currentAssets;
        var totalEl = equity + nonCurrentLiab + currentLiab;
        var balanced = Math.Abs(totalAssets - totalEl) < 0.01;
        findings.Add(balanced
            ? Finding("pass", "Statement of financial position balances (assets = equity + liabilities).")
            : Finding("warn", "Assets (" + Str(Round(totalAssets, 2)) + ") do not equal equity + liabilities (" + Str(Round(totalEl, 2)) + ") \u2014 check inputs."));
        if (rowCount > 0)
        {
            findings.Add(Finding("pass", rowCount + " trial-balance line(s) mapped to the IFRS statements."));
        }

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "compliance", ListOf(findings) },
            {
                "income", PhpArray.List(
                    Row("Revenue", Round(revenue, 2)),
                    Row("Cost of sales", Round(-cogs, 2)),
                    Row("Gross profit", Round(grossProfit, 2)),
                    Row("Operating expenses", Round(-opex, 2)),
                    Row("Other income", Round(other, 2)),
                    Row("Operating profit", Round(operatingProfit, 2)))
            },
            {
                "balance", PhpArray.List(
                    Row("Non-current assets", Round(nonCurrentAssets, 2)),
                    Row("Current assets", Round(currentAssets, 2)),
                    Row("Total assets", Round(totalAssets, 2)),
                    Row("Equity", Round(equity, 2)),
                    Row("Non-current liabilities", Round(nonCurrentLiab, 2)),
                    Row("Current liabilities", Round(currentLiab, 2)),
                    Row("Total equity & liabilities", Round(totalEl, 2)))
            },
            { "net", Round(operatingProfit, 2) },
            { "net_label", "Operating profit" },
            { "balanced", balanced },
            { "note", balanced ? "Balance sheet balances." : "Assets do not equal equity + liabilities \u2014 check inputs." },
        };
    }

    private static PhpArray Einvoice(PhpArray prof, PhpArray input, long now)
    {
        var rate = Float(prof["tax_rate"]);
        var lines = input["lines"] as PhpArray ?? new PhpArray();
        var subtotal = 0.0;
        var rows = new List<PhpArray>();
        foreach (var item in lines.Values)
        {
            var ln = item as PhpArray ?? new PhpArray();
            var desc = Trim(Str(ln["desc"] ?? string.Empty));
            var qty = Float(ln["qty"] ?? 0L);
            var price = Float(ln["price"] ?? 0L);
            if (desc.Length == 0 && qty == 0 && price == 0)
            {
                continue;
            }

            var amt = qty * price;
            subtotal += amt;
            rows.Add(new PhpArray { { "desc", desc }, { "qty", qty }, { "price", Round(price, 2) }, { "amount", Round(amt, 2) } });
        }

        var tax = subtotal * rate / 100.0;
        var total = subtotal + tax;
        var number = Upper(Str(input["number"] ?? "INV-" + Date("Ymd", now) + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(2))));
        var uuid = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:x8}-{1:x4}-{2:x4}-{3:x4}-{4:x12}",
            RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue),
            RandomNumberGenerator.GetInt32(0, 0x10000),
            RandomNumberGenerator.GetInt32(0, 0x10000),
            RandomNumberGenerator.GetInt32(0, 0x10000),
            BitConverter.ToInt64([.. RandomNumberGenerator.GetBytes(6), 0, 0]));

        var sch = EinvoiceScheme(prof);
        var sellerTrn = Trim(Str(input["seller_trn"] ?? string.Empty));
        var buyerTrn = Trim(Str(input["buyer_trn"] ?? string.Empty));
        var endpoint = sellerTrn.Length > 0 ? sch.Eas + ":" + sellerTrn : string.Empty;
        var seller = Trim(Str(input["seller"] ?? string.Empty));

        var findings = new List<PhpArray>();
        if (rows.Count == 0)
        {
            findings.Add(Finding("fail", "No invoice lines \u2014 add at least one line item."));
        }

        if (rate > 0 && sellerTrn.Length == 0)
        {
            findings.Add(Finding("warn", "Seller tax registration number is required on a " + Str(prof["tax_label"]) + " tax invoice in " + Str(prof["name"]) + "."));
        }

        if (seller.Length == 0)
        {
            findings.Add(Finding("warn", "Seller legal name is missing."));
        }

        if (endpoint.Length > 0)
        {
            findings.Add(Finding("pass", "Network endpoint built for " + sch.Network + ": " + endpoint + "."));
        }

        findings.Add(Finding("pass", "Invoice formatted for the " + sch.Scheme + " scheme (" + Str(prof["name"]) + ")."));

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "rate", rate },
            { "label", prof["tax_label"] },
            { "scheme", sch.Scheme },
            { "network", sch.Network },
            { "endpoint", endpoint },
            { "endpoint_label", sch.EndpointLabel },
            { "number", number },
            { "uuid", uuid },
            { "seller", seller },
            { "seller_trn", sellerTrn },
            { "buyer", Trim(Str(input["buyer"] ?? string.Empty)) },
            { "buyer_trn", buyerTrn },
            { "date", Date("Y-m-d", now) },
            { "lines", ListOf(rows) },
            { "subtotal", Round(subtotal, 2) },
            { "tax", Round(tax, 2) },
            { "net", Round(total, 2) },
            { "net_label", "Invoice total" },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray ExtReport(PhpArray prof, PhpArray input)
    {
        var auth = Authority(Str(prof["country"]));
        var vat = Vat(prof, input);
        var ct = Ct(prof, input);
        var findings = new List<PhpArray>();
        findings.AddRange(((PhpArray)vat["compliance"]!).Values.Cast<PhpArray>());
        findings.AddRange(((PhpArray)ct["compliance"]!).Values.Cast<PhpArray>());
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "authority", auth.Authority },
            { "vat_return_name", auth.VatReturn },
            { "ct_return_name", auth.CtReturn },
            { "vat", vat },
            { "ct", ct },
            { "net", vat["net"] },
            { "net_label", auth.VatReturn + " net " + Str(prof["tax_label"]) },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray Customs(PhpArray prof, PhpArray input)
    {
        var country = Str(prof["country"]);
        var label = Str(prof["tax_label"]);
        var freight = Num(input["freight"] ?? 0L);
        var insurance = Num(input["insurance"] ?? 0L);
        var fx = OrDefault(Num(input["fx_rate"] ?? 1L), 1.0);
        var other = Num(input["other"] ?? 0L);
        var findings = new List<PhpArray>();
        var lines = new List<CustomsLine>();
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            long missingHs = 0, zeroVal = 0;
            foreach (var r in ParseCsvRows(csv))
            {
                var hs = Trim(Str(r["hs_code"] ?? r["hs"] ?? r["hscode"] ?? string.Empty));
                var qty = OrDefault(Num(r["qty"] ?? r["quantity"] ?? 1L), 1.0);
                var uv = Num(r["unit_value"] ?? r["value"] ?? r["price"] ?? 0L);
                if (hs.Length == 0)
                {
                    missingHs++;
                }

                if (uv <= 0)
                {
                    zeroVal++;
                }

                lines.Add(new CustomsLine(hs, qty, uv));
            }

            if (missingHs > 0)
            {
                findings.Add(Finding("warn", missingHs + " line(s) have no HS code \u2014 duty defaults to the country standard rate."));
            }

            if (zeroVal > 0)
            {
                findings.Add(Finding("warn", zeroVal + " line(s) have a zero/blank value \u2014 customs may reject undervalued goods."));
            }
        }
        else
        {
            lines.Add(new CustomsLine(
                Trim(Str(input["hs_code"] ?? string.Empty)),
                OrDefault(Num(input["qty"] ?? 1L), 1.0),
                Num(input["unit_value"] ?? 0L)));
        }

        var res = CustomsCompute(country, freight, insurance, fx, Str(input["regime"] ?? "import_for_home"), lines);
        var landed = Float(res["cif_value"]) + Float(res["duty_total"]) + (other * fx);
        findings.Add(Finding("pass", "CIF, duty and import " + label + " computed for " + Str(prof["name"]) + "."));
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "pack", CustomsPackLabel },
            {
                "rows", PhpArray.List(
                    Row("Goods value (CIF basis)", R2(res["goods_value"])),
                    Row("Freight", R2(res["freight"])),
                    Row("Insurance", R2(res["insurance"])),
                    Row("CIF value", R2(res["cif_value"])),
                    Row("Customs duty", R2(res["duty_total"])),
                    Row("Import " + label, R2(res["vat_total"])),
                    Row("Other landed costs", Round(other * fx, 2)),
                    Row("Total landed cost", Round(landed, 2)))
            },
            { "lines", res["lines"] },
            { "net", R2(res["total_payable"]) },
            { "net_label", "Duty + import " + label + " payable" },
            { "landed_cost", Round(landed, 2) },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray Insurance(PhpArray prof, PhpArray input, long now)
    {
        var recommended = InsuranceRecommendedFor(Str(prof["country"]))
            .Select(r => new PhpArray { { "class", InsuranceClassLabel(r.Class) }, { "basis", r.Basis } })
            .ToList();
        var sumInsured = Num(input["sum_insured"] ?? 0L);
        var ratePct = Num(input["rate"] ?? 0L);
        var premium = Num(input["premium"] ?? 0L);
        if (premium <= 0 && sumInsured > 0 && ratePct > 0)
        {
            premium = sumInsured * ratePct / 100.0;
        }

        var findings = new List<PhpArray>();
        var rows = new List<PhpArray>();
        if (sumInsured > 0)
        {
            rows.Add(Row("Sum insured", Round(sumInsured, 2)));
        }

        if (ratePct > 0)
        {
            rows.Add(Row("Premium rate (%)", Round(ratePct, 4)));
        }

        if (premium > 0)
        {
            rows.Add(Row("Annual premium", Round(premium, 2)));
        }

        var expiryStr = Trim(Str(input["expiry"] ?? string.Empty));
        long? daysLeft = null;
        var status = string.Empty;
        if (expiryStr.Length > 0 && StrToTime(expiryStr, now) is { } ts)
        {
            var left = DaysLeft(ts, now);
            daysLeft = left;
            status = DocStatus(ts, now);
            rows.Add(Row("Policy expiry", Date("Y-m-d", ts)));
            rows.Add(Row("Days to renewal", left));
            findings.Add(status switch
            {
                "expired" => Finding("fail", "Policy has EXPIRED \u2014 arrange cover immediately; a gap in cover is a serious risk."),
                "expiring" => Finding("warn", "Policy renews within 30 days \u2014 start the renewal now."),
                _ => Finding("pass", "Policy is valid for " + left + " more day(s)."),
            });
        }

        findings.Add(Finding("pass", recommended.Count + " compulsory/recommended cover(s) listed for " + Str(prof["name"]) + "."));
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "recommended", ListOf(recommended) },
            { "rows", ListOf(rows) },
            { "status", status },
            { "days_left", daysLeft },
            { "net", Round(premium, 2) },
            { "net_label", "Annual premium" },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray DocExpiry(PhpArray prof, PhpArray input, long now)
    {
        var defaultReminders = Trim(Str(input["reminder_days"] ?? "90,60,30,7"));
        var items = new List<(string Title, string Expiry, string Reminders)>();
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            foreach (var r in ParseCsvRows(csv))
            {
                items.Add((
                    Trim(Str(r["title"] ?? r["document"] ?? r["name"] ?? "Document")),
                    Trim(Str(r["expiry"] ?? r["expiry_date"] ?? r["expires"] ?? string.Empty)),
                    Trim(Str(r["reminder_days"] ?? defaultReminders))));
            }
        }
        else
        {
            items.Add((Trim(Str(input["title"] ?? "Document")), Trim(Str(input["expiry"] ?? string.Empty)), defaultReminders));
        }

        var rows = new List<PhpArray>();
        var findings = new List<PhpArray>();
        long expired = 0, expiring = 0;
        foreach (var it in items)
        {
            var parsed = it.Expiry.Length > 0 ? StrToTime(it.Expiry, now) : null;
            if (parsed is not { } ts)
            {
                rows.Add(new PhpArray { { "title", it.Title }, { "expiry", "\u2014" }, { "days_left", "\u2014" }, { "status", "no date" }, { "next_reminder", "\u2014" } });
                continue;
            }

            var daysLeft = DaysLeft(ts, now);
            var status = DocStatus(ts, now);
            var reminders = ParseReminderDays(it.Reminders);
            var due = DueThresholds(ts, reminders, now);
            string next;
            if (due.Count > 0)
            {
                next = "due now (" + due[0] + "d)";
            }
            else
            {
                var future = reminders.Where(d => daysLeft > d).ToList();
                next = future.Count > 0 ? "at " + future.Max() + "d" : "\u2014";
            }

            if (status == "expired")
            {
                expired++;
            }
            else if (status == "expiring")
            {
                expiring++;
            }

            rows.Add(new PhpArray { { "title", it.Title }, { "expiry", Date("Y-m-d", ts) }, { "days_left", daysLeft }, { "status", status }, { "next_reminder", next } });
        }

        if (expired > 0)
        {
            findings.Add(Finding("fail", expired + " document(s) already EXPIRED \u2014 act now."));
        }

        if (expiring > 0)
        {
            findings.Add(Finding("warn", expiring + " document(s) expiring within 30 days."));
        }

        findings.Add(Finding("pass", rows.Count + " document(s) tracked with reminder lead days " + defaultReminders + "."));
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "doc_rows", ListOf(rows) },
            { "net", (long)rows.Count },
            { "net_label", "Documents tracked" },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray Valuation(PhpArray prof, PhpArray input)
    {
        var findings = new List<PhpArray>();
        var revenue = Num(input["revenue"] ?? 0L);
        var ebitda = Num(input["ebitda"] ?? 0L);
        var csv = Csv(input);
        if (csv.Length > 0)
        {
            var agg = CtFromCsv(ParseCsvRows(csv));
            if (revenue <= 0)
            {
                revenue = agg.Revenue;
            }

            if (ebitda <= 0)
            {
                ebitda = agg.Revenue - agg.Expenses;
            }

            findings.AddRange(agg.Findings);
        }

        var netDebt = Num(input["net_debt"] ?? 0L);
        var growth = Num(input["growth"] ?? 5L);
        var discount = OrDefault(Num(input["discount"] ?? 15L), 15.0);
        var ebitdaMult = OrDefault(Num(input["ebitda_multiple"] ?? 6L), 6.0);
        var revMult = OrDefault(Num(input["revenue_multiple"] ?? 1.5), 1.5);
        var taxRate = Num(input["tax_rate"] ?? 0L);

        var fcf = ebitda * (1 - (taxRate / 100.0));
        var g = growth / 100.0;
        var d = discount / 100.0;
        var dcfEv = d > g ? fcf * (1 + g) / (d - g) : 0.0;
        if (d <= g)
        {
            findings.Add(Finding("warn", "Discount rate must exceed the growth rate for a finite DCF \u2014 DCF set to 0."));
        }

        var ebitdaEv = ebitda * ebitdaMult;
        var revEv = revenue * revMult;
        var avgEv = (dcfEv + ebitdaEv + revEv) / 3.0;
        var equity = avgEv - netDebt;
        findings.Add(ebitda <= 0
            ? Finding("warn", "EBITDA is zero/negative \u2014 multiple and DCF methods are unreliable; rely on revenue multiple.")
            : Finding("pass", "Three valuation methods computed and averaged."));
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            {
                "rows", PhpArray.List(
                    Row("DCF enterprise value", Round(dcfEv, 2)),
                    Row("EBITDA-multiple EV (" + Str(Round(ebitdaMult, 2)) + "x)", Round(ebitdaEv, 2)),
                    Row("Revenue-multiple EV (" + Str(Round(revMult, 2)) + "x)", Round(revEv, 2)),
                    Row("Average enterprise value", Round(avgEv, 2)),
                    Row("Less: net debt", Round(-netDebt, 2)),
                    Row("Equity value", Round(equity, 2)))
            },
            { "net", Round(equity, 2) },
            { "net_label", "Estimated equity value" },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray FinModel(PhpArray prof, PhpArray input, long now)
    {
        var revenue = Num(input["revenue"] ?? 0L);
        var csv = Csv(input);
        if (csv.Length > 0 && revenue <= 0)
        {
            revenue = CtFromCsv(ParseCsvRows(csv)).Revenue;
        }

        var growth = Num(input["growth"] ?? 10L) / 100.0;
        var gm = Num(input["gross_margin"] ?? 40L) / 100.0;
        var opexPct = Num(input["opex_pct"] ?? 25L) / 100.0;
        var taxRate = Num(input["tax_rate"] ?? 0L) / 100.0;
        var years = IntFromDouble(Num(input["years"] ?? 5L));
        if (years < 1)
        {
            years = 3;
        }

        if (years > 10)
        {
            years = 10;
        }

        var projection = new List<PhpArray>();
        var rev = revenue;
        var baseYear = IntFromString(Date("Y", now));
        for (var i = 0; i < years; i++)
        {
            var yrRev = i == 0 ? rev : rev * (1 + growth);
            rev = yrRev;
            var gross = yrRev * gm;
            var opexAmt = yrRev * opexPct;
            var ebitda = gross - opexAmt;
            var net = ebitda * (1 - taxRate);
            projection.Add(new PhpArray
            {
                { "year", baseYear + i },
                { "revenue", Round(yrRev, 2) },
                { "gross_profit", Round(gross, 2) },
                { "ebitda", Round(ebitda, 2) },
                { "net_profit", Round(net, 2) },
            });
        }

        var findings = new List<PhpArray>
        {
            Finding("pass", years + "-year projection built at " + Str(Round(growth * 100, 1)) + "% growth, " + Str(Round(gm * 100, 1)) + "% gross margin."),
        };
        if (revenue <= 0)
        {
            findings.Add(Finding("warn", "Base revenue is zero \u2014 enter a starting revenue (or upload a P&L CSV)."));
        }

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "projection", ListOf(projection) },
            { "net", projection.Count > 0 ? projection[^1]["net_profit"] : 0.0 },
            { "net_label", "Year " + (baseYear + years - 1) + " net profit" },
            { "compliance", ListOf(findings) },
        };
    }

    private static readonly string[] MonthNames =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    private static PhpArray TaxKit(PhpArray prof)
    {
        var country = Str(prof["country"]);
        var cit = CitRate(country);
        var auth = Authority(country);
        var sch = EinvoiceScheme(prof);
        var fyStart = Int(prof["fiscal_year_start_month"] ?? 1L);
        var label = Str(prof["tax_label"]);
        var taxRate = Str(prof["tax_rate"]);
        var hasTax = Float(prof["tax_rate"]) > 0;
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            {
                "rows_text", PhpArray.List(
                    Row("Country", Str(prof["name"]) + " (" + country + ")"),
                    Row("Currency", prof["currency"]),
                    Row(label + " standard rate", taxRate + "%"),
                    Row("Corporate tax rate", Str(cit.Rate) + "%"),
                    Row("Small-business / relief threshold", cit.Threshold > 0 ? Str(prof["currency"]) + " " + NumberFormat(cit.Threshold, 2) : "None"),
                    Row("E-invoice scheme", sch.Scheme),
                    Row("E-invoice network", sch.Network),
                    Row("Filing authority", auth.Authority),
                    Row("VAT/GST return", auth.VatReturn),
                    Row("Corporate tax return", auth.CtReturn),
                    Row("Fiscal year starts", fyStart is >= 1 and <= 12 ? MonthNames[fyStart - 1] : "January"))
            },
            { "net", prof["tax_rate"] },
            { "net_label", label + " rate (%)" },
            { "note", cit.Note },
            {
                "compliance", PhpArray.List(
                    Finding("pass", "Tax profile resolved for " + Str(prof["name"]) + " from your registered country."),
                    Finding(hasTax ? "pass" : "warn", hasTax ? label + " applies at " + taxRate + "%." : "No general " + label + " in this country."))
            },
        };
    }

    private static PhpArray HrCompliance(PhpArray prof, PhpArray input, long now)
    {
        var country = Str(prof["hr_country"] ?? prof["country"]);
        var findings = new List<PhpArray>();
        var p = HrLawProfile(country, now);
        string S(string key) => Str(p[key] ?? "\u2014");
        var name = Str(p["name"] ?? country);
        var rows = PhpArray.List(
            Row("Country", name),
            Row("Standard weekly hours", S("weekly_hours")),
            Row("Work week", S("workweek")),
            Row("Overtime", S("overtime")),
            Row("Max probation", S("probation_max_months") + " months"),
            Row("Notice period", S("notice_days") + " days"),
            Row("Annual leave", S("annual_leave_days") + " days/yr"),
            Row("Sick leave", S("sick_leave")),
            Row("Maternity", S("maternity")),
            Row("Paternity", S("paternity")),
            Row("Public holidays", S("public_holidays")),
            Row("End-of-service", S("eos")),
            Row("Wage protection", S("wage_protection")),
            Row("Statutory basis", S("authority")));
        findings.Add(Finding("pass", "Labour-law card resolved for " + name + "."));

        var basic = Num(input["basic_salary"] ?? 0L);
        var hireStr = Trim(Str(input["hire_date"] ?? string.Empty));
        var empRows = new List<PhpArray>();
        var eos = 0.0;
        if (basic > 0 && hireStr.Length > 0 && StrToTime(hireStr, now) is { } hireTs)
        {
            var chk = HrComplianceCheck(country, hireTs, basic, Num(input["leave_balance"] ?? 0L), now);
            eos = chk.EosLiability;
            var flags = chk.Flags
                .Select(f => Finding(f.Severity == "high" ? "fail" : "warn", f.Message + " (" + f.Basis + ")"))
                .ToList();
            empRows.Add(Row("Service years", Round(chk.ServiceYears, 2)));
            empRows.Add(Row("In probation", chk.InProbation ? "Yes" : "No"));
            empRows.Add(Row("End-of-service liability", Round(eos, 2)));
            findings.AddRange(flags);
            if (flags.Count == 0)
            {
                findings.Add(Finding("pass", "No labour-law compliance flags for this employee."));
            }
        }

        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            { "rows_text", rows },
            { "emp_rows", ListOf(empRows) },
            { "authority_url", Str(p["authority_url"] ?? string.Empty) },
            { "net", Round(eos, 2) },
            { "net_label", "End-of-service liability" },
            { "compliance", ListOf(findings) },
        };
    }

    private static PhpArray Workflow(PhpArray prof, PhpArray input)
    {
        var cur = Str(prof["currency"]);
        var t1 = Float(input["tier1"] ?? 5000L);
        var t2 = Float(input["tier2"] ?? 50000L);
        var a0 = Trim(Str(input["approver0"] ?? "Line manager"));
        var a1 = Trim(Str(input["approver1"] ?? "Department head"));
        var a2 = Trim(Str(input["approver2"] ?? "Finance director"));
        return new PhpArray
        {
            { "ok", true },
            { "currency", prof["currency"] },
            {
                "steps", PhpArray.List(
                    new PhpArray { { "range", "Up to " + cur + " " + NumberFormat(t1, 2) }, { "approver", a0 }, { "sla", "1 business day" } },
                    new PhpArray { { "range", cur + " " + NumberFormat(t1, 2) + " \u2013 " + NumberFormat(t2, 2) }, { "approver", a0 + " \u2192 " + a1 }, { "sla", "2 business days" } },
                    new PhpArray { { "range", "Above " + cur + " " + NumberFormat(t2, 2) }, { "approver", a0 + " \u2192 " + a1 + " \u2192 " + a2 }, { "sla", "3 business days" } })
            },
            { "net", 3L },
            { "net_label", "Approval tiers configured" },
            { "note", "Adopt this as your purchase-requisition approval matrix. The full ECOM AE platform enforces it automatically on every requisition and PO." },
        };
    }
}
