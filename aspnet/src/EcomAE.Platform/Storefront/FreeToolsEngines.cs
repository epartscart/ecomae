using static EcomAE.Platform.Storefront.FreeToolsPhp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The pure ERP engines the PHP free tools reuse: <c>epc_country_profile</c>, the labour-law engine
/// (<c>epc_hr_law_profile</c>, <c>epc_hr_gratuity</c>, <c>epc_hr_compliance_check</c>), the CSV parser
/// <c>epc_dataio_parse_csv</c>, the document-expiry rules (<c>epc_docx_*</c>), <c>epc_cust_compute</c> and the
/// insurance cover lists. Tables live in <c>FreeToolsTables.g.cs</c>, generated from the PHP functions.
/// </summary>
public static partial class FreeToolsEngines
{
    /// <summary>PHP <c>epc_country_profile($country)</c> with <c>hr_country</c> from the labour-law packs.</summary>
    public static PhpArray CountryProfile(string country)
    {
        var c = Upper(Trim(country));
        var profile = new PhpArray();
        if (CountryProfiles.TryGetValue(c, out var pack))
        {
            foreach (var (k, v) in pack)
            {
                profile[Str(k)] = v;
            }

            profile["hr_country"] = HrResolveCountry(c);
            return profile;
        }

        profile["country"] = c.Length > 0 ? c : "XX";
        profile["name"] = "Generic";
        profile["currency"] = "USD";
        profile["language"] = "en";
        profile["dir"] = "ltr";
        profile["tax_label"] = "VAT";
        profile["tax_rate"] = 0.0;
        profile["einvoice"] = string.Empty;
        profile["fiscal_year_start_month"] = 1L;
        profile["date_format"] = "Y-m-d";
        profile["hr_country"] = "generic";
        return profile;
    }

    public static (double Rate, double Threshold, string Note) CitRate(string country)
        => CitRates.TryGetValue(Upper(Trim(country)), out var r) ? r : CitFallback;

    public static (string Authority, string VatReturn, string CtReturn) Authority(string country)
        => Authorities.TryGetValue(Upper(Trim(country)), out var a) ? a : AuthorityFallback;

    /// <summary>PHP <c>epc_free_tools_einvoice_scheme($prof)</c>.</summary>
    public static (string Scheme, string Network, string Eas, string EndpointLabel) EinvoiceScheme(PhpArray prof)
    {
        var c = Upper(Str(prof["country"]));
        var schemeName = Str(prof["einvoice"]);
        (string, string, string, string)? known = c switch
        {
            "AE" => ("FTA (PINT-AE)", "Peppol (PINT-AE)", "0235", "Tax Registration Number (TRN)"),
            "SA" => ("ZATCA (Fatoora)", "ZATCA clearance/reporting", "SA:VAT", "VAT registration number"),
            "GB" => ("HMRC / Peppol", "Peppol (BIS Billing 3.0)", "0209", "GLN / company number"),
            "IN" => ("GST IRP (e-Invoice)", "GSTN IRP (IRN + QR)", "IN:GSTIN", "GSTIN"),
            "EG" => ("ETA e-invoice", "Egypt ETA platform", "EG:TIN", "Tax ID"),
            _ => null,
        };
        if (known is { } m)
        {
            return (schemeName.Length > 0 ? schemeName : m.Item1, m.Item2, m.Item3, m.Item4);
        }

        var peppolEas = c switch
        {
            "IT" => "0211", "DE" => "0204", "FR" => "0009", "NL" => "0106", "BE" => "0208", "ES" => "0210", "NO" => "0192",
            "SE" => "0007", "DK" => "0184", "FI" => "0216", "AU" => "0151", "NZ" => "0088", "SG" => "0195",
            _ => null,
        };
        if (peppolEas is not null)
        {
            return (schemeName.Length > 0 ? schemeName : "Peppol BIS Billing 3.0", "Peppol (BIS Billing 3.0)", peppolEas, "VAT / company identifier");
        }

        return (schemeName.Length > 0 ? schemeName : "No mandated e-invoice scheme for this country", "Peppol (generic)", "0088", "Tax / company identifier");
    }

