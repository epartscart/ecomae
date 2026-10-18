namespace EcomAE.Platform.Presentation;

/// <summary>
/// Action-pane tabs for one ERP app. Aliases that open the same page collapse
/// into one default view. A route with <c>?tab=</c> is its own view.
/// </summary>
public static class ErpActionPaneViews
{
    public sealed record View(string Key, string Label, string Href, bool IsDefault);

    public static IReadOnlyList<View> ForPath(string? appPath)
    {
        var path = PhpParityDumpCatalog.NormalizePath(appPath);
        var slug = AppSlug(path);
        if (slug.Length == 0)
        {
            return [new View("home", "Home", "/erp", true)];
        }

        var rows = ErpPhpTabRouteMap.All
            .Where(pair => AppSlug(PhpParityDumpCatalog.NormalizePath(pair.Value)) == slug)
            .Select(pair => (Key: pair.Key, Href: pair.Value, Tab: QueryTab(pair.Value)))
            .ToList();

        if (rows.Count == 0)
        {
            return [new View("home", "Home", path.StartsWith("/erp", StringComparison.OrdinalIgnoreCase) ? path : "/erp", true)];
        }

        var views = new List<View>();
        var defaults = rows.Where(row => string.IsNullOrEmpty(row.Tab)).ToList();
        if (defaults.Count > 0)
        {
            var pick = defaults.OrderByDescending(row => row.Key.Length).First();
            views.Add(new View(pick.Key, LabelFor(pick.Key), BarePath(pick.Href), true));
        }

        foreach (var group in rows.Where(row => !string.IsNullOrEmpty(row.Tab)).GroupBy(row => row.Tab, StringComparer.OrdinalIgnoreCase))
        {
            var pick = group.OrderByDescending(row => row.Key.Length).First();
            views.Add(new View(pick.Tab!, LabelFor(pick.Key), pick.Href, false));
        }

        if (!views.Any(view => view.IsDefault) && views.Count > 0)
        {
            var first = views[0];
            views[0] = first with { IsDefault = true };
        }

        return views
            .OrderByDescending(view => view.IsDefault)
            .ThenBy(view => view.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ActiveKey(IReadOnlyList<View> views, string? requestedTab)
    {
        var key = (requestedTab ?? string.Empty).Trim();
        var match = views.FirstOrDefault(view => key.Length > 0 && view.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.Key;
        }

        return views.FirstOrDefault(view => view.IsDefault)?.Key
            ?? views.FirstOrDefault()?.Key
            ?? "home";
    }

    public static string LabelFor(string key)
    {
        var title = ErpPhpModuleChromeCatalog.ForTab(key).Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Human(key);
        }

        return Relax(title);
    }

    private static string QueryTab(string href)
    {
        var query = href.IndexOf('?', StringComparison.Ordinal);
        if (query < 0)
        {
            return string.Empty;
        }

        foreach (var part in href[(query + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            if (part[..eq].Equals("tab", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(part[(eq + 1)..]);
            }
        }

        return string.Empty;
    }

    private static string BarePath(string href)
    {
        var query = href.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? href : href[..query];
    }

    private static string AppSlug(string path)
    {
        foreach (var shell in new[] { "/cp/", "/erp/" })
        {
            if (path.StartsWith(shell, StringComparison.OrdinalIgnoreCase))
            {
                return path[shell.Length..];
            }
        }

        return path;
    }

    private static string Human(string key)
        => string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "ap", "ar", "gl", "po", "vat", "hr", "rfid", "wms", "aml", "ncr", "bu", "sku", "id", "coa"
    };

    private static string Relax(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var prose = words.Any(word => word.Length > 2 && char.IsLower(word[0]));
        if (!prose)
        {
            for (var i = 1; i < words.Length; i++)
            {
                var bare = words[i].Trim('&', '/', '-', '(', ')');
                if (bare.Length > 1 && !Acronyms.Contains(bare))
                {
                    words[i] = words[i].Replace(bare, char.ToLowerInvariant(bare[0]) + bare[1..].ToLowerInvariant(), StringComparison.Ordinal);
                }
            }
        }

        for (var i = 0; i < words.Length; i++)
        {
            var bare = words[i].Trim('&', '/', '-', '(', ')');
            if (Acronyms.Contains(bare))
            {
                words[i] = words[i].Replace(bare, bare.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);
            }
        }

        return string.Join(' ', words);
    }
}
