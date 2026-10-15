using System.Reflection;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Erp;

public sealed record ErpLandingChoice(string Label, string Path, string Group);

/// <summary>
/// The page a company opens after ERP login. The stored value is always an internal <c>/erp</c> path.
/// </summary>
public static class ErpLandingPage
{
    public const string SettingKey = "erp_landing_page";

    private static readonly string[] QueryKeys =
    [
        "dash_profile", "tab", "year", "company", "area", "cat", "rep", "fetch", "propertyId", "unitId",
    ];

    private static readonly HashSet<string> KnownPaths = BuildPaths();

    public static IReadOnlyList<ErpLandingChoice> Choices(bool jewellery, bool fitOut, bool realEstate)
    {
        var list = new List<ErpLandingChoice>();
        Add(list, "Company home", "/erp", "Home");
        foreach (var profile in ErpNetsuiteDashboardCatalog.Profiles.Values)
        {
            Add(list, profile.Label, "/erp?dash_profile=" + profile.Key, "Home");
        }

        foreach (var area in ErpProcessFlowCatalog.Areas)
        {
            var step = area.Visible(jewellery).FirstOrDefault();
            if (step is null || string.IsNullOrWhiteSpace(step.Href))
            {
                continue;
            }

            Add(list, area.Label, step.Href, area.CategoryLabel);
        }

        if (realEstate)
        {
            Add(list, "Year book", "/erp/landlord-app", "Real estate");
            Add(list, "Properties", "/erp/real-estate-app", "Real estate");
        }

        if (jewellery)
        {
            Add(list, "Jewellery retail", "/erp/jewellery-retail-app", "Jewellery");
            Add(list, "Gold rate", "/erp/jewellery-masters-app?tab=gold_rate", "Jewellery");
        }

        if (fitOut)
        {
            Add(list, "Projects", "/erp/project-accounting-app", "Fit-out");
        }

        return list;
    }

    public static string LabelFor(string? path, bool jewellery, bool fitOut, bool realEstate)
    {
        var normal = Normalize(path);
        if (string.IsNullOrEmpty(normal))
        {
            return "Company home";
        }

        var match = Choices(jewellery, fitOut, realEstate).FirstOrDefault(choice => Same(choice.Path, normal));
        return match?.Label ?? "Saved page";
    }

    public static bool Same(string? left, string? right)
        => string.Equals(Normalize(left) ?? "", Normalize(right) ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>Keeps a posted return address unless it is the ordinary ERP home and a landing page is saved.</summary>
    public static string Prefer(string? requested, string? stored)
    {
        var landing = Normalize(stored);
        if (string.IsNullOrEmpty(landing) || !IsDefaultHome(requested))
        {
            return string.IsNullOrWhiteSpace(requested) ? "/erp" : requested.Trim();
        }

        return WithSite(landing, SiteKeyOf(requested));
    }

    public static bool IsDefaultHome(string? path)
    {
        var bare = StripSite(path ?? "");
        var cut = bare.IndexOf('?', StringComparison.Ordinal);
        if (cut >= 0)
        {
            bare = bare[..cut];
        }

        bare = bare.TrimEnd('/');
        return bare.Length == 0 || bare.Equals("/erp", StringComparison.OrdinalIgnoreCase) || bare.Equals("/erp/app", StringComparison.OrdinalIgnoreCase);
    }

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.Length > 240
            || text.Contains("://", StringComparison.Ordinal)
            || text.StartsWith("//", StringComparison.Ordinal)
            || text.Contains('\\', StringComparison.Ordinal)
            || text.Contains("..", StringComparison.Ordinal)
            || text.Any(char.IsWhiteSpace)
            || text.Any(char.IsControl))
        {
            return null;
        }

