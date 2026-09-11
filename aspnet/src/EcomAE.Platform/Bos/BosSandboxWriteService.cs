using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>config_sandbox</c> <c>promote</c> / <c>epc_sandbox_promote</c>,
/// <c>discard</c> / <c>epc_sandbox_discard</c>, <c>apply_change</c> / <c>epc_sandbox_apply_change</c>,
/// and <c>create</c> / <c>epc_sandbox_create</c>.
/// Rollback and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found/only-active checks.
/// </summary>
public interface IBosSandboxWriteService
{
    Task<ErpSimpleWriteResult> PromoteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DiscardAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ApplyChangeAsync(
        long snapshotId,
        string? key,
        string? oldValue,
        string? newValue,
        string? changeType,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? name,
        string? configDataJson,
        long createdBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosSandboxWriteService : IBosSandboxWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosSandboxWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> PromoteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(
            snapshotId,
            """
            UPDATE `epc_config_snapshots` SET `status`='promoted', `promoted_at`=NOW() WHERE `id`=? AND `status`='active'
            """,
            "Sandbox snapshot promoted",
            cancellationToken);

    public Task<ErpSimpleWriteResult> DiscardAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(
            snapshotId,
            """
            UPDATE `epc_config_snapshots` SET `status`='discarded' WHERE `id`=? AND `status`='active'
            """,
            "Sandbox snapshot discarded",
            cancellationToken);

    public async Task<ErpSimpleWriteResult> ApplyChangeAsync(
        long snapshotId,
        string? key,
        string? oldValue,
        string? newValue,
        string? changeType,
        CancellationToken cancellationToken = default)
    {
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
                    INSERT INTO `epc_sandbox_changes` (`snapshot_id`,`change_key`,`old_value`,`new_value`,`change_type`) VALUES (?,?,?,?,?)
                    """),
                cancellationToken,
                snapshotId,
                key ?? "",
                oldValue ?? "",
                newValue ?? "",
                changeType ?? "modify").ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Sandbox change applied", snapshotId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Sandbox table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? name,
        string? configDataJson,
        long createdBy,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var snapshotName = name ?? "Sandbox";
        var configData = SerializeConfigData(configDataJson);
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
                    "INSERT INTO `epc_config_snapshots` (`site_key`,`snapshot_name`,`config_data`,`created_by`) VALUES (?,?,?,?)"),
                cancellationToken, key, snapshotName, configData, createdBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Sandbox snapshot created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Sandbox table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['config_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>json_encode</c>. Empty object becomes <c>[]</c>.
    /// </summary>
    public static string SerializeConfigData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (!doc.RootElement.EnumerateObject().Any())
                {
                    return "[]";
                }

                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            return "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private async Task<ErpSimpleWriteResult> SetStatusAsync(
        long snapshotId,
        string sql,
        string okMessage,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(sql),
                cancellationToken, snapshotId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(okMessage, snapshotId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Sandbox table is missing — schema-ensure stays Classic.");
        }
    }
}
