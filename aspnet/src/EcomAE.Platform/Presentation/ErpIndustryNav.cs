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
    private static readonly HashSet<string> JewelleryTabIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "gold_rate", "jewellery_tag", "gold_scheme", "aml_compliance",
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

    public static bool IsJewelleryTab(PhpModuleCatalog.ModuleLink tab)
    {
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
    /// Filter ERP topnav like PHP: strip jewellery-only tabs unless the active company is jewellery.
    /// </summary>
    public static IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> FilterTopnav(
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> groups,
        bool jewelleryCompany)
    {
        if (jewelleryCompany)
        {
            return groups;
        }

        var filtered = new List<LegacyDesktopChromeCatalog.MegaGroup>();
        foreach (var group in groups)
        {
            var columns = new List<LegacyDesktopChromeCatalog.MegaAreaColumn>();
            var allTabs = new List<PhpModuleCatalog.ModuleLink>();
            foreach (var col in group.Columns ?? Array.Empty<LegacyDesktopChromeCatalog.MegaAreaColumn>())
            {
                var tabs = col.Tabs.Where(t => !IsJewelleryTab(t)).ToList();
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

        return filtered;
    }

    /// <summary>
    /// Fallback company list when DB has no legal entities yet — MAIN (shared modules)
    /// + Jewellery division (jewellery pack) so the top-level switcher is usable on platform.
    /// </summary>
    public static IReadOnlyList<ErpCompanyDigest> FallbackCompanies(string brandLabel)
    {
        var mainName = string.IsNullOrWhiteSpace(brandLabel) ? "Main Company" : brandLabel + " — Main";
        return
        [
            new ErpCompanyDigest(1, "MAIN", mainName, "AED", "AE", "", true),
            new ErpCompanyDigest(2, "JW", "Jewellery Division", "AED", "AE", "jewellery_diamond", true),
        ];
    }

    public static string IndustryLabelForPack(string? pack, bool jewellery)
    {
        if (jewellery)
        {
            return string.IsNullOrWhiteSpace(pack) ? "Jewellery & luxury" : pack.Replace('_', ' ');
        }

        return string.IsNullOrWhiteSpace(pack) ? "Core / multi-industry" : pack.Replace('_', ' ');
    }
}
