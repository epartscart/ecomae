namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>epc_hr_law_profiles_all</c> / <c>epc_hr_law_authority_url</c> twin.
/// Built-in labour-law pack — <c>law_fetch</c> reloads this catalog (no HTTP scrape).
/// </summary>
public static class ErpHrLabourLawPack
{
    public sealed record Profile(
        string Code,
        string Name,
        string Region,
        int WeeklyHours,
        string Workweek,
        string Overtime,
        int ProbationMaxMonths,
        int NoticeDays,
        int AnnualLeaveDays,
        string SickLeave,
        string Maternity,
        string Paternity,
        string PublicHolidays,
        string Eos,
        string EosModel,
        string WageProtection,
        string Authority,
        string AuthorityUrl);

    public static IReadOnlyList<Profile> All => _all;

    public static Profile Resolve(string? code)
    {
        var key = (code ?? "").Trim().ToUpperInvariant();
        if (key is "" or "GENERIC") return ByCode("generic");
        return _all.FirstOrDefault(p => p.Code == key) ?? ByCode("generic");
    }

    public static Profile ByCode(string code) =>
        _all.First(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string AuthorityUrl(string? code)
    {
        var key = (code ?? "").Trim().ToUpperInvariant();
        return _urls.TryGetValue(key, out var url) ? url : "https://www.ilo.org/global/standards";
    }

    private static readonly Dictionary<string, string> _urls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = "https://www.mohre.gov.ae",
        ["SA"] = "https://www.hrsd.gov.sa",
        ["QA"] = "https://www.adlsa.gov.qa",
        ["OM"] = "https://www.mol.gov.om",
        ["BH"] = "https://www.mlsd.gov.bh",
        ["KW"] = "https://www.pam.gov.kw",
        ["IN"] = "https://labour.gov.in",
        ["PK"] = "https://ophrd.gov.pk",
        ["BD"] = "https://mole.gov.bd",
        ["LK"] = "https://labourdept.gov.lk",
        ["NP"] = "https://www.dol.gov.np",
        ["EG"] = "https://www.manpower.gov.eg",
        ["JO"] = "https://mol.gov.jo",
        ["LB"] = "https://www.labor.gov.lb",
        ["MA"] = "https://www.travail.gov.ma",
        ["TR"] = "https://www.csgb.gov.tr",
        ["GB"] = "https://www.gov.uk/browse/working",
        ["DE"] = "https://www.bmas.de",
        ["FR"] = "https://travail-emploi.gouv.fr",
        ["NL"] = "https://business.gov.nl/regulation/employment-law/",
        ["IE"] = "https://www.workplacerelations.ie",
        ["US"] = "https://www.dol.gov",
        ["CA"] = "https://www.canada.ca/en/services/jobs/workplace.html",
        ["PH"] = "https://www.dole.gov.ph",
        ["SG"] = "https://www.mom.gov.sg",
        ["MY"] = "https://www.mohr.gov.my",
        ["AU"] = "https://www.fairwork.gov.au",
        ["ZA"] = "https://www.labour.gov.za",
        ["NG"] = "https://labour.gov.ng",
        ["KE"] = "https://www.labour.go.ke",
    };

