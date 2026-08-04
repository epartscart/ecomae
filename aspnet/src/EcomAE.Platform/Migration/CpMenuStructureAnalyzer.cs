using System.Text.Json;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Summarizes legacy PHP <c>menu.structure</c> JSON without returning the raw tree.
/// Writes remain PHP menu_manager / menu_edit.
/// </summary>
public static class CpMenuStructureAnalyzer
{
    public static CpMenuStructureSummary Analyze(string? structureJson)
    {
        if (string.IsNullOrWhiteSpace(structureJson))
        {
            return new CpMenuStructureSummary(
                StructurePresent: false,
                StructureParseOk: true,
                NodeCount: 0,
                MaxDepth: 0,
                UrlLinkCount: 0,
                ContentLinkCount: 0,
                UnknownLinkCount: 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(structureJson);
            var root = doc.RootElement;
            if (root.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            {
                return new CpMenuStructureSummary(true, false, 0, 0, 0, 0, 0);
            }

            var nodeCount = 0;
            var maxDepth = 0;
            var urlLinks = 0;
            var contentLinks = 0;
            var unknownLinks = 0;
            Walk(root, depth: 1, ref nodeCount, ref maxDepth, ref urlLinks, ref contentLinks, ref unknownLinks);
            return new CpMenuStructureSummary(
                StructurePresent: true,
                StructureParseOk: true,
                NodeCount: nodeCount,
                MaxDepth: maxDepth,
                UrlLinkCount: urlLinks,
                ContentLinkCount: contentLinks,
                UnknownLinkCount: unknownLinks);
        }
        catch (JsonException)
        {
            return new CpMenuStructureSummary(true, false, 0, 0, 0, 0, 0);
        }
    }

    private static void Walk(
        JsonElement element,
        int depth,
        ref int nodeCount,
        ref int maxDepth,
        ref int urlLinks,
        ref int contentLinks,
        ref int unknownLinks)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                Walk(child, depth, ref nodeCount, ref maxDepth, ref urlLinks, ref contentLinks, ref unknownLinks);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Treat objects with link_mode / value / caption-like keys as menu nodes.
        var looksLikeNode =
            element.TryGetProperty("link_mode", out _)
            || element.TryGetProperty("value", out _)
            || element.TryGetProperty("caption", out _)
            || element.TryGetProperty("name", out _);

        if (looksLikeNode)
        {
            nodeCount++;
            if (depth > maxDepth)
            {
                maxDepth = depth;
            }

            if (element.TryGetProperty("link_mode", out var modeEl))
            {
                var mode = modeEl.ValueKind == JsonValueKind.String
                    ? modeEl.GetString() ?? string.Empty
                    : string.Empty;
                if (string.Equals(mode, "url", StringComparison.OrdinalIgnoreCase))
                {
                    urlLinks++;
                }
                else if (string.Equals(mode, "content", StringComparison.OrdinalIgnoreCase))
                {
                    contentLinks++;
                }
                else if (!string.IsNullOrWhiteSpace(mode))
                {
                    unknownLinks++;
                }
            }
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                // Common child keys: children, items, nodes — walk any nested object/array.
                Walk(prop.Value, depth + (looksLikeNode ? 1 : 0), ref nodeCount, ref maxDepth, ref urlLinks, ref contentLinks, ref unknownLinks);
            }
        }
    }
}

public sealed record CpMenuStructureSummary(
    bool StructurePresent,
    bool StructureParseOk,
    int NodeCount,
    int MaxDepth,
    int UrlLinkCount,
    int ContentLinkCount,
    int UnknownLinkCount);