        var hash = text.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            text = text[..hash];
        }

        text = StripSite(text);
        if (!text.StartsWith("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = "";
        var cut = text.IndexOf('?', StringComparison.Ordinal);
        var path = cut < 0 ? text : text[..cut];
        if (cut >= 0)
        {
            query = text[(cut + 1)..];
        }

        path = path.TrimEnd('/');
        if (path.Length == 0)
        {
            path = "/erp";
        }

        if (!KnownPaths.Contains(path))
        {
            return null;
        }

        var kept = new List<string>();
        if (query.Length > 0)
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=', StringComparison.Ordinal);
                if (eq <= 0)
                {
                    return null;
                }

                var key = pair[..eq];
                var value = pair[(eq + 1)..];
                if (!QueryKeys.Contains(key, StringComparer.Ordinal))
                {
                    continue;
                }

                if (!AllowedValue(key, value))
                {
                    return null;
                }

                kept.Add(key + "=" + value);
            }
        }

        kept.Sort(StringComparer.Ordinal);
        return kept.Count == 0 ? path : path + "?" + string.Join("&", kept);
    }

    public static string WithSite(string path, string? siteKey)
    {
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return path;
        }

        var cut = path.IndexOf('?', StringComparison.Ordinal);
        var bare = cut < 0 ? path : path[..cut];
        var query = cut < 0 ? "" : path[cut..];
        return ClientErpPath.PrefixErpPath(siteKey, bare) + query;
    }

    private static string? SiteKeyOf(string? path)
    {
        var bare = (path ?? "").Trim();
        var cut = bare.IndexOf('?', StringComparison.Ordinal);
        if (cut >= 0)
        {
            bare = bare[..cut];
        }

        return ClientErpPath.TryParse(bare, out var parsed) && parsed.SiteKey.Length > 0 ? parsed.SiteKey : null;
    }

    private static string StripSite(string path)
    {
        var query = "";
        var cut = path.IndexOf('?', StringComparison.Ordinal);
        var bare = cut < 0 ? path : path[..cut];
        if (cut >= 0)
        {
            query = path[cut..];
        }

        if (!ClientErpPath.TryParse(bare, out var parsed) || parsed.SiteKey.Length == 0 || parsed.Rest.Length == 0)
        {
            return path;
        }

        return "/" + parsed.Rest + query;
    }

    private static bool AllowedValue(string key, string value)
    {
        if (value.Length == 0 || value.Length > 40)
        {
            return false;
        }

        if (key == "dash_profile")
        {
            return ErpNetsuiteDashboardCatalog.Profiles.ContainsKey(value);
        }

        if (key is "year")
        {
            return value.Length == 4 && value.All(char.IsDigit);
        }

        if (key is "company" or "propertyId" or "unitId" or "fetch")
        {
            return value.All(char.IsDigit);
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static void Add(List<ErpLandingChoice> list, string label, string path, string group)
    {
        var normal = Normalize(path);
        if (string.IsNullOrEmpty(normal) || list.Any(choice => Same(choice.Path, normal)))
        {
            return;
        }

        list.Add(new ErpLandingChoice(label, normal, group));
    }

    private static HashSet<string> BuildPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/erp",
            "/erp/app",
        };
        foreach (var field in typeof(EcomAeRoutes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(string) || field.GetValue(null) is not string value)
            {
                continue;
            }

            if (!value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase) || value.Contains('?', StringComparison.Ordinal))
            {
                continue;
            }

            if (value.Contains("/ajax", StringComparison.OrdinalIgnoreCase)
                || value.Contains("/save", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("/report", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("/document", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("/workbook", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("/export", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            set.Add(value.TrimEnd('/'));
        }

        foreach (var area in ErpProcessFlowCatalog.Areas)
        {
            foreach (var step in area.Steps.Concat(area.JewellerySteps))
            {
                var path = (step.Href ?? "").Split('?', 2)[0].TrimEnd('/');
                if (path.StartsWith("/erp", StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(path.Length == 0 ? "/erp" : path);
                }
            }
        }

        return set;
    }
}
