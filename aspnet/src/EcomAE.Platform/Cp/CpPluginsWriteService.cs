using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPluginsActivateRequest(string? PluginsList, long PluginId, int FlagValue);

public interface ICpPluginsWriteService
{
    Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>plugins_manager.php</c> <c>plugins_action_type=activated</c>. Delete and filesystem remove stay Classic. Plugin 10 (backend 2FA) activate stays Classic.</summary>
public sealed class CpPluginsWriteService : ICpPluginsWriteService
{
    public const long BackendTwoFactorPluginId = 10;

    private readonly IErpWriteConnectionFactory _connections;

    public CpPluginsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static IReadOnlyList<long> ParsePluginIds(string? pluginsList, long pluginId)
    {
        var ids = new List<long>();
        if (pluginId > 0)
        {
            ids.Add(pluginId);
        }

        var raw = (pluginsList ?? string.Empty).Trim();
        if (raw.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) && n > 0)
                        {
                            ids.Add(n);
                        }
                        else if (el.ValueKind == JsonValueKind.String
                                 && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                                 && parsed > 0)
                        {
                            ids.Add(parsed);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return ids.Count > 0 ? ids.Distinct().ToArray() : [];
            }
        }

        return ids.Distinct().ToArray();
    }

    public static int NormalizeFlag(int flagValue)
        => flagValue == 0 ? 0 : 1;

    public async Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, CancellationToken cancellationToken = default)
    {
        var ids = ParsePluginIds(request.PluginsList, request.PluginId);
        if (ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one plugin id is required.");
        }

        var flag = NormalizeFlag(request.FlagValue);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "plugins", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "plugins table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            foreach (var id in ids)
            {
                var lockFlag = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(`control_lock`, 0) FROM `plugins` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `plugins` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Plugin was not found.");
                }

                if (lockFlag != 0)
                {
                    return ErpSimpleWriteResult.Fail("locked", "This plugin is locked. Lock changes stay on the Classic twin.");
                }

                if (id == BackendTwoFactorPluginId && flag == 1)
                {
                    return ErpSimpleWriteResult.Fail(
                        "two_factor",
                        "Backend 2FA plugin activate stays on the Classic twin until notify debug is ported.");
                }
            }

            var placeholders = string.Join(" OR ", ids.Select((_, i) => "`id` = @p" + (i + 1).ToString(CultureInfo.InvariantCulture)));
            var sql = "UPDATE `plugins` SET `activated` = @p0 WHERE " + placeholders;
            var args = new object?[ids.Count + 1];
            args[0] = flag;
            for (var i = 0; i < ids.Count; i++)
            {
                args[i + 1] = ids[i];
            }

            var writes = await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken, args).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("unchanged", "Plugin activation was not updated.");
            }

            return ErpSimpleWriteResult.Ok(
                flag == 1 ? "Plugin activated." : "Plugin deactivated.",
                ids[0]);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
