using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-pack helpers. PHP identifiers kept for the inventory:
/// <c>epc_ecomae_faq_modules</c>, <c>epc_ecomae_faq_status_counts</c>,
/// <c>epc_ecomae_legal_effective_date</c>, <c>epc_ecomae_legal_catalog</c>,
/// <c>epc_ecomae_legal_top_level_aliases</c>,
/// <c>epc_ded_divisions</c>, <c>epc_ded_total_activities</c>,
/// <c>epc_ded_divisions_for_group</c>, <c>epc_worldwide_business_registries</c>,
/// <c>epc_ded_coverage_audit</c>, <c>epc_ded_portal_industry_bridge</c>,
/// <c>epc_ded_portal_bridge_complete</c>.
/// </summary>
public static class PhpPlanQ1Pack
{
    public const string FaqDataPath = "content/general_pages/epc_ecomae_faq_data.php";
    public const string LegalContentPath = "content/general_pages/epc_ecomae_legal_content.php";
    public const string DedActivityMappingPath = "content/general_pages/epc_ded_activity_mapping.php";

    private static readonly Lazy<JsonElement> Faq = new(() => JsonDocument.Parse(PhpPlanQ1PackJson.FaqJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Legal = new(() => JsonDocument.Parse(PhpPlanQ1PackJson.LegalJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Ded = new(() => JsonDocument.Parse(PhpPlanQ1PackJson.DedJson).RootElement.Clone());

    public static JsonElement EpcEcomaeFaqModules() => Faq.Value.Clone();

    public static Dictionary<string, int> EpcEcomaeFaqStatusCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Yes"] = 0,
            ["Partial"] = 0,
            ["Planned"] = 0,
            ["No"] = 0
        };
        foreach (var mod in Faq.Value.EnumerateArray())
        {
            foreach (var item in mod.GetProperty("items").EnumerateArray())
            {
                var status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "Partial" : "Partial";
                if (counts.ContainsKey(status))
                {
                    counts[status]++;
                }
            }
        }

        return counts;
    }

    public static string EpcEcomaeLegalEffectiveDate() => Legal.Value.GetProperty("date").GetString() ?? "";

    public static JsonElement EpcEcomaeLegalCatalog() => Legal.Value.GetProperty("catalog").Clone();

    public static JsonElement EpcEcomaeLegalTopLevelAliases() => Legal.Value.GetProperty("aliases").Clone();

    public static JsonElement EpcDedDivisions() => Ded.Value.GetProperty("divisions").Clone();

    public static int EpcDedTotalActivities()
    {
        var total = 0;
        foreach (var div in Ded.Value.GetProperty("divisions").EnumerateObject())
        {
            total += div.Value.GetProperty("activities").GetInt32();
        }

        return total;
    }

    public static Dictionary<string, JsonElement> EpcDedDivisionsForGroup(string groupKey)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var div in Ded.Value.GetProperty("divisions").EnumerateObject())
        {
            foreach (var g in div.Value.GetProperty("ecomae_groups").EnumerateArray())
            {
                if (g.GetString() == groupKey)
                {
                    result[div.Name] = div.Value.Clone();
                    break;
                }
            }
        }

        return result;
    }

    public static JsonElement EpcWorldwideBusinessRegistries() => Ded.Value.GetProperty("registries").Clone();

    public static List<string> EpcDedCoverageAudit(IEnumerable<string>? groupKeys = null)
    {
        groupKeys ??= DefaultIndustryGroupKeys();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var div in Ded.Value.GetProperty("divisions").EnumerateObject())
        {
            foreach (var g in div.Value.GetProperty("ecomae_groups").EnumerateArray())
            {
                var key = g.GetString();
                if (!string.IsNullOrEmpty(key))
                {
                    covered.Add(key);
                }
            }
        }

        return groupKeys.Where(gk => !covered.Contains(gk)).ToList();
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcDedPortalIndustryBridge(
        IReadOnlyDictionary<string, string>? portalGroupByCode = null)
    {
        portalGroupByCode ??= DefaultPortalGroupByCode();
        var portalByGroup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pair in portalGroupByCode)
        {
            if (pair.Key == "platform_host")
            {
                continue;
            }

            var gk = pair.Value ?? "";
            if (gk == "")
            {
                continue;
            }

            if (!portalByGroup.TryGetValue(gk, out var list))
            {
                list = new List<string>();
                portalByGroup[gk] = list;
            }

            list.Add(pair.Key);
        }

        var output = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var div in Ded.Value.GetProperty("divisions").EnumerateObject())
        {
            var codes = new Dictionary<string, bool>(StringComparer.Ordinal);
            var groups = new List<string>();
            foreach (var g in div.Value.GetProperty("ecomae_groups").EnumerateArray())
            {
                var gk = g.GetString() ?? "";
                groups.Add(gk);
                if (portalByGroup.TryGetValue(gk, out var list))
                {
                    foreach (var code in list)
                    {
                        codes[code] = true;
                    }
                }
            }

            output[div.Name] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = div.Value.GetProperty("label").GetString() ?? "",
                ["activities"] = div.Value.GetProperty("activities").GetInt32(),
                ["ecomae_groups"] = groups,
                ["portal_codes"] = codes.Keys.ToList()
            };
        }

        return output;
    }

    public static bool EpcDedPortalBridgeComplete(IReadOnlyDictionary<string, string>? portalGroupByCode = null)
    {
        foreach (var row in EpcDedPortalIndustryBridge(portalGroupByCode).Values)
        {
            if (row["portal_codes"] is not List<string> codes || codes.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<string> DefaultIndustryGroupKeys()
        => new[] { "agriculture_farming", "missing_one" };

    public static IReadOnlyDictionary<string, string> DefaultPortalGroupByCode()
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agri"] = "agriculture_farming",
            ["fish"] = "agriculture_farming",
            ["platform_host"] = "x",
            ["blank"] = ""
        };
}