    /// <summary>PHP <c>epc_dataio_parse_csv($raw)</c>: quotes, embedded commas and newlines, doubled quotes.</summary>
    public static List<List<string>> ParseCsv(string raw)
    {
        raw = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var rows = new List<List<string>>();
        var field = new System.Text.StringBuilder();
        var row = new List<string>();
        var inQuotes = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\n')
            {
                row.Add(field.ToString());
                rows.Add(row);
                row = [];
                field.Clear();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>PHP <c>epc_docx_parse_reminder_days()</c>: positive ints, de-duplicated, descending.</summary>
    public static List<long> ParseReminderDays(string csv)
    {
        var seen = new List<long>();
        foreach (var part in csv.Split(','))
        {
            var n = IntFromString(Trim(part));
            if (n > 0 && !seen.Contains(n))
            {
                seen.Add(n);
            }
        }

        seen.Sort((a, b) => b.CompareTo(a));
        return seen;
    }

    /// <summary>PHP <c>epc_docx_days_left($expiry)</c>.</summary>
    public static long DaysLeft(long expiry, long now)
        => expiry <= 0 ? 0 : (long)Math.Floor((expiry - now) / 86400.0);

    /// <summary>PHP <c>epc_docx_status($expiry)</c> (30-day window).</summary>
    public static string DocStatus(long expiry, long now)
    {
        if (expiry <= 0)
        {
            return "none";
        }

        var left = DaysLeft(expiry, now);
        return left < 0 ? "expired" : left <= 30 ? "expiring" : "valid";
    }

    /// <summary>PHP <c>epc_docx_due_thresholds($expiry, $days)</c> with nothing sent yet: ascending.</summary>
    public static List<long> DueThresholds(long expiry, IReadOnlyList<long> reminderDays, long now)
    {
        if (expiry <= 0)
        {
            return [];
        }

        var left = DaysLeft(expiry, now);
        var due = new List<long>();
        foreach (var d in reminderDays)
        {
            if (d > 0 && left <= d && !due.Contains(d))
            {
                due.Add(d);
            }
        }

        due.Sort();
        return due;
    }

    private static readonly (string Prefix, double Rate)[] AeDutyOverrides =
        [("2402", 100.0), ("2203", 50.0), ("2204", 50.0), ("3004", 0.0), ("1006", 0.0), ("0401", 0.0)];

    public const string CustomsPackLabel = "United Arab Emirates (Dubai Customs / Mirsal 2)";

    /// <summary>PHP <c>epc_cust_duty_rate()</c>: longest HS prefix of the (only) AE pack, else 5%.</summary>
    public static double DutyRate(string hsCode)
    {
        double? best = null;
        var bestLen = -1;
        foreach (var (prefix, rate) in AeDutyOverrides)
        {
            if (hsCode.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > bestLen)
            {
                best = rate;
                bestLen = prefix.Length;
            }
        }

        return best ?? 5.0;
    }

    public sealed record CustomsLine(string HsCode, double Qty, double UnitValue);

    /// <summary>PHP <c>epc_cust_compute($hdr, $lines)</c>; every country uses the AE pack, as in PHP.</summary>
    public static PhpArray CustomsCompute(string country, double freightIn, double insuranceIn, double fx, string regime, IReadOnlyList<CustomsLine> lines)
    {
        var freight = Round(freightIn * fx, 2);
        var insurance = Round(insuranceIn * fx, 2);
        var goods = 0.0;
        var computed = new List<PhpArray>();
        foreach (var ln in lines)
        {
            var lv = Round(ln.Qty * ln.UnitValue * fx, 2);
            goods = Round(goods + lv, 2);
            computed.Add(new PhpArray
            {
                { "hs_code", ln.HsCode },
                { "qty", ln.Qty },
                { "unit_value", ln.UnitValue },
                { "line_value", lv },
            });
        }

        var cif = Round(goods + freight + insurance, 2);
        var vatApplies = regime == "import_for_home";
        double dutyTotal = 0, vatTotal = 0, allocated = 0;
        var n = computed.Count;
        for (var i = 0; i < n; i++)
        {
            var ln = computed[i];
            var lineValue = (double)ln["line_value"]!;
            var share = goods > 0 ? lineValue / goods : 1.0 / Math.Max(1, n);
            var lineCif = i == n - 1 ? Round(cif - allocated, 2) : Round(cif * share, 2);
            allocated = Round(allocated + lineCif, 2);
            var rate = DutyRate(Str(ln["hs_code"]));
            var duty = Round(lineCif * rate / 100, 2);
            var vat = vatApplies ? Round((lineCif + duty) * 5.0 / 100, 2) : 0.0;
            ln["line_cif"] = lineCif;
            ln["duty_rate"] = rate;
            ln["duty_amount"] = duty;
            ln["vat_amount"] = vat;
            dutyTotal = Round(dutyTotal + duty, 2);
            vatTotal = Round(vatTotal + vat, 2);
        }

        return new PhpArray
        {
            { "country", country },
            { "regime", regime },
            { "goods_value", goods },
            { "freight", freight },
            { "insurance", insurance },
            { "cif_value", cif },
            { "duty_total", dutyTotal },
            { "vat_total", vatTotal },
            { "total_payable", Round(dutyTotal + vatTotal, 2) },
            { "lines", PhpArray.List(computed.Cast<object?>().ToArray()) },
        };
    }

    public static string InsuranceClassLabel(string cls)
    {
        if (InsuranceClasses.TryGetValue(cls, out var label))
        {
            return label;
        }

        var spaced = cls.Replace('_', ' ');
        return spaced.Length > 0 ? Upper(spaced[..1]) + spaced[1..] : spaced;
    }

    public static IReadOnlyList<(string Class, string Basis)> InsuranceRecommendedFor(string country)
        => InsuranceRecommended.TryGetValue(Upper(Trim(country)), out var list) ? list : InsuranceRecommendedFallback;

    /// <summary>PHP <c>epc_hr_resolve_country()</c>.</summary>
    public static string HrResolveCountry(string country)
    {
        var c = Upper(Trim(country));
        if (c.Length == 0 || c == "GENERIC")
        {
            return "generic";
        }

        return HrProfiles.ContainsKey(c) && c != "generic" ? c : "generic";
    }

    public sealed record Gratuity(bool Eligible, double Days, double Amount, string Notes);

    /// <summary>PHP <c>epc_hr_gratuity($country, $basic, $years)</c> (termination, 30-day month).</summary>
    public static Gratuity HrGratuity(string country, double basic, double years)
    {
        country = HrResolveCountry(country);
        var daily = basic / 30;
        switch (country)
        {
            case "AE":
            {
                if (years < 1.0)
                {
                    return new Gratuity(false, 0, 0, "Under 1 year: no gratuity (UAE).");
                }

                var days = (Math.Min(years, 5.0) * 21.0) + (Math.Max(years - 5.0, 0.0) * 30.0);
                var amount = days * daily;
                var cap = 24.0 * basic;
                if (amount > cap)
                {
                    amount = cap;
                }

                return new Gratuity(true, Round(days, 2), Round(amount, 2),
                    "UAE Federal Decree-Law 33/2021 arts. 51\u201352 (as amended): 21 days basic/yr first 5 yrs, 30 days/yr beyond; capped at 2 years\u2019 basic wage; accrued after 1 year continuous service.");
            }

            case "SA":
            {
                var award = ((Math.Min(years, 5.0) * 0.5) + (Math.Max(years - 5.0, 0.0) * 1.0)) * basic;
                return new Gratuity(award > 0, Round((Math.Min(years, 5.0) * 15.0) + (Math.Max(years - 5.0, 0.0) * 30.0), 2), Round(award, 2),
                    "KSA Labor Law art.84/85: half month first 5 yrs, full month after; resignation factor applied.");
            }

            case "IN":
            {
                if (years < 5.0)
                {
                    return new Gratuity(false, 0, 0, "India: eligible only after 5 years continuous service.");
                }

                var counted = Math.Floor(years) + ((years - Math.Floor(years)) * 12.0 >= 6.0 ? 1 : 0);
                var amount = 15.0 / 26.0 * basic * counted;
                if (amount > 2000000.0)
                {
                    amount = 2000000.0;
                }

                return new Gratuity(true, Round(15.0 / 26.0 * 30.0 * counted, 2), Round(amount, 2),
                    "India Payment of Gratuity Act: (15/26) x last wage x years (>=6 months rounds up); cap 2,000,000.");
            }

            case "QA":
                return years < 1.0
                    ? new Gratuity(false, 0, 0, "Qatar: eligible after 1 year.")
                    : new Gratuity(true, Round(years * 21.0, 2), Round(years * 21.0 * daily, 2), "Qatar: minimum 3 weeks (21 days) basic wage per year.");

            case "PK":
                return years < 1.0
                    ? new Gratuity(false, 0, 0, "Pakistan: eligible after 1 year.")
                    : new Gratuity(true, Round(years * 30.0, 2), Round(years * 30.0 * daily, 2), "Pakistan Standing Orders: 30 days wage per completed year of service.");

            case "OM":
            case "BH":
            case "KW":
            {
                if (years < 1.0)
                {
                    return new Gratuity(false, 0, 0, "GCC: eligible after 1 year.");
                }

                var days = (Math.Min(years, 3.0) * 15.0) + (Math.Max(years - 3.0, 0.0) * 30.0);
                return new Gratuity(true, Round(days, 2), Round(days * daily, 2), "GCC end-of-service: 15 days/yr first 3 yrs, 30 days/yr after.");
            }

            default:
                return years < 1.0
                    ? new Gratuity(false, 0, 0, "Generic: eligible after 1 year.")
                    : new Gratuity(true, Round(years * 30.0, 2), Round(years * 30.0 * daily, 2), "Generic configurable end-of-service (30 days/year).");
        }
    }

    /// <summary>PHP <c>epc_hr_law_profile($country, $asOf)</c>.</summary>
    public static PhpArray HrLawProfile(string country, long asOf)
    {
        var c = Upper(Trim(country));
        var known = c != "GENERIC" && HrProfiles.ContainsKey(c) && c != "generic";
        var code = known ? c : (c.Length > 0 ? c : "XX");
        var profile = new PhpArray
        {
            { "country", code },
            { "authority_url", HrAuthorityUrls.TryGetValue(code, out var url) ? url : HrAuthorityFallback },
            { "as_of", asOf },
        };
        foreach (var (k, v) in HrProfiles[known ? c : "generic"])
        {
            if (!profile.Has(Str(k)))
            {
                profile[Str(k)] = v;
            }
        }

        if (code == "AE")
        {
            PhpArray? version = null;
            foreach (var (from, overlay) in AeVersionOverlays)
            {
                if (asOf >= LocalUnixOf(from))
                {
                    version = overlay;
                }
            }

            if (version is not null)
            {
                foreach (var (k, v) in version)
                {
                    profile[Str(k)] = v;
                }
            }

            if (asOf >= LocalUnix(2026, 1, 1) && Float(profile["emirati_min_wage"]) < 6000.0)
            {
                profile["emirati_min_wage"] = 6000.0;
            }

            profile["law_effective_from"] = "2022-02-02";
            profile["pack_refreshed"] = "2026-07";
        }

        return profile;
    }

    private static long LocalUnixOf(string ymdHis)
        => LocalUnix(
            int.Parse(ymdHis[..4], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(ymdHis[5..7], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(ymdHis[8..10], System.Globalization.CultureInfo.InvariantCulture));

    private static long ProbationNoticeFromPolicy(string country) => HrResolveCountry(country) == "AE" ? 14 : 0;

    public sealed record ComplianceFlag(string Severity, string Message, string Basis);

    public sealed record ComplianceResult(IReadOnlyList<ComplianceFlag> Flags, double ServiceYears, bool InProbation, double EosLiability);

    /// <summary>PHP <c>epc_hr_compliance_check($country, $emp)</c> for one employee as of <paramref name="asOf"/>.</summary>
    public static ComplianceResult HrComplianceCheck(string country, long hire, double basic, double leaveBalance, long asOf)
    {
        var prof = HrLawProfile(country, asOf);
        var years = hire > 0 && asOf > hire ? (asOf - hire) / (365.25 * 86400) : 0.0;
        var flags = new List<ComplianceFlag>();
        var cc = HrResolveCountry(country);
        var authority = Str(prof["authority"]);

        if (hire <= 0)
        {
            flags.Add(new ComplianceFlag("error", "No hire date on record \u2014 statutory entitlements cannot be computed.", "Record completeness"));
        }

        if (basic <= 0)
        {
            flags.Add(new ComplianceFlag("warn", "No basic salary recorded \u2014 gratuity / leave-salary cannot be computed.", "Record completeness"));
        }

        var probMonths = Int(prof["probation_max_months"]);
        var probNotice = prof.Has("probation_notice_days") ? Int(prof["probation_notice_days"]) : ProbationNoticeFromPolicy(country);
        var inProbation = false;
        if (hire > 0 && probMonths > 0)
        {
            var probEnds = AddMonths(hire, (int)probMonths);
            if (asOf < probEnds)
            {
                inProbation = true;
                var daysLeft = (long)Math.Ceiling((probEnds - asOf) / 86400.0);
                var sev = daysLeft <= Math.Max(14, probNotice) ? "warn" : "info";
                var msg = "In probation \u2014 ends " + Date("d M Y", probEnds) + " (" + daysLeft + " day" + (daysLeft == 1 ? string.Empty : "s")
                          + " left). Confirm or act before the statutory cap.";
                if (probNotice > 0)
                {
                    msg += " Employer termination during probation requires \u2265" + probNotice + " days\u2019 written notice.";
                }

                flags.Add(new ComplianceFlag(sev, msg,
                    "Max probation " + probMonths + " months" + (probNotice > 0 ? "; probation notice " + probNotice + " days" : string.Empty) + " (" + authority + ")"));
            }
        }

        if (cc == "AE")
        {
            var wpsRes = Str(prof["wps_resolution"]);
            if (wpsRes.Length > 0)
            {
                flags.Add(new ComplianceFlag("info",
                    "WPS \u2014 due " + Str(prof["wps_due"]) + "; \u2265" + Int(prof["wps_threshold_pct"]) + "% of wages on time via SIF.", wpsRes));
            }
        }

        var eosModel = prof.Has("eos_model") ? Str(prof["eos_model"]) : "generic";
        var eosEligible = false;
        var eosLiability = 0.0;
        if (basic > 0 && years > 0)
        {
            if (eosModel is "AE" or "SA" or "QA" or "OM" or "BH" or "KW" or "IN" or "PK")
            {
                var g = HrGratuity(eosModel, basic, years);
                eosEligible = g.Eligible;
                eosLiability = g.Amount;
            }
            else if (eosModel is "gratuity_generic" or "generic")
            {
                var g = HrGratuity("generic", basic, years);
                eosEligible = g.Eligible;
                eosLiability = g.Amount;
            }
        }

        if (eosEligible && eosLiability > 0)
        {
            flags.Add(new ComplianceFlag("info", "End-of-service liability accrued \u2014 ensure it is provisioned in the accounts.", Str(prof["eos"])));
        }

        var annual = Float(prof["annual_leave_days"]);
        if (annual > 0 && leaveBalance > annual * 1.5)
        {
            flags.Add(new ComplianceFlag("warn",
                "Leave balance (" + NumberFormat(leaveBalance, 1) + " d) exceeds 1.5\u00d7 the annual entitlement (" + NumberFormat(annual, 0)
                + " d) \u2014 plan leave or encashment to respect carry-over limits.",
                "Statutory annual leave " + NumberFormat(annual, 0) + " days"));
        }

        if (flags.Count == 0)
        {
            flags.Add(new ComplianceFlag("ok", "No statutory issues detected.", authority));
        }

        return new ComplianceResult(flags, Round(years, 2), inProbation, Round(eosLiability, 2));
    }
}
