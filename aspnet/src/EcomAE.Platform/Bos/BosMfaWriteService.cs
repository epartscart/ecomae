using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>mfa_policy</c> <c>save</c> / <c>epc_mfa_save_policy</c>.
/// Enroll, verify, and schema-ensure stay Classic. Dedicated <c>/bos/ajax/mfa-policy</c> dry-run stays refuse-confirm.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// </summary>
public interface IBosMfaWriteService
{
    Task<ErpSimpleWriteResult> SavePolicyAsync(
        string? tenantKey,
        string? rolesJson,
        string? pathsJson,
        string? gracePeriodHours,
        CancellationToken cancellationToken = default);
}

public sealed class BosMfaWriteService : IBosMfaWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosMfaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(int)</c> on a leading optional sign + digits token.</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode(..., true)</c> then <c>is_array(...) ? ... : array()</c> then <c>json_encode</c>.
    /// Missing/null/invalid/empty-object becomes <c>[]</c>.
    /// </summary>
    public static string EncodeJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var el = doc.RootElement;
            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.GetRawText();
            }

            if (el.ValueKind == JsonValueKind.Object && !el.EnumerateObject().Any())
            {
                return "[]";
            }

            if (el.ValueKind == JsonValueKind.Object)
            {
                return el.GetRawText();
            }

            return "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    /// <summary>PHP <c>(string)($_POST['tenant_key'] ?? '__platform__')</c> — omitted uses default; posted empty stays empty.</summary>
    public static string PhpTenantKey(string? raw)
        => raw ?? "__platform__";

    /// <summary>PHP <c>(int)($_POST['grace_period_hours'] ?? 72)</c> — omitted uses 72; posted empty is 0.</summary>
    public static long PhpGraceHours(string? raw)
        => raw is null ? 72 : PhpIntval(raw);

    public async Task<ErpSimpleWriteResult> SavePolicyAsync(
        string? tenantKey,
        string? rolesJson,
        string? pathsJson,
        string? gracePeriodHours,
        CancellationToken cancellationToken = default)
    {
        var key = PhpTenantKey(tenantKey);
        var roles = EncodeJsonArray(rolesJson);
        var paths = EncodeJsonArray(pathsJson);
        var grace = PhpGraceHours(gracePeriodHours);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_mfa_policy` (`tenant_key`, `require_mfa_for_roles`, `require_mfa_for_paths`, `grace_period_hours`)
                    VALUES (?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE `require_mfa_for_roles` = VALUES(`require_mfa_for_roles`),
                    `require_mfa_for_paths` = VALUES(`require_mfa_for_paths`),
                    `grace_period_hours` = VALUES(`grace_period_hours`)
                    """),
                cancellationToken, key, roles, paths, grace).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("MFA policy saved", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "MFA policy table is missing — schema-ensure stays Classic.");
        }
    }
}