    private static readonly Profile[] _all =
    [
        new("AE", "United Arab Emirates", "GCC", 48, "Up to 8h/day · 48h/week (Ramadan −2h/day)", "Art. 19: basic+≥25% normal · basic+≥50% night / rest day", 6, 30, 30, "Art. 31: 90 days/yr (15 full · 30 half · 45 unpaid) after 3 months", "Art. 30: 60 days (45 full + 15 half)", "Art. 32: 5 working days parental leave", "~14 days (Cabinet-declared)", "Arts. 51–52: 21 days basic/yr first 5 yrs, 30 days/yr beyond; after 1 yr; capped at 2 yrs’ basic wage", "AE", "WPS mandatory (MOHRE) — MR 598/2022 → MR 340/2026 from 1 Jun 2026", "MOHRE — Federal Decree-Law 33/2021 (as amended)", "https://www.mohre.gov.ae"),
        new("SA", "Saudi Arabia", "GCC", 48, "Sun–Thu (Fri/Sat weekend); 36h Ramadan", "150% (basic + 50%)", 3, 60, 21, "120 days (30 full · 60 at 75% · 30 unpaid)", "10 weeks", "3 days", "~4 official", "Half month/yr first 5 yrs, full month/yr after; resignation factor (art.85)", "SA", "WPS / Mudad mandatory", "MHRSD — Saudi Labor Law", "https://www.hrsd.gov.sa"),
        new("QA", "Qatar", "GCC", 48, "Sun–Thu; 36h Ramadan", "125% · 150% night", 6, 30, 21, "2 weeks full + 4 weeks half", "50 days", "—", "~3 official", "Min 3 weeks (21 days) basic per year, after 1 year", "QA", "WPS mandatory", "MOL — Law 14/2004", "https://www.adlsa.gov.qa"),
        new("OM", "Oman", "GCC", 45, "Sun–Thu", "125% · 150% night / rest day", 3, 30, 30, "Up to 10 weeks (graded)", "98 days", "7 days", "~9 days", "15 days/yr first 3 yrs, 30 days/yr after (non-Omanis)", "OM", "WPS (Oman) mandatory", "MOL — Labour Law 53/2023", "https://www.mol.gov.om"),
        new("BH", "Bahrain", "GCC", 48, "Sun–Thu; 6h Ramadan", "125% · 150% night / rest day", 3, 30, 30, "55 days (15 full · 20 half · 20 unpaid)", "60 days paid + 15 unpaid", "1 day", "~10 days", "15 days/yr first 3 yrs, 30 days/yr after (expats)", "BH", "WPS (Bahrain) mandatory", "MOL — Law 36/2012", "https://www.mlsd.gov.bh"),
        new("KW", "Kuwait", "GCC", 48, "Sun–Thu", "125% · 150% rest day", 3, 90, 30, "75 days (graded)", "70 days", "—", "~13 days", "15 days/yr first 5 yrs, 1 month/yr after", "KW", "WPS mandatory", "PAM — Law 6/2010", "https://www.pam.gov.kw"),
        new("IN", "India", "South Asia", 48, "Mon–Sat", "200% (twice ordinary wage)", 6, 30, 18, "State-dependent (~12 days)", "26 weeks (Maternity Benefit Act)", "—", "State-dependent", "Payment of Gratuity Act: (15/26) × wage × yrs, after 5 yrs; cap ₹2,000,000", "IN", "Wages paid via bank (Code on Wages)", "Central Labour Codes + State Shops Acts", "https://labour.gov.in"),
        new("PK", "Pakistan", "South Asia", 48, "Mon–Sat", "200%", 3, 30, 14, "16 days (8 at full pay)", "12 weeks", "—", "~13 days", "Gratuity 30 days wage per completed year, or EOBI pension", "PK", "Provincial wage rules", "Provincial Standing Orders / Shops Acts", "https://ophrd.gov.pk"),
        new("GB", "United Kingdom", "Europe", 48, "Mon–Fri (48h avg, opt-out)", "No statutory premium (contractual)", 6, 7, 28, "SSP up to 28 weeks", "52 weeks (39 paid)", "2 weeks", "8 bank holidays", "Statutory redundancy pay by age & tenure (≥2 yrs)", "severance", "PAYE / NMW enforced", "Employment Rights Act / ACAS", "https://www.gov.uk/browse/working"),
        new("US", "United States", "Americas", 40, "Mon–Fri (FLSA)", "150% over 40h/week (non-exempt)", 0, 0, 0, "No federal mandate (state/city vary)", "FMLA 12 weeks unpaid", "FMLA 12 weeks unpaid", "~11 federal", "At-will: no statutory severance; WARN Act for mass layoffs", "none", "FLSA minimum wage", "FLSA / DOL (+ state law)", "https://www.dol.gov"),
        new("SG", "Singapore", "APAC", 44, "Mon–Fri/Sat", "150% (≤ S$2,600 / workmen)", 6, 30, 7, "14 outpatient / 60 hospitalisation", "16 weeks", "4 weeks", "11 days", "No statutory severance; retrenchment by contract/norm", "none", "CPF contributions", "Employment Act / MOM", "https://www.mom.gov.sg"),
        new("generic", "Generic / other", "Other", 48, "Mon–Fri", "125% (configurable)", 3, 30, 21, "Configurable", "Configurable", "Configurable", "Configurable", "Configurable end-of-service (default 30 days/yr after 1 yr)", "generic", "Configurable", "Local labour law (configure)", "https://www.ilo.org/global/standards"),
    ];
}
