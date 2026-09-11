using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>multi_entity</c> <c>add_member</c> / <c>epc_entity_add_member</c>
/// and <c>eliminate</c> / <c>epc_entity_eliminate</c>.
/// Create, intercompany, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/site-key checks.
/// </summary>
public interface IBosEntityWriteService
{
    Task<ErpSimpleWriteResult> AddMemberAsync(
        long groupId,
        string? siteKey,
        string? memberData,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EliminateAsync(
        long groupId,
        CancellationToken cancellationToken = default);
}

public sealed class BosEntityWriteService : IBosEntityWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosEntityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(float)</c> on a token (leading numeric / optional exponent; trailing junk ignored).</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['member_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>entity_name ?? $siteKey</c>, <c>ownership_pct ?? 100</c>, <c>local_currency ?? 'AED'</c>,
    /// <c>consolidation ?? 'full'</c>. Empty string is present and does not take the default.
    /// </summary>
    public static (string EntityName, decimal OwnershipPct, string LocalCurrency, string Consolidation) ParseMemberData(
        string? raw,
        string siteKey)
    {
        var entityName = siteKey;
        var ownershipPct = 100m;
        var localCurrency = "AED";
        var consolidation = "full";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (entityName, ownershipPct, localCurrency, consolidation);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (entityName, ownershipPct, localCurrency, consolidation);
            }

            if (TryGetPresent(doc.RootElement, "entity_name", out var nameEl))
            {
                entityName = JsonString(nameEl);
            }

            if (TryGetPresent(doc.RootElement, "ownership_pct", out var pctEl))
            {
                ownershipPct = PhpFloat(pctEl);
            }

            if (TryGetPresent(doc.RootElement, "local_currency", out var currencyEl))
            {
                localCurrency = JsonString(currencyEl);
            }

            if (TryGetPresent(doc.RootElement, "consolidation", out var methodEl))
            {
                consolidation = JsonString(methodEl);
            }

            return (entityName, ownershipPct, localCurrency, consolidation);
        }
        catch (JsonException)
        {
            return (siteKey, 100m, "AED", "full");
        }
    }

    private static bool TryGetPresent(JsonElement root, string name, out JsonElement element)
    {
        if (root.TryGetProperty(name, out element)
            && element.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return true;
        }

        element = default;
        return false;
    }

    private static decimal PhpFloat(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.Number when element.TryGetDouble(out var n) => (decimal)n,
            JsonValueKind.String => PhpFloat(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    private static string JsonString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "",
            _ => ""
        };

    public async Task<ErpSimpleWriteResult> AddMemberAsync(
        long groupId,
        string? siteKey,
        string? memberData,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var key = PhpBosSiteKey(siteKey);
        var parsed = ParseMemberData(memberData, key);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_entity_members` (`group_id`,`site_key`,`entity_name`,`ownership_pct`,`local_currency`,`consolidation`) VALUES (?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `ownership_pct`=VALUES(`ownership_pct`)
                    """),
                cancellationToken,
                groupId,
                key,
                parsed.EntityName,
                parsed.OwnershipPct,
                parsed.LocalCurrency,
                parsed.Consolidation).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Entity member saved", groupId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Entity member table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> EliminateAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var eliminated = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_intercompany_txns` SET `status`='eliminated' WHERE `group_id`=? AND `status`='matched'
                    """),
                cancellationToken, groupId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Eliminated " + eliminated.ToString(CultureInfo.InvariantCulture) + " matched inter-company row(s).",
                groupId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Intercompany table is missing — schema-ensure stays Classic.");
        }
    }
}
