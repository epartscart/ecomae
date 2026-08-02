using System.Text.Json;

namespace EcomAE.Platform.Migration;

internal static class PhpDecommissionEvidence
{
    public static bool HasAuthenticatedPriceLookupSmoke(string root)
    {
        var path = Path.Combine(root, "staging-smoke", "price-lookup-aspnet.json");
        if (!TryReadJson(path, out var doc))
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out var code)
            && code.GetString() is "missing_api_key" or "unauthorized" or "invalid_api_key")
        {
            return false;
        }

        return doc.RootElement.ValueKind == JsonValueKind.Object;
    }

    public static bool HasCatalogStatusSmoke(string root)
    {
        var path = Path.Combine(root, "staging-smoke", "catalog-status-aspnet.json");
        if (!TryReadJson(path, out var doc))
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object)
        {
            return false;
        }

        return doc.RootElement.ValueKind == JsonValueKind.Object;
    }

    public static bool HasSurfaceDigestSmoke(string root)
    {
        var path = Path.Combine(root, "staging-smoke", "surface-digests-aspnet.json");
        if (!TryReadJson(path, out var doc))
        {
            return false;
        }

        if (!doc.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            return false;
        }

        return doc.RootElement.TryGetProperty("routes", out var routes)
            && routes.ValueKind == JsonValueKind.Array
            && routes.GetArrayLength() > 0;
    }

    public static bool HasParitySamples(string root)
    {
        var dir = Path.Combine(root, "parity-samples");
        return Directory.Exists(dir)
            && Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Any();
    }

    public static bool HasReleaseOwnerApproval(string root)
    {
        var path = Path.Combine(root, "RELEASE_OWNER_APPROVAL.md");
        return File.Exists(path)
            && File.ReadAllText(path).Contains("APPROVED_TO_REMOVE_PHP_FALLBACK", StringComparison.Ordinal);
    }

    private static bool TryReadJson(string path, out JsonDocument doc)
    {
        doc = null!;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 2)
            {
                return false;
            }

            doc = JsonDocument.Parse(bytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
