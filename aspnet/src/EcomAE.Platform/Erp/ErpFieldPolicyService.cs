using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Field-level access rules for tenant admins (ecom security model): a rule says a
/// field in an ERP/CP area is <c>hidden</c> or <c>readonly</c> for a user or a whole
/// user group (group rows live on user_id=0 with a group_key). Read order:
/// group rule &lt; user rule — the most specific rule wins. Modules query the
/// resolved mode per field at render time; missing rules mean full access, so the
/// super layer is untouched until a tenant admin sets a rule.
/// </summary>
public interface IErpFieldPolicyService
{
    /// <summary>field_key → mode ("hidden" | "readonly") resolved for this user in an area.</summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(int userId, string groupKey, string area, CancellationToken cancellationToken = default);

    /// <summary>Writes one rule. userId=0 + groupKey targets a user group.</summary>
    Task<bool> SetRuleAsync(int userId, string groupKey, string area, string fieldKey, string mode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpFieldRuleRow>> ListAsync(string area, CancellationToken cancellationToken = default);
}

public sealed record ErpFieldRuleRow(int UserId, string GroupKey, string Area, string FieldKey, string Mode);

public sealed class ErpFieldPolicyService : IErpFieldPolicyService
{
    /// <summary>Only these modes are stored — anything else is rejected.</summary>
    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal) { "hidden", "readonly" };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpFieldPolicyService(IErpWriteConnectionFactory connections)
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
            "CREATE TABLE IF NOT EXISTS `epc_erp_field_rule` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`user_id` int(11) NOT NULL DEFAULT 0,"
            + "`group_key` varchar(64) NOT NULL DEFAULT '',"
            + "`area` varchar(64) NOT NULL DEFAULT '',"
            + "`field_key` varchar(80) NOT NULL DEFAULT '',"
            + "`mode` varchar(16) NOT NULL DEFAULT 'hidden',"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "UNIQUE KEY `uk_field_rule` (`user_id`,`group_key`,`area`,`field_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Field-level access rules (hidden/readonly) per user or group'",
            cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(int userId, string groupKey, string area, CancellationToken cancellationToken = default)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(area))
        {
            return resolved;
        }

        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return resolved;
            }

            await using var command = connection.CreateCommand();
            // Group rules first (least specific), then the user's own rules override.
            command.CommandText = ErpDb.Positional(
                "SELECT `field_key`,`mode`,`user_id` FROM `epc_erp_field_rule`"
                + " WHERE `area`=? AND ((`user_id`=0 AND `group_key`=?) OR `user_id`=?)"
                + " ORDER BY `user_id` ASC");
            ErpDb.AddParameters(command, area.Trim(), (groupKey ?? string.Empty).Trim(), userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                resolved[reader.GetString(0)] = reader.GetString(1);
            }
        }
        catch (DbException)
        {
            // Policy read failure fails open to full access — never break a form.
        }

        return resolved;
    }

    public async Task<bool> SetRuleAsync(int userId, string groupKey, string area, string fieldKey, string mode, CancellationToken cancellationToken = default)
    {
        area = (area ?? string.Empty).Trim();
        fieldKey = (fieldKey ?? string.Empty).Trim();
        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        groupKey = (groupKey ?? string.Empty).Trim();
        if (area.Length is 0 or > 64 || fieldKey.Length is 0 or > 80 || !Modes.Contains(mode) || userId < 0 || groupKey.Length > 64)
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
                    "INSERT INTO `epc_erp_field_rule` (`user_id`,`group_key`,`area`,`field_key`,`mode`,`time_updated`) VALUES (?,?,?,?,?,?)"
                    + " ON DUPLICATE KEY UPDATE `mode`=VALUES(`mode`), `time_updated`=VALUES(`time_updated`)"),
                cancellationToken,
                userId,
                groupKey,
                area,
                fieldKey,
                mode,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ErpFieldRuleRow>> ListAsync(string area, CancellationToken cancellationToken = default)
    {
        var rows = new List<ErpFieldRuleRow>();
        try
        {
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return rows;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `user_id`,`group_key`,`area`,`field_key`,`mode` FROM `epc_erp_field_rule`"
                + " WHERE (?='' OR `area`=?) ORDER BY `area`,`field_key`,`user_id` LIMIT 500");
            ErpDb.AddParameters(command, (area ?? string.Empty).Trim(), (area ?? string.Empty).Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpFieldRuleRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }
}
