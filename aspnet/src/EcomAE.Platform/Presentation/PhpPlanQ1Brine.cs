using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-brine Super-CP ERP module registry. PHP identifiers kept for the inventory:
/// <c>epc_portal_erp_modules_registry</c>, <c>epc_portal_erp_modules_presets</c>,
/// <c>epc_portal_industry_erp_modules_preset_map</c>, <c>epc_portal_industry_erp_modules_preset</c>,
/// <c>epc_portal_erp_modules_resolve_for_onboard</c>, <c>epc_portal_erp_modules_presets_ui</c>,
/// <c>epc_portal_erp_modules_detect_preset</c>, <c>epc_portal_erp_modules_default_ids</c>,
/// <c>epc_portal_erp_modules_normalize_list</c>, <c>epc_portal_erp_modules_full_access_context</c>,
/// <c>epc_portal_erp_modules_enabled</c>, <c>epc_portal_erp_modules_enabled_areas</c>,
/// <c>epc_portal_erp_modules_allowed_tabs</c>, <c>epc_erp_filter_tabs_by_tenant_modules</c>,
/// <c>epc_portal_erp_modules_from_post</c>, <c>epc_portal_erp_modules_area_enabled</c>.
/// Path: <c>content/general_pages/epc_portal_erp_modules.php</c>.
/// GET never mints a session cookie. Leftover nav / staff / voucher parents stay injected.
/// Do not write leftover unique basenames for those parents.
/// </summary>
public static class PhpPlanQ1Brine
{
    public const string PortalErpModulesPath = "content/general_pages/epc_portal_erp_modules.php";

    public static Func<bool>? IsPlatformErpRequest { get; set; }
    public static Func<bool>? IsPlatformErpActive { get; set; }
    public static Func<Dictionary<string, object?>>? LoadSiteSettings { get; set; }
    public static Func<Dictionary<string, object?>, string>? ResolveAccessMode { get; set; }
    public static Func<Dictionary<string, object?>>? NavAreasConfig { get; set; }
    public static Func<List<string>>? StaffAllTabs { get; set; }
    public static Func<List<string>, Dictionary<string, object?>?, List<string>>? FilterCommerceTabs { get; set; }

    public static void Reset()
    {
        IsPlatformErpRequest = null;
        IsPlatformErpActive = null;
        LoadSiteSettings = null;
        ResolveAccessMode = null;
        NavAreasConfig = null;
        StaffAllTabs = null;
        FilterCommerceTabs = null;
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalErpModulesRegistry()
    {
        return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["erp_overview"] = Mod("erp_overview", "Overview", "Dashboard and cross-department workflow", "overview", "fa-th-large"),
            ["erp_sales"] = Mod("erp_sales", "Sales", "CRM, proposals, orders, revenue, receivables, fulfilment, delivery, invoices", "sales", "fa-line-chart"),
            ["erp_purchasing"] = Mod("erp_purchasing", "Purchasing", "Suppliers, RFQ, POs, payables, procurement link", "purchasing", "fa-shopping-basket"),
            ["erp_finance"] = Mod("erp_finance", "Finance", "Treasury, GL, COA, VAT, e-invoicing, opening balances", "finance", "fa-university"),
            ["erp_operations"] = Mod("erp_operations", "Operations", "Inventory, fixed assets, manufacturing", "operations", "fa-cubes"),
            ["erp_custom_shipping"] = Mod("erp_custom_shipping", "Custom & Shipping", "UAE customs declarations and logistics documentation", "custom_shipping", "fa-ship"),
            ["erp_people"] = Mod("erp_people", "People", "HR, payroll, staff profiles, expense reports", "people", "fa-users"),
            ["erp_insights"] = Mod("erp_insights", "Insights", "Reports, marketing campaigns, knowledge base, multi-entity, audit", "insights", "fa-bar-chart"),
            ["erp_collaboration"] = Mod("erp_collaboration", "Collaboration", "Agenda, contacts, documents", "collaboration", "fa-calendar"),
            ["erp_enterprise"] = Mod("erp_enterprise", "Enterprise", "Business units, financial dimensions, budgeting and listings", "enterprise", "fa-building-o"),
            ["erp_jewellery"] = Mod("erp_jewellery", "Jewellery industry pack", "Jewellery-specific fields in Inventory, Purchase, Sales, Service, Finance (shown when industry = jewellery)", "setup", "fa-diamond")
        };
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalErpModulesPresets()
    {
        var full = EpcPortalErpModulesRegistry().Keys.ToList();
        var customs = new List<string> { "erp_overview", "erp_custom_shipping", "erp_collaboration" };
        var hr = new List<string> { "erp_overview", "erp_people", "erp_collaboration" };
        return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["full_erp"] = Preset("Full ERP", "All ERP areas enabled", full),
            ["customs_logistics"] = Preset("Customs + Logistics", "Overview + customs declarations & shipping docs", customs),
            ["custom_shipping_only"] = Preset("Custom & Shipping only", "Alias for customs_logistics", customs),
            ["hr_only"] = Preset("HR only", "Overview + HR / payroll / staff", hr),
            ["people_only"] = Preset("People only", "Alias for hr_only", hr),
            ["finance_einvoice"] = Preset("Finance + e-invoice", "Overview + finance + sales invoices", new List<string> { "erp_overview", "erp_finance", "erp_sales", "erp_collaboration" })
        };
    }

