using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Builds the PHP <c>dp_menu</c> category tree from flat <c>shop_catalogue_categories</c> rows.
/// Filters APAI-synced aliases (ePartsCart warehouse storefront keeps legacy roots only).
/// </summary>
public static class StorefrontOwnCatalogueTreeBuilder
{
    public const string AppBase = "/storefront/own-catalog-app";

    public static IReadOnlyList<StorefrontCatalogueCategoryNode> Build(
        IEnumerable<StorefrontCatalogueCategoryRow> rows,
        bool filterApai = true)
    {
        var byParent = new Dictionary<int, List<StorefrontCatalogueCategoryRow>>();
        foreach (var row in rows)
        {
            if (filterApai && IsApai(row.Alias, row.Url))
            {
                continue;
            }

            if (!byParent.TryGetValue(row.Parent, out var list))
            {
                list = [];
                byParent[row.Parent] = list;
            }

            list.Add(row);
        }

        foreach (var list in byParent.Values)
        {
            list.Sort(static (a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : a.Id.CompareTo(b.Id);
            });
        }

        return BuildChildren(0, byParent);
    }

    public static string LabelFor(string alias, string value, int id)
    {
        var label = (value ?? string.Empty).Trim();
        if (label.Length == 0
            || string.Equals(label, "null", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(label, @"^\?+$"))
        {
            label = HumanizeAlias(alias);
        }

        if (label.Length == 0)
        {
            label = "Category #" + id.ToString(CultureInfo.InvariantCulture);
        }

        return label;
    }

    public static string HrefFor(string url)
    {
        var route = (url ?? string.Empty).Trim().TrimStart('/');
        if (route.Length == 0)
        {
            return AppBase;
        }

        return AppBase + "?url=" + Uri.EscapeDataString(route);
    }

    public static bool IsApai(string? alias, string? url)
    {
        var a = (alias ?? string.Empty).Trim();
        if (a.StartsWith("apai-", StringComparison.OrdinalIgnoreCase)
            || a.StartsWith("apai_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var u = (url ?? string.Empty).Trim().TrimStart('/').ToLowerInvariant();
        return u.StartsWith("apai-", StringComparison.Ordinal)
               || u.StartsWith("apai_", StringComparison.Ordinal)
               || u.Contains("/apai-", StringComparison.Ordinal)
               || u.Contains("/apai_", StringComparison.Ordinal);
    }

    private static List<StorefrontCatalogueCategoryNode> BuildChildren(
        int parentId,
        Dictionary<int, List<StorefrontCatalogueCategoryRow>> byParent)
    {
        if (!byParent.TryGetValue(parentId, out var children) || children.Count == 0)
        {
            return [];
        }

        var nodes = new List<StorefrontCatalogueCategoryNode>(children.Count);
        foreach (var child in children)
        {
            var nested = BuildChildren(child.Id, byParent);
            nodes.Add(new StorefrontCatalogueCategoryNode(
                child.Id,
                child.Alias,
                child.Url,
                child.Parent,
                child.Level,
                nested.Count > 0 ? nested.Count : child.ChildCount,
                child.SortOrder,
                child.Image,
                child.Value,
                HrefFor(child.Url),
                nested));
        }

        return nodes;
    }

    private static string HumanizeAlias(string? alias)
    {
        var slug = (alias ?? string.Empty).Trim();
        if (slug.Length == 0)
        {
            return string.Empty;
        }

        slug = Regex.Replace(slug, @"^apai-[a-z0-9_]+-", "", RegexOptions.IgnoreCase);
        slug = slug.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.ToLowerInvariant());
    }
}
