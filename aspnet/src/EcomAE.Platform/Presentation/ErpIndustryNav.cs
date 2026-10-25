using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP parity for <c>epc_erp_nav_filter_by_industry</c> + jewellery company detection
/// (<c>epc_jw_is_jewellery_tenant</c> / per-company <c>industry_pack</c>).
/// Jewellery tabs (jw_*, gold_rate, jewellery_tag, …) show only for jewellery companies;
/// MAIN / non-jewellery companies keep shared modules only.
/// </summary>
public static class ErpIndustryNav
{
    public sealed record NavVisibility(bool Visible, string Code, string Detail);

    public sealed record NavInspectionRow(
        string PlacementId,
        string AreaId,
        string TabKey,
        string Label,
        bool Visible,
        string Code,
        string Detail);

    public sealed record ErpNavAudience(
        string TenantVersion,
        string? IndustryCode,
        string? CountryCode,
        bool IsSuperErp,
        IReadOnlySet<string>? DisabledTabIds = null,
        IReadOnlySet<string>? EnabledModuleIds = null,
        ErpCompanyDigest? Company = null);

    private static readonly HashSet<string> JewelleryTabIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "gold_rate", "jewellery_tag", "gold_scheme",
    };

    /// <summary>
    /// The generated catalog still marks these as jewellery because the old pack lived there.
    /// They are shared duties, so company 1 and every other company keep the top-menu link.
    /// </summary>
    private static readonly HashSet<string> SharedTabIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "aml_compliance",
    };

    public static bool IsJewelleryCompany(ErpCompanyDigest? company)
    {
        if (company is null)
        {
            return false;
        }

        var pack = (company.IndustryPack ?? string.Empty).Trim().ToLowerInvariant();
        if (pack.StartsWith("jewellery", StringComparison.Ordinal)
            || pack.Contains("jewel", StringComparison.Ordinal))
        {
            return true;
        }

        var code = (company.Code ?? string.Empty).Trim().ToLowerInvariant();
        var name = (company.Name ?? string.Empty).Trim().ToLowerInvariant();
        return code.Contains("jewel", StringComparison.Ordinal)
            || name.Contains("jewel", StringComparison.Ordinal)
            || code is "jw" or "jewellery";
    }

    public static bool IsFitOutIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code.Contains("fitout", StringComparison.Ordinal)
            || code.Contains("fit_out", StringComparison.Ordinal)
            || code.Contains("fit-out", StringComparison.Ordinal)
            || code.Contains("construction", StringComparison.Ordinal)
            || code.Contains("building_materials", StringComparison.Ordinal);
    }

    /// <summary>Fit-out modules are allowed by the host industry or by the active company (pack, code or name), like the route gate.</summary>
    public static bool ShowFitOutModules(string? hostIndustryCode, ErpCompanyDigest? company, string? tenantIndustry = null)
        => IsFitOutIndustry(hostIndustryCode) || IsFitOutIndustry(tenantIndustry) || IsFitOutCompany(company);

    public static bool IsFitOutTab(PhpModuleCatalog.ModuleLink tab)
        => string.Equals(TabKey(tab), "project_accounting", StringComparison.OrdinalIgnoreCase);

    public static bool IsFitOutCompany(ErpCompanyDigest? company)
    {
        if (company is null)
        {
            return false;
        }

        var pack = (company.IndustryPack ?? string.Empty).Trim().ToLowerInvariant();
        var code = (company.Code ?? string.Empty).Trim().ToLowerInvariant();
        var name = (company.Name ?? string.Empty).Trim().ToLowerInvariant();
        return pack.Contains("fitout", StringComparison.Ordinal)
            || pack.Contains("fit-out", StringComparison.Ordinal)
            || pack.Contains("construction", StringComparison.Ordinal)
            || pack.Contains("building_materials", StringComparison.Ordinal)
            || code.Contains("fit", StringComparison.Ordinal)
            || name.Contains("fit-out", StringComparison.Ordinal)
            || name.Contains("fitout", StringComparison.Ordinal)
            || name.Contains("construction", StringComparison.Ordinal);
    }

    public static bool IsRealEstateIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code.Contains("real_estate", StringComparison.Ordinal)
            || code.Contains("real-estate", StringComparison.Ordinal)
            || code.Contains("realestate", StringComparison.Ordinal);
    }

    public static bool ShowRealEstateModules(string? hostIndustryCode, ErpCompanyDigest? company, string? tenantIndustry = null)
        => IsRealEstateIndustry(hostIndustryCode) || IsRealEstateIndustry(tenantIndustry) || IsRealEstateCompany(company);

    public static bool IsRealEstateTab(PhpModuleCatalog.ModuleLink tab)
    {
        var key = TabKey(tab);
        var href = tab.Href ?? string.Empty;
        return key.StartsWith("real_estate", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/erp/real-estate-app", StringComparison.OrdinalIgnoreCase)
            || href.Contains("/erp/landlord-app", StringComparison.OrdinalIgnoreCase)
            || href.Contains("tab=real_estate", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRealEstateCompany(ErpCompanyDigest? company)
    {
        if (company is null)
        {
            return false;
        }

        var pack = (company.IndustryPack ?? string.Empty).Trim().ToLowerInvariant();
        var code = (company.Code ?? string.Empty).Trim().ToLowerInvariant();
        var name = (company.Name ?? string.Empty).Trim().ToLowerInvariant();
        return pack.Contains("real_estate", StringComparison.Ordinal)
            || pack.Contains("real-estate", StringComparison.Ordinal)
            || pack.Contains("realestate", StringComparison.Ordinal)
            || code is "real" or "re"
            || name.Contains("real estate", StringComparison.Ordinal)
            || name.Contains("real-estate", StringComparison.Ordinal);
    }

    public static bool IsJewelleryFromHostOrPack(string? industryCode, string? industryPack, ErpCompanyDigest? company)
    {
        if (IsJewelleryCompany(company))
        {
            return true;
        }

        var pack = (industryPack ?? string.Empty).Trim().ToLowerInvariant();
        if (pack.StartsWith("jewellery", StringComparison.Ordinal) || pack.Contains("jewel", StringComparison.Ordinal))
        {
            return true;
        }

        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code is "jewellery" or "jewelry";
    }

    public static bool IsAutoPartsIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code.Contains("auto_part", StringComparison.Ordinal)
            || code.Contains("autopart", StringComparison.Ordinal)
            || code.Contains("auto-part", StringComparison.Ordinal)
            || code.Contains("spare_part", StringComparison.Ordinal)
            || code.Contains("sparepart", StringComparison.Ordinal)
            || code is "automotive" or "auto" or "asap";
    }

    /// <summary>Auto-parts modules are allowed by the host industry or by the active company (pack, code or name), like the route gate.</summary>
    public static bool ShowAutoPartsModules(string? hostIndustryCode, ErpCompanyDigest? company, string? tenantIndustry = null)
        => IsAutoPartsIndustry(hostIndustryCode) || IsAutoPartsIndustry(tenantIndustry) || IsAutoPartsCompany(company);

    public static bool IsAutoPartsCompany(ErpCompanyDigest? company)
    {
        if (company is null)
        {
            return false;
        }

        var pack = (company.IndustryPack ?? string.Empty).Trim().ToLowerInvariant();
        var code = (company.Code ?? string.Empty).Trim().ToLowerInvariant();
        var name = (company.Name ?? string.Empty).Trim().ToLowerInvariant();
        return pack.Contains("auto_part", StringComparison.Ordinal)
            || pack.Contains("autopart", StringComparison.Ordinal)
            || pack.Contains("auto-part", StringComparison.Ordinal)
            || pack.Contains("spare_part", StringComparison.Ordinal)
            || pack.Contains("sparepart", StringComparison.Ordinal)
            || code is "asap" or "auto" or "autoparts"
            || name.Contains("auto part", StringComparison.Ordinal)
            || name.Contains("autopart", StringComparison.Ordinal)
            || name.Contains("spare part", StringComparison.Ordinal)
            || name.Contains("sparepart", StringComparison.Ordinal);
    }

    public static bool IsFmcgIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return code.Contains("fmcg", StringComparison.Ordinal)
            || code.Contains("fmcg_wholesale", StringComparison.Ordinal)
            || code.Contains("food_beverage", StringComparison.Ordinal)
            || code.Contains("food-beverage", StringComparison.Ordinal)
            || code is "fmcg" or "food" or "food_beverage" or "consumer_goods";
    }

    /// <summary>FMCG modules are allowed by the host industry, tenant industry, or the active company's pack/code/name.</summary>
    public static bool ShowFmcgModules(string? hostIndustryCode, ErpCompanyDigest? company, string? tenantIndustry = null)
        => IsFmcgIndustry(hostIndustryCode) || IsFmcgIndustry(tenantIndustry) || IsFmcgCompany(company);

    public static bool IsFmcgCompany(ErpCompanyDigest? company)
    {
        if (company is null)
        {
            return false;
        }

        var pack = (company.IndustryPack ?? string.Empty).Trim().ToLowerInvariant();
        var code = (company.Code ?? string.Empty).Trim().ToLowerInvariant();
        var name = (company.Name ?? string.Empty).Trim().ToLowerInvariant();
        return pack.Contains("fmcg", StringComparison.Ordinal)
            || pack.Contains("food_beverage", StringComparison.Ordinal)
            || pack.Contains("consumer_goods", StringComparison.Ordinal)
            || code is "fmcg" or "food"
            || name.Contains("fmcg", StringComparison.Ordinal)
            || name.Contains("food", StringComparison.Ordinal)
            || name.Contains("beverage", StringComparison.Ordinal);
    }

    /// <summary>
    /// Product jewellery tenants (thejewellerytrend.com) always get jewellery
    /// modules — even when legal entity 1 is coded MAIN with an empty pack.
    /// Super CP / auto-parts hosts still require a jewellery company or pack.
    /// </summary>
    public static bool ShowJewelleryModules(string? hostIndustryCode, ErpCompanyDigest? company, string? tenantIndustry = null)
        => IsJewelleryFromHostOrPack(hostIndustryCode, tenantIndustry, company)
           || IsJewelleryFromHostOrPack(tenantIndustry, tenantIndustry, company);

    public static bool IsJewelleryTab(PhpModuleCatalog.ModuleLink tab)
    {
        if (SharedTabIds.Contains(TabKey(tab)))
        {
            return false;
        }

        if (tab.IsJewellery)
        {
            return true;
        }

        // Generated catalog ids look like "inventory_mgmt/jw_karat".
        var id = tab.Id ?? string.Empty;
        var slash = id.LastIndexOf('/');
        var key = slash >= 0 && slash < id.Length - 1 ? id[(slash + 1)..] : id;
        if (key.StartsWith("jw_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (JewelleryTabIds.Contains(key))
        {
            return true;
        }

        var href = tab.Href ?? string.Empty;
        foreach (var marker in JewelleryTabIds)
        {
            if (href.Contains("tab=" + marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return href.Contains("tab=jw_", StringComparison.OrdinalIgnoreCase)
            || href.Contains("[JW]", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Module directory rows like PHP <c>epc_jw_is_jewellery_tenant</c>: jewellery-only
    /// tabs are omitted unless the tenant/company is jewellery.
    /// </summary>
    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> DirectoryTabs(bool jewelleryCompany)
        => DirectoryTabs(jewelleryCompany, fitOutCompany: true, realEstateCompany: true);

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> DirectoryTabs(bool jewelleryCompany, bool fitOutCompany)
        => DirectoryTabs(jewelleryCompany, fitOutCompany, realEstateCompany: true);

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> DirectoryTabs(bool jewelleryCompany, bool fitOutCompany, bool realEstateCompany)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PhpModuleCatalog.ModuleLink>(PhpModuleCatalog.ErpTabs.Count + PhpModuleCatalog.AspNetErpTabs.Count);
        foreach (var tab in PhpModuleCatalog.ErpTabs.Concat(PhpModuleCatalog.AspNetErpTabs))
        {
            if (string.IsNullOrWhiteSpace(tab.Id) || !seen.Add(tab.Id))
            {
                continue;
            }

            if ((!jewelleryCompany && IsJewelleryTab(tab))
                || (!fitOutCompany && IsFitOutTab(tab))
                || (!realEstateCompany && IsRealEstateTab(tab)))
            {
                continue;
            }

            rows.Add(tab);
        }

        return rows;
    }

    /// <summary>
    /// Filter ERP topnav like PHP: strip jewellery-only tabs unless the active company is jewellery.
    /// </summary>
    public static IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> FilterTopnav(
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> groups,
        bool jewelleryCompany)
        => FilterTopnav(
            groups,
            new ErpNavAudience(
                TenantVersion: string.Empty,
                IndustryCode: jewelleryCompany ? "jewellery" : null,
                CountryCode: null,
                IsSuperErp: false));

    /// <summary>
    /// Applies the PHP navigation audience rules without inventing a tenant's
    /// module entitlement. An empty disabled set preserves the complete PHP
    /// tree; super-ERP can pass a versioned, tenant-specific deny set.
    /// </summary>
    public static IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> FilterTopnav(
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> groups,
        ErpNavAudience audience)
    {
        var filtered = new List<LegacyDesktopChromeCatalog.MegaGroup>();
        var seenTabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDestinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var columns = new List<LegacyDesktopChromeCatalog.MegaAreaColumn>();
            var allTabs = new List<PhpModuleCatalog.ModuleLink>();
            foreach (var col in group.Columns ?? Array.Empty<LegacyDesktopChromeCatalog.MegaAreaColumn>())
            {
                var tabs = col.Tabs
                    .Where(t => !IsDisabled(t, audience))
                    .Where(t => IsEnabled(t, audience))
                    .Where(t => seenTabIds.Add(t.Id))
                    .Where(t => seenDestinations.Add(TopMenuLinks.DestinationKey(TopMenuLinks.ErpHref(t.Href))))
                    .ToList();
                if (tabs.Count == 0)
                {
                    continue;
                }

                columns.Add(col with { Tabs = tabs });
                allTabs.AddRange(tabs);
            }

            if (columns.Count == 0 || allTabs.Count == 0)
            {
                continue;
            }

            filtered.Add(group with
            {
                Links = allTabs,
                Columns = columns,
                HubHref = allTabs[0].Href,
            });
        }

        if (audience.IsSuperErp)
        {
            var mailTabs = SuperErpMailTabs()
                .Where(t => seenDestinations.Add(TopMenuLinks.DestinationKey(TopMenuLinks.ErpHref(t.Href))))
                .ToList();
            if (mailTabs.Count > 0)
            {
                var at = Math.Min(1, filtered.Count);
                filtered.Insert(at, new LegacyDesktopChromeCatalog.MegaGroup(
                    "mail",
                    "Mail",
                    mailTabs,
                    "fa-inbox",
                    "Mail",
                    mailTabs[0].Href,
                    [new LegacyDesktopChromeCatalog.MegaAreaColumn("mail", "Mailbox & SMTP", "fa-inbox", mailTabs)],
                    "Platform mailboxes, SMTP and alerts"));
            }
        }

        return filtered;
    }

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> SuperErpMailTabs() =>
    [
        new("mail/platform_mail", "Platform mailboxes", "/erp/super-mail-app", "fa-inbox", "mail"),
        new("mail/tenant_email", "Tenant email / SMTP", "/erp/tenant-email-app", "fa-envelope", "mail"),
        new("mail/communication", "Communication", "/erp/platform-communication-app", "fa-comments", "mail"),
        new("mail/notifications", "Notifications", "/erp/notifications-app", "fa-bell", "mail"),
        new("mail/sms", "SMS & WhatsApp", "/erp/sms-whatsapp-app", "fa-mobile", "mail"),
    ];

    public static IReadOnlyList<string> DuplicateTabIds(
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> groups)
        => groups
            .SelectMany(g => g.Columns ?? Array.Empty<LegacyDesktopChromeCatalog.MegaAreaColumn>())
            .SelectMany(c => c.Tabs)
            .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<NavInspectionRow> Inspect(
        ErpNavAudience audience)
        => PhpModuleCatalog.ErpTabs
            .Select(tab =>
            {
                var slash = tab.Id.LastIndexOf('/');
                var area = slash > 0 ? tab.Id[..slash] : tab.Group ?? string.Empty;
                var key = slash >= 0 && slash < tab.Id.Length - 1
                    ? tab.Id[(slash + 1)..]
                    : tab.Id;
                var decision = ExplainTab(tab, audience);
                return new NavInspectionRow(
                    tab.Id,
                    area,
                    key,
                    tab.Label,
                    decision.Visible,
                    decision.Code,
                    decision.Detail);
            })
            .ToArray();

    /// <summary>
    /// Explains a tab decision for Super ERP diagnostics and tenant support.
    /// The PHP tree remains the default source of visibility; policy only adds
    /// explicit tenant, industry, or module-pack restrictions.
    /// </summary>
    public static NavVisibility ExplainTab(
        PhpModuleCatalog.ModuleLink tab,
        ErpNavAudience audience)
    {
        if (IsJewelleryTab(tab)
            && !IsJewelleryFromHostOrPack(
                audience.IndustryCode,
                audience.IndustryCode,
                audience.Company))
        {
            return new(false, "industry", "Jewellery module requires the jewellery industry pack.");
        }

        if (IsFitOutTab(tab) && !ShowFitOutModules(audience.IndustryCode, audience.Company))
        {
            return new(false, "industry", "Project accounting requires the fit-out / construction industry pack.");
        }

        if (IsRealEstateTab(tab) && !ShowRealEstateModules(audience.IndustryCode, audience.Company))
        {
            return new(false, "industry", "Real estate requires the real-estate investment industry pack.");
        }

        if (audience.DisabledTabIds is { Count: > 0 }
            && (audience.DisabledTabIds.Contains(tab.Id)
                || audience.DisabledTabIds.Contains(TabKey(tab))))
        {
            return new(false, "disabled", "Explicit tenant/version deny rule.");
        }

        if (!IsEnabled(tab, audience))
        {
            return new(false, "module-pack", "Tab is outside the enabled tenant module pack.");
        }

        var scope = audience.IsSuperErp ? "Super ERP" : "tenant";
        var country = string.IsNullOrWhiteSpace(audience.CountryCode)
            ? "registered country unresolved"
            : audience.CountryCode.Trim().ToUpperInvariant();
        return new(true, "php-default", $"PHP placement enabled for {scope}; country context {country}.");
    }

    private static bool IsDisabled(
        PhpModuleCatalog.ModuleLink tab,
        ErpNavAudience audience)
    {
        return !ExplainTab(tab, audience).Visible;
    }

    private static bool IsEnabled(
        PhpModuleCatalog.ModuleLink tab,
        ErpNavAudience audience)
    {
        if (audience.EnabledModuleIds is null || audience.EnabledModuleIds.Count == 0)
        {
            return true;
        }

        var key = TabKey(tab);
        var area = tab.Group ?? string.Empty;
        return audience.EnabledModuleIds.Contains(tab.Id)
            || audience.EnabledModuleIds.Contains(key)
            || audience.EnabledModuleIds.Contains(area);
    }

    private static string TabKey(PhpModuleCatalog.ModuleLink tab)
    {
        var slash = tab.Id.LastIndexOf('/');
        return slash >= 0 ? tab.Id[(slash + 1)..] : tab.Id;
    }

    /// <summary>
    /// Fallback company list when DB has no legal entities yet — MAIN (shared modules)
    /// + Jewellery division (jewellery pack) so the top-level switcher is usable on platform.
    /// </summary>
    public static IReadOnlyList<ErpCompanyDigest> FallbackCompanies(string brandLabel)
    {
        var mainName = LegalOrganizationLabel.Resolve("Main Company", null, brandLabel);
        return
        [
            new ErpCompanyDigest(1, "MAIN", mainName, "AED", "AE", "", true),
            new ErpCompanyDigest(2, "JW", "Jewellery Division", "AED", "AE", "jewellery_diamond", true),
            new ErpCompanyDigest(3, "FITOUT", "Fit-out Company", "AED", "AE", "fitout_contracting", true),
            new ErpCompanyDigest(4, "REAL", "Real Estate Company", "AED", "AE", "real-estate-development", true),
        ];
    }

    /// <summary>
    /// Empty shop DB (this VM / new tenant) gets MAIN + Jewellery Division so
    /// <c>?company=1</c> ↔ <c>?company=2</c> works. Super-CP also merges those
    /// fallbacks when the digest is missing one of them. Tenant hosts that already
    /// have legal-entity rows keep those rows as-is.
    /// </summary>
    public static IReadOnlyList<ErpCompanyDigest> EnsureSwitchableCompanies(
        IReadOnlyList<ErpCompanyDigest>? companies,
        bool isSuperCpHost,
        string brandLabel)
    {
        var list = companies is { Count: > 0 }
            ? companies.ToList()
            : new List<ErpCompanyDigest>();

        if (list.Count == 0)
        {
            return FallbackCompanies(brandLabel).ToList();
        }

        if (!isSuperCpHost)
        {
            return list;
        }

        foreach (var fallback in FallbackCompanies(brandLabel))
        {
            if (list.Any(c => c.Id == fallback.Id
                || string.Equals(c.Code, fallback.Code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(fallback);
        }

        return list;
    }

    public static string IndustryLabelForPack(string? pack, bool jewellery)
    {
        if (jewellery)
        {
            return string.IsNullOrWhiteSpace(pack) ? "Jewellery & luxury" : pack.Replace('_', ' ');
        }

        if (IsRealEstateIndustry(pack))
        {
            return string.IsNullOrWhiteSpace(pack) ? "Real estate" : pack.Replace('_', ' ').Replace('-', ' ');
        }

        return string.IsNullOrWhiteSpace(pack) ? "Core / multi-industry" : pack.Replace('_', ' ');
    }
}