    public static Dictionary<string, object?> EpcPortalIndustryErpModulesPresetMap()
        => new(StringComparer.Ordinal)
        {
            ["erp_standalone"] = "full_erp",
            ["hr_recruitment"] = "hr_only",
            ["logistics"] = "customs_logistics",
            ["tax_advisory"] = "finance_einvoice",
            ["consultancy"] = "full_erp"
        };

    public static string EpcPortalIndustryErpModulesPreset(string industryCode)
    {
        var code = Regex.Replace(industryCode.ToLowerInvariant(), "[^a-z0-9_]", "");
        var map = EpcPortalIndustryErpModulesPresetMap();
        return map.TryGetValue(code, out var preset) ? Str(preset) : "full_erp";
    }

    public static List<string> EpcPortalErpModulesResolveForOnboard(Dictionary<string, object?> intro, string industryCode = "", string accessMode = "full")
    {
        var mods = new List<string>();
        if (intro.TryGetValue("erp_modules", out var raw) && raw is System.Collections.IEnumerable && raw is not string && !IsEmptyList(raw))
        {
            mods = EpcPortalErpModulesNormalizeList(raw);
        }

        if (mods.Count == 0)
        {
            mods = EpcPortalErpModulesFromPost(intro);
        }

        if (mods.Count > 0)
        {
            return mods;
        }

        var presetId = "";
        if (intro.TryGetValue("erp_modules_preset", out var presetRaw) && !PhpEmpty(presetRaw))
        {
            presetId = Regex.Replace(Str(presetRaw).ToLowerInvariant(), "[^a-z0-9_]", "");
        }

        if (presetId == "" && industryCode != "")
        {
            presetId = EpcPortalIndustryErpModulesPreset(industryCode);
        }

        if (presetId != "")
        {
            var presets = EpcPortalErpModulesPresets();
            if (presets.TryGetValue(presetId, out var preset) && preset.TryGetValue("modules", out var modsRaw))
            {
                return EpcPortalErpModulesNormalizeList(modsRaw);
            }
        }

        return EpcPortalErpModulesDefaultIds(accessMode);
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalErpModulesPresetsUi()
    {
        var all = EpcPortalErpModulesPresets();
        var outMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var id in new[] { "full_erp", "hr_only", "customs_logistics", "finance_einvoice" })
        {
            if (all.TryGetValue(id, out var row))
            {
                outMap[id] = row;
            }
        }

        return outMap;
    }

    public static string EpcPortalErpModulesDetectPreset(IEnumerable<string> enabledIds)
    {
        var enabled = EpcPortalErpModulesNormalizeList(enabledIds.Cast<object?>().ToList());
        enabled.Sort(StringComparer.Ordinal);
        foreach (var (pid, preset) in EpcPortalErpModulesPresets())
        {
            var mods = EpcPortalErpModulesNormalizeList(preset.GetValueOrDefault("modules"));
            mods.Sort(StringComparer.Ordinal);
            if (mods.SequenceEqual(enabled, StringComparer.Ordinal))
            {
                return pid;
            }
        }

        return "";
    }

    public static List<string> EpcPortalErpModulesDefaultIds(string accessMode = "full")
    {
        var registry = EpcPortalErpModulesRegistry();
        var erpOnly = accessMode == "erp_only";
        var outList = new List<string>();
        foreach (var (id, meta) in registry)
        {
            var on = erpOnly ? PhpTruthy(meta.GetValueOrDefault("default_erp_only")) : PhpTruthy(meta.GetValueOrDefault("default_full"));
            if (on)
            {
                outList.Add(id);
            }
        }

        return outList;
    }

