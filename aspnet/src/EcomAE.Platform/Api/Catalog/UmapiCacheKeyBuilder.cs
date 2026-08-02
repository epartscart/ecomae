using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Mirrors PHP <c>epc_cache_key</c> / <c>epc_normalize_vin</c>.
/// </summary>
public static partial class UmapiCacheKeyBuilder
{
    public static string Build(string action, string section, string language, string region, IReadOnlyDictionary<string, object?> parameters)
    {
        // PHP json_encode([]) => "[]"; json_encode(assoc) => object. Match both.
        var json = parameters.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(parameters
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value));
        var material = $"{action}|{section}|{language}|{region}|{json}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    public static string NormalizeVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return string.Empty;
        }

        return AlnumCleaner().Replace(vin.ToUpperInvariant(), string.Empty);
    }

    /// <summary>Mirrors PHP <c>epc_normalize_engine_code</c> (same character class as VIN normalize).</summary>
    public static string NormalizeEngineCode(string? code) => NormalizeVin(code);

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex AlnumCleaner();
}
