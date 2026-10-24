using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Per-user tenant personalization (ecom-style "personalization" layer): each
/// signed-in ERP user stores their own widget visibility/layout choices in the
/// tenant database under <c>epc_erp_user_prefs</c>. Currently used for the
/// executive dashboard widget hide/show; the table is generic so other
/// surfaces (grids, forms) can reuse it. Tenant admin defaults can later ship
/// as group-level rows — read order: user row &gt; group row &gt; default.
/// </summary>
public interface IErpUserPrefsService
{
    /// <summary>User-hidden ∪ group-default-hidden — read order: user row wins, then group row.</summary>
    Task<IReadOnlySet<string>> HiddenWidgetsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> SetWidgetHiddenAsync(int userId, string widgetKey, bool hidden, CancellationToken cancellationToken = default);
    /// <summary>Tenant-admin group default (user_id=0 row): applies to users with no own row.</summary>
    Task<bool> SetGroupDefaultHiddenAsync(string widgetKey, bool hidden, CancellationToken cancellationToken = default);
}

public sealed class ErpUserPrefsService : IErpUserPrefsService
{
    /// <summary>Dashboard widget keys a user may hide — allowlist enforced on write.</summary>
    public static readonly IReadOnlySet<string> WidgetKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "hero", "industry-kpis", "exec-kpis", "financial-position",
            "process-flow", "working-capital", "approval-queue",
        };

    private const string PrefPrefix = "dash.hide:";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpUserPrefsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private async Task<DbConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_user_prefs` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`user_id` int(11) NOT NULL DEFAULT 0,"
            + "`pref_key` varchar(80) NOT NULL DEFAULT '',"
            + "`pref_value` varchar(255) NOT NULL DEFAULT '',"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "UNIQUE KEY `uk_user_pref` (`user_id`,`pref_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-user personalization (widget visibility, layout)'",
            cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public async Task<IReadOnlySet<string>> HiddenWidgetsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        if (userId <= 0)
        {
            return hidden;
        }

        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return hidden;
            }

            // Read order: user row wins; a group default (user_id=0) applies only
            // when the user has no row of their own for that widget.
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `user_id`,`pref_key`,`pref_value` FROM `epc_erp_user_prefs`"
                + " WHERE `user_id` IN (0,?) AND `pref_key` LIKE 'dash.hide:%'");
            ErpDb.AddParameters(command, userId);
            var own = new Dictionary<string, string>(StringComparer.Ordinal);
            var group = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetString(1);
                if (!key.StartsWith(PrefPrefix, StringComparison.Ordinal)) { continue; }
                var widget = key[PrefPrefix.Length..];
                var value = reader.GetString(2);
                if (reader.GetInt32(0) == 0) { group[widget] = value; }
                else { own[widget] = value; }
            }
            foreach (var pair in group) { if (pair.Value == "1") { hidden.Add(pair.Key); } }
            foreach (var pair in own)
            {
                if (pair.Value == "1") { hidden.Add(pair.Key); }
                else { hidden.Remove(pair.Key); }
            }
        }
        catch (DbException)
        {
            // Preference read failure must never break the dashboard.
        }

        return hidden;
    }

    public async Task<bool> SetWidgetHiddenAsync(int userId, string widgetKey, bool hidden, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(widgetKey) || !WidgetKeys.Contains(widgetKey.Trim()))
        {
            return false;
        }

        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return false;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_user_prefs` (`user_id`,`pref_key`,`pref_value`,`time_updated`) VALUES (?,?,?,?)"
                    + " ON DUPLICATE KEY UPDATE `pref_value`=VALUES(`pref_value`), `time_updated`=VALUES(`time_updated`)"),
                cancellationToken,
                userId,
                PrefPrefix + widgetKey.Trim(),
                hidden ? "1" : "0",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public Task<bool> SetGroupDefaultHiddenAsync(string widgetKey, bool hidden, CancellationToken cancellationToken = default)
    {
        // Group defaults live on the synthetic user_id=0 row.
        return WriteRowAsync(0, widgetKey, hidden, cancellationToken);
    }

    private async Task<bool> WriteRowAsync(int userId, string widgetKey, bool hidden, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(widgetKey) || !WidgetKeys.Contains(widgetKey.Trim()))
        {
            return false;
        }
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null) { return false; }
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_user_prefs` (`user_id`,`pref_key`,`pref_value`,`time_updated`) VALUES (?,?,?,?)"
                    + " ON DUPLICATE KEY UPDATE `pref_value`=VALUES(`pref_value`), `time_updated`=VALUES(`time_updated`)"),
                cancellationToken,
                userId,
                PrefPrefix + widgetKey.Trim(),
                hidden ? "1" : "0",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
