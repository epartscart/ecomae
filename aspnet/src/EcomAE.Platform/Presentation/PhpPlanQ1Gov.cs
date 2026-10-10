using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-gov helpers. PHP identifiers kept for the inventory:
/// <c>epc_platform_governance_categories</c>, <c>epc_platform_governance_enforcement_levels</c>,
/// <c>epc_platform_governance_db_ensure</c>, <c>epc_platform_governance_default_rules</c>,
/// <c>epc_platform_governance_seed</c>, <c>epc_platform_governance_list_rules</c>,
/// <c>epc_platform_governance_update_rule</c>, <c>epc_platform_governance_rule_applies</c>,
/// <c>epc_platform_governance_active_rules</c>, <c>epc_platform_governance_blocked_for_branding</c>,
/// <c>EPC_TENANT_CONFIG_VERSION</c>, <c>epc_tenant_config_ensure_schema</c>,
/// <c>epc_tenant_config_groups</c>, <c>epc_tenant_config_get</c>, <c>epc_tenant_config_group</c>,
/// <c>epc_tenant_config_all</c>, <c>epc_tenant_config_set</c>, <c>epc_tenant_config_bulk_set</c>,
/// <c>epc_tenant_config_history</c>, <c>epc_tenant_config_fleet</c>,
/// <c>epc_tenant_config_export</c>, <c>epc_tenant_config_import</c>.
/// </summary>
public static class PhpPlanQ1Gov
{
    public const string PlatformGovernancePath = "content/general_pages/epc_platform_governance.php";
    public const string TenantConfigPath = "content/general_pages/epc_tenant_config.php";
    public const string EpcTenantConfigVersion = "1.0.0";

