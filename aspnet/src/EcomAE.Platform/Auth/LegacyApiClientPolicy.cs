using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Auth;

public static partial class LegacyApiClientPolicy
{
    public static bool ProductAllowed(LegacyApiClientRecord client, string neededProduct)
    {
        if (string.Equals(neededProduct, "both", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(client.Product, "both", StringComparison.OrdinalIgnoreCase)
            || string.Equals(client.Product, neededProduct, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ActionAllowed(LegacyApiClientRecord client, string action)
    {
        var allowed = ParseAllowedActions(client.AllowedActionsJson);
        return allowed.Count == 0 || allowed.Contains(NormalizeAction(action));
    }

    public static bool QuotaAvailable(LegacyApiClientRecord client)
    {
        var limit = Math.Max(1, client.DailyLimit);
        return client.CallsToday < limit;
    }

    public static IReadOnlySet<string> ParseAllowedActions(string? jsonOrList)
    {
        var value = jsonOrList?.Trim() ?? string.Empty;
        if (value.Length == 0 || value == "*")
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (value.StartsWith('[', StringComparison.Ordinal))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(value) ?? [];
                return parsed.Select(NormalizeAction).Where(static item => item.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        return value.Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeAction)
            .Where(static item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeAction(string action)
    {
        return ActionCleaner().Replace(action.ToLowerInvariant(), string.Empty);
    }

    [GeneratedRegex("[^a-z0-9_]")]
    private static partial Regex ActionCleaner();
}