    public static List<string> EpcPortalErpModulesNormalizeList(object? raw)
    {
        var registry = EpcPortalErpModulesRegistry();
        var ids = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (raw is string s && s != "")
        {
            try
            {
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    raw = doc.RootElement.EnumerateArray().Select(el => (object?)el.ToString()).ToList();
                }
                else
                {
                    raw = Regex.Split(s, @"[\s,]+", RegexOptions.None).Where(x => x != "").ToList();
                }
            }
            catch (JsonException)
            {
                raw = Regex.Split(s, @"[\s,]+", RegexOptions.None).Where(x => x != "").ToList();
            }
        }

        if (raw is not System.Collections.IEnumerable enumerable || raw is string)
        {
            return [];
        }

        foreach (var item in enumerable)
        {
            object? value = item;
            if (value is Dictionary<string, object?> map && map.TryGetValue("id", out var idVal))
            {
                value = idVal;
            }

            var id = Regex.Replace(Str(value).ToLowerInvariant(), "[^a-z0-9_]", "");
            if (id != "" && registry.ContainsKey(id))
            {
                ids[id] = true;
            }
        }

        return ids.Keys.ToList();
    }

    public static bool EpcPortalErpModulesFullAccessContext()
        => Flag(IsPlatformErpRequest) || Flag(IsPlatformErpActive);

    public static List<string> EpcPortalErpModulesEnabled(Dictionary<string, object?>? settings = null)
    {
        if (EpcPortalErpModulesFullAccessContext())
        {
            return EpcPortalErpModulesRegistry().Keys.ToList();
        }

        settings ??= LoadSiteSettings?.Invoke() ?? [];
        var stored = new List<string>();
        if (settings.TryGetValue("erp_modules", out var mods) && mods is System.Collections.IEnumerable && mods is not string)
        {
            stored = EpcPortalErpModulesNormalizeList(mods);
        }
        else if (settings.TryGetValue("erp_modules_json", out var json) && !PhpEmpty(json))
        {
            stored = EpcPortalErpModulesNormalizeList(json);
        }

        if (stored.Count > 0)
        {
            return stored;
        }

        var mode = ResolveAccessMode?.Invoke(settings)
            ?? Str(settings.GetValueOrDefault("access_mode"), "full");
        return EpcPortalErpModulesDefaultIds(mode);
    }

    public static List<string> EpcPortalErpModulesEnabledAreas(Dictionary<string, object?>? settings = null)
    {
        var registry = EpcPortalErpModulesRegistry();
        var enabled = EpcPortalErpModulesEnabled(settings);
        var expand = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["overview"] = ["overview"],
            ["sales"] = ["sales", "ar", "credit_coll", "service_mgmt"],
            ["purchasing"] = ["purchasing", "ap", "landed_cost_area", "logistics"],
            ["finance"] = ["finance", "tax", "banking", "fixed_assets", "asset_mgmt", "budgeting", "consolidations", "cost_acct", "cost_mgmt", "audit_wb"],
            ["operations"] = ["inventory_mgmt", "pim", "warehouse", "mhei", "production", "master_planning_area", "retail"],
            ["custom_shipping"] = ["logistics", "purchasing"],
            ["people"] = ["people", "payroll_area", "leave_abs", "expense"],
            ["insights"] = ["finance", "sales", "enterprise", "setup", "common"],
            ["collaboration"] = ["projects", "enterprise", "common", "service_mgmt"],
            ["enterprise"] = ["enterprise", "budgeting", "common", "consolidations"]
        };
        var areas = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var modId in enabled)
        {
            var baseArea = registry.TryGetValue(modId, out var meta) ? Str(meta.GetValueOrDefault("area")) : "";
            if (baseArea == "")
            {
                continue;
            }

            foreach (var a in expand.TryGetValue(baseArea, out var list) ? list : new[] { baseArea })
            {
                areas[a] = true;
            }
        }

        areas["setup"] = true;
        areas["enterprise"] = true;
        areas["tax"] = true;
        areas["common"] = true;
        areas["risk"] = true;
        return areas.Keys.ToList();
    }

    public static List<string> EpcPortalErpModulesAllowedTabs(Dictionary<string, object?>? settings = null)
    {
        var areasCfg = NavAreasConfig?.Invoke();
        if (areasCfg is null)
        {
            return StaffAllTabs?.Invoke() ?? ["dashboard", "staff_all"];
        }

        var enabledAreas = EpcPortalErpModulesEnabledAreas(settings);
        if (enabledAreas.Count == 0)
        {
            return StaffAllTabs?.Invoke() ?? ["dashboard", "staff_all"];
        }

        var tabs = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var areaKey in enabledAreas)
        {
            if (!areasCfg.TryGetValue(areaKey, out var areaObj) || areaObj is not Dictionary<string, object?> area)
            {
                continue;
            }

            if (!area.TryGetValue("tabs", out var tabsRaw) || tabsRaw is not Dictionary<string, object?> tabMap)
            {
                continue;
            }

            foreach (var tabKey in tabMap.Keys)
            {
                if (tabKey == "procurement_link")
                {
                    continue;
                }

                tabs[tabKey] = true;
                if (tabKey == "cash_bank")
                {
                    tabs["bank_recon"] = true;
                }
            }
        }

        if (tabs.Count == 0)
        {
            return ["dashboard"];
        }

        var tabList = tabs.Keys.ToList();
        return FilterCommerceTabs?.Invoke(tabList, settings) ?? tabList;
    }

    public static List<string> EpcErpFilterTabsByTenantModules(IEnumerable<string> userTabs, Dictionary<string, object?>? settings = null)
    {
        var tenantTabs = EpcPortalErpModulesAllowedTabs(settings);
        if (tenantTabs.Count == 0)
        {
            return userTabs.ToList();
        }

        var tenantSet = new HashSet<string>(tenantTabs, StringComparer.Ordinal);
        var filtered = userTabs.Where(tenantSet.Contains).ToList();
        if (filtered.Count == 0)
        {
            return tenantTabs.Contains("dashboard", StringComparer.Ordinal) ? ["dashboard"] : [tenantTabs[0]];
        }

        return filtered;
    }

    public static List<string> EpcPortalErpModulesFromPost(Dictionary<string, object?> post)
    {
        if (post.TryGetValue("erp_modules_preset", out var presetRaw) && !PhpEmpty(presetRaw))
        {
            var presets = EpcPortalErpModulesPresets();
            var presetId = Regex.Replace(Str(presetRaw).ToLowerInvariant(), "[^a-z0-9_]", "");
            if (presetId != "" && presets.TryGetValue(presetId, out var preset) && preset.TryGetValue("modules", out var mods))
            {
                return EpcPortalErpModulesNormalizeList(mods);
            }
        }

        if (post.TryGetValue("erp_modules", out var list) && list is System.Collections.IEnumerable && list is not string)
        {
            var normalized = EpcPortalErpModulesNormalizeList(list);
            if (normalized.Count > 0)
            {
                return normalized;
            }
        }

        return [];
    }

    public static bool EpcPortalErpModulesAreaEnabled(string areaKey, Dictionary<string, object?>? settings = null)
        => EpcPortalErpModulesEnabledAreas(settings).Contains(areaKey, StringComparer.Ordinal);

    private static Dictionary<string, object?> Mod(string id, string label, string desc, string area, string icon)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["label"] = label,
            ["desc"] = desc,
            ["area"] = area,
            ["icon"] = icon,
            ["default_erp_only"] = true,
            ["default_full"] = true
        };

    private static Dictionary<string, object?> Preset(string label, string desc, List<string> modules)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["desc"] = desc,
            ["modules"] = modules
        };

    private static bool Flag(Func<bool>? fn) => fn?.Invoke() == true;

    private static bool IsEmptyList(object? raw)
    {
        if (raw is System.Collections.ICollection col)
        {
            return col.Count == 0;
        }

        if (raw is System.Collections.IEnumerable enumerable and not string)
        {
            return !enumerable.Cast<object?>().Any();
        }

        return true;
    }

    private static string Str(object? value, string fallback = "")
    {
        if (value is null)
        {
            return fallback;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    private static bool PhpTruthy(object? value)
        => value is true or 1 or 1L or 1.0 or "1" || (value is string s && s != "" && s != "0");

    private static bool PhpEmpty(object? value)
        => value is null or false or 0 or 0L or 0d || value is string s && (s == "" || s == "0");
}