    private static readonly Lazy<JsonElement> Gov = new(() => JsonDocument.Parse(PhpPlanQ1GovData.GovernanceJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Groups = new(() => JsonDocument.Parse(PhpPlanQ1GovData.TenantGroupsJson).RootElement.Clone());
    private static readonly string[] UpdateRuleCols = { "active", "enforcement", "title", "description" };

    public sealed class GovRule
    {
        public string RuleKey { get; set; } = "";
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Enforcement { get; set; } = "required";
        public string Scope { get; set; } = "all_tenants";
        public string ConfigJson { get; set; } = "{}";
        public Dictionary<string, object?> Config { get; set; } = new(StringComparer.Ordinal);
        public string ModuleLink { get; set; } = "";
        public int Active { get; set; } = 1;
        public long TimeUpdated { get; set; }
    }

    public sealed class GovStore
    {
        public bool SchemaReady { get; set; }
        public List<GovRule> Rules { get; } = new();
        public Func<long>? Clock { get; set; }
        public long Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public sealed class TenantConfigRow
    {
        public string SiteKey { get; set; } = "";
        public string Group { get; set; } = "";
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string ValueType { get; set; } = "string";
        public string Label { get; set; } = "";
        public int UpdatedBy { get; set; }
        public string UpdatedAt { get; set; } = "";
    }

    public sealed class TenantHistoryRow
    {
        public string SiteKey { get; set; } = "";
        public string Group { get; set; } = "";
        public string Key { get; set; } = "";
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = "";
        public int ChangedBy { get; set; }
        public string ChangedAt { get; set; } = "";
    }

    public sealed class TenantConfigStore
    {
        public bool SchemaReady { get; set; }
        public List<TenantConfigRow> Rows { get; } = new();
        public List<TenantHistoryRow> History { get; } = new();
    }

    public static JsonElement EpcPlatformGovernanceCategories() => Gov.Value.GetProperty("categories").Clone();
    public static string[] EpcPlatformGovernanceEnforcementLevels()
        => Gov.Value.GetProperty("levels").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
    public static JsonElement EpcPlatformGovernanceDefaultRules() => Gov.Value.GetProperty("defaults").Clone();
    public static void EpcPlatformGovernanceDbEnsure(GovStore store) => store.SchemaReady = true;

    public static int EpcPlatformGovernanceSeed(GovStore store)
    {
        EpcPlatformGovernanceDbEnsure(store);
        var now = store.Now();
        var count = 0;
        foreach (var raw in EpcPlatformGovernanceDefaultRules().EnumerateArray())
        {
            var key = raw.GetProperty("rule_key").GetString() ?? "";
            var configEl = raw.GetProperty("config_json");
            var configJson = configEl.ValueKind == JsonValueKind.Array || configEl.ValueKind == JsonValueKind.Object
                ? configEl.GetRawText()
                : "{}";
            var existing = store.Rules.FirstOrDefault(r => r.RuleKey == key);
            if (existing is null)
            {
                store.Rules.Add(new GovRule
                {
                    RuleKey = key,
                    Category = raw.GetProperty("category").GetString() ?? "tenant",
                    Title = raw.GetProperty("title").GetString() ?? "",
                    Description = raw.GetProperty("description").GetString() ?? "",
                    Enforcement = raw.GetProperty("enforcement").GetString() ?? "required",
                    Scope = raw.GetProperty("scope").GetString() ?? "all_tenants",
                    ConfigJson = configJson,
                    Config = ParseConfig(configJson),
                    ModuleLink = raw.TryGetProperty("module_link", out var link) ? link.GetString() ?? "" : "",
                    Active = 1,
                    TimeUpdated = now
                });
            }
            else
            {
                if (existing.Title == "")
                {
                    existing.Title = raw.GetProperty("title").GetString() ?? "";
                }

                if (string.IsNullOrEmpty(existing.Description))
                {
                    existing.Description = raw.GetProperty("description").GetString() ?? "";
                }

                if (existing.ModuleLink == "")
                {
                    existing.ModuleLink = raw.TryGetProperty("module_link", out var link) ? link.GetString() ?? "" : "";
                }

                existing.TimeUpdated = now;
            }

            count++;
        }

        return count;
    }

    public static List<GovRule> EpcPlatformGovernanceListRules(GovStore store, string? category = null)
    {
        EpcPlatformGovernanceDbEnsure(store);
        IEnumerable<GovRule> q = store.Rules;
        if (!string.IsNullOrEmpty(category))
        {
            q = q.Where(r => r.Category == category);
        }

        return q.OrderBy(r => r.Category, StringComparer.Ordinal).ThenBy(r => r.RuleKey, StringComparer.Ordinal).ToList();
    }

    public static bool EpcPlatformGovernanceUpdateRule(GovStore store, string ruleKey, Dictionary<string, object?> patch)
    {
        EpcPlatformGovernanceDbEnsure(store);
        var sets = false;
        var row = store.Rules.FirstOrDefault(r => r.RuleKey == ruleKey);
        foreach (var col in UpdateRuleCols)
        {
            if (!patch.ContainsKey(col))
            {
                continue;
            }

            if (col == "active")
            {
                if (row is not null)
                {
                    row.Active = IsPhpEmpty(patch["active"]) ? 0 : 1;
                }

                sets = true;
            }
            else if (col == "enforcement")
            {
                var enf = Convert.ToString(patch["enforcement"], CultureInfo.InvariantCulture) ?? "";
                if (!EpcPlatformGovernanceEnforcementLevels().Contains(enf, StringComparer.Ordinal))
                {
                    continue;
                }

                if (row is not null)
                {
                    row.Enforcement = enf;
                }

                sets = true;
            }
            else
            {
                var value = Convert.ToString(patch[col], CultureInfo.InvariantCulture) ?? "";
                if (row is not null)
                {
                    if (col == "title")
                    {
                        row.Title = value;
                    }
                    else
                    {
                        row.Description = value;
                    }
                }

                sets = true;
            }
        }

        if (!sets)
        {
            return false;
        }

        if (row is not null)
        {
            row.TimeUpdated = store.Now();
        }

        return true;
    }

    public static bool EpcPlatformGovernanceRuleApplies(Dictionary<string, object?> rule, Dictionary<string, object?> context)
    {
        var scope = Convert.ToString(rule.TryGetValue("scope", out var s) ? s : "all_tenants", CultureInfo.InvariantCulture) ?? "all_tenants";
        if (scope == "all_tenants")
        {
            return true;
        }

        if (scope == "demo" && !IsPhpEmpty(context.TryGetValue("is_demo", out var demo) ? demo : null))
        {
            return true;
        }

        if (scope == "erp_only" && Convert.ToString(context.TryGetValue("access_mode", out var mode) ? mode : "", CultureInfo.InvariantCulture) == "erp_only")
        {
            return true;
        }

        if (scope == "tenant_key")
        {
            var key = Convert.ToString(context.TryGetValue("site_key", out var sk) ? sk : "", CultureInfo.InvariantCulture) ?? "";
            var cfg = rule.TryGetValue("config", out var raw) && raw is Dictionary<string, object?> dict ? dict : new Dictionary<string, object?>();
            var target = Convert.ToString(cfg.TryGetValue("tenant_key", out var tk) ? tk : cfg.TryGetValue("site_key", out var tsk) ? tsk : "", CultureInfo.InvariantCulture) ?? "";
            return target == "" || (key != "" && key == target);
        }

        if (scope.StartsWith("tenant:", StringComparison.Ordinal))
        {
            var key = scope[7..];
            return key != "" && key == (Convert.ToString(context.TryGetValue("site_key", out var sk) ? sk : "", CultureInfo.InvariantCulture) ?? "");
        }

        return false;
    }

    public static List<GovRule> EpcPlatformGovernanceActiveRules(GovStore store, Dictionary<string, object?>? context = null)
    {
        context ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var outRows = new List<GovRule>();
        foreach (var rule in EpcPlatformGovernanceListRules(store))
        {
            if (rule.Active == 0)
            {
                continue;
            }

            var asDict = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["scope"] = rule.Scope,
                ["config"] = rule.Config
            };
            if (EpcPlatformGovernanceRuleApplies(asDict, context))
            {
                outRows.Add(rule);
            }
        }

        return outRows;
    }

    public static bool EpcPlatformGovernanceBlockedForBranding(string html)
    {
        var lower = html.ToLowerInvariant();
        return lower.Contains("umapi", StringComparison.Ordinal)
            || lower.Contains("docpart", StringComparison.Ordinal)
            || lower.Contains("crossbase", StringComparison.Ordinal);
    }

    public static JsonElement EpcTenantConfigGroups() => Groups.Value.Clone();
    public static void EpcTenantConfigEnsureSchema(TenantConfigStore store) => store.SchemaReady = true;

    public static string EpcTenantConfigGet(TenantConfigStore store, string siteKey, string group, string key)
    {
        EpcTenantConfigEnsureSchema(store);
        var row = store.Rows.FirstOrDefault(r => r.SiteKey == siteKey && r.Group == group && r.Key == key);
        if (row is not null)
        {
            return row.Value;
        }

        return FieldDefault(group, key);
    }

    public static List<Dictionary<string, object?>> EpcTenantConfigGroup(TenantConfigStore store, string siteKey, string group)
    {
        EpcTenantConfigEnsureSchema(store);
        if (!Groups.Value.TryGetProperty(group, out var def))
        {
            return new List<Dictionary<string, object?>>();
        }

        var stored = store.Rows.Where(r => r.SiteKey == siteKey && r.Group == group).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);
        var result = new List<Dictionary<string, object?>>();
        foreach (var field in def.GetProperty("fields").EnumerateArray())
        {
            var key = field.GetProperty("key").GetString() ?? "";
            var fallback = field.GetProperty("default").GetString() ?? "";
            result.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["label"] = field.GetProperty("label").GetString(),
                ["type"] = field.GetProperty("type").GetString(),
                ["value"] = stored.TryGetValue(key, out var v) ? v : fallback,
                ["default"] = fallback
            });
        }

        return result;
    }

    public static Dictionary<string, object?> EpcTenantConfigAll(TenantConfigStore store, string siteKey)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var group in Groups.Value.EnumerateObject())
        {
            result[group.Name] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = group.Value.GetProperty("label").GetString(),
                ["icon"] = group.Value.GetProperty("icon").GetString(),
                ["fields"] = EpcTenantConfigGroup(store, siteKey, group.Name)
            };
        }

        return result;
    }

    public static Dictionary<string, object?> EpcTenantConfigSet(TenantConfigStore store, string siteKey, string group, string key, string value, int userId = 0)
    {
        EpcTenantConfigEnsureSchema(store);
        if (!Groups.Value.TryGetProperty(group, out var def))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid config group" };
        }

        JsonElement? fieldDef = null;
        foreach (var f in def.GetProperty("fields").EnumerateArray())
        {
            if (f.GetProperty("key").GetString() == key)
            {
                fieldDef = f;
                break;
            }
        }

        if (fieldDef is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid config key" };
        }

        var oldValue = EpcTenantConfigGet(store, siteKey, group, key);
        var row = store.Rows.FirstOrDefault(r => r.SiteKey == siteKey && r.Group == group && r.Key == key);
        if (row is null)
        {
            store.Rows.Add(new TenantConfigRow
            {
                SiteKey = siteKey,
                Group = group,
                Key = key,
                Value = value,
                ValueType = fieldDef.Value.GetProperty("type").GetString() ?? "string",
                Label = fieldDef.Value.GetProperty("label").GetString() ?? "",
                UpdatedBy = userId,
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedBy = userId;
            row.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        store.History.Add(new TenantHistoryRow
        {
            SiteKey = siteKey,
            Group = group,
            Key = key,
            OldValue = oldValue,
            NewValue = value,
            ChangedBy = userId,
            ChangedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["key"] = key, ["old_value"] = oldValue, ["new_value"] = value };
    }

    public static Dictionary<string, object?> EpcTenantConfigBulkSet(TenantConfigStore store, string siteKey, string group, Dictionary<string, object?> values, int userId = 0)
    {
        var results = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in values)
        {
            results[kv.Key] = EpcTenantConfigSet(store, siteKey, group, kv.Key, Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "", userId);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["results"] = results };
    }

    /// <summary>PHP binds LIMIT ? which fatals on this MariaDB; the twin takes an int limit.</summary>
    public static List<TenantHistoryRow> EpcTenantConfigHistory(TenantConfigStore store, string siteKey, int limit = 50)
    {
        EpcTenantConfigEnsureSchema(store);
        return store.History.Where(h => h.SiteKey == siteKey).OrderByDescending(h => h.ChangedAt).Take(limit).ToList();
    }

    public static List<Dictionary<string, object?>> EpcTenantConfigFleet(TenantConfigStore store)
    {
        EpcTenantConfigEnsureSchema(store);
        return store.Rows
            .GroupBy(r => r.SiteKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["configs_set"] = g.Count(),
                ["groups_configured"] = g.Select(x => x.Group).Distinct(StringComparer.Ordinal).Count(),
                ["last_updated"] = g.Max(x => x.UpdatedAt)
            })
            .OrderByDescending(x => Convert.ToInt32(x["configs_set"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static List<Dictionary<string, string>> EpcTenantConfigExport(TenantConfigStore store, string siteKey)
        => store.Rows
            .Where(r => r.SiteKey == siteKey)
            .OrderBy(r => r.Group, StringComparer.Ordinal)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["config_group"] = r.Group,
                ["config_key"] = r.Key,
                ["config_value"] = r.Value,
                ["value_type"] = r.ValueType
            })
            .ToList();

    public static Dictionary<string, object?> EpcTenantConfigImport(TenantConfigStore store, string siteKey, IEnumerable<Dictionary<string, string>> configs, int userId = 0)
    {
        var imported = 0;
        foreach (var c in configs)
        {
            var r = EpcTenantConfigSet(
                store,
                siteKey,
                c.TryGetValue("config_group", out var g) ? g : "",
                c.TryGetValue("config_key", out var k) ? k : "",
                c.TryGetValue("config_value", out var v) ? v : "",
                userId);
            if (r.TryGetValue("ok", out var ok) && ok is true)
            {
                imported++;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["imported"] = imported };
    }

    private static Dictionary<string, object?> ParseConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static string FieldDefault(string group, string key)
    {
        if (!Groups.Value.TryGetProperty(group, out var def))
        {
            return "";
        }

        foreach (var field in def.GetProperty("fields").EnumerateArray())
        {
            if (field.GetProperty("key").GetString() == key)
            {
                return field.GetProperty("default").GetString() ?? "";
            }
        }

        return "";
    }

    private static bool IsPhpEmpty(object? value)
        => value is null
            || value is false
            || value is int i && i == 0
            || value is string s && (s.Length == 0 || s == "0");
}
