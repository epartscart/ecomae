using System.Data.Common;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin of the read-only ajax_erp.php case <c>shortcut_list</c>
/// (<c>epc_shortcuts_list</c> / <c>epc_shortcuts_list_for_surface</c> + <c>epc_shortcuts_as_tiles</c>).
/// Never creates schema; an unprovisioned <c>epc_user_shortcuts</c> table fails closed.
/// </summary>
public interface IErpShortcutReadService
{
    Task<ErpAjaxRowsResult> ListAsync(int userId, string? surface, CancellationToken cancellationToken = default);
}

public sealed partial class ErpShortcutReadService : IErpShortcutReadService
{
    public const string NotProvisioned = "User shortcuts table is not provisioned";

    public static readonly IReadOnlyList<string> Tones =
        ["red", "black", "crimson", "stone", "blue", "teal", "amber", "violet", "indigo", "emerald", "rose", "slate"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpShortcutReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAjaxRowsResult> ListAsync(int userId, string? surface, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return new(ErpSimpleWriteResult.Fail("auth", "No user session"), []);
        }

        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_user_shortcuts'"),
            cancellationToken).ConfigureAwait(false) > 0;
        if (!exists)
        {
            return new(ErpSimpleWriteResult.Fail("invalid", NotProvisioned), []);
        }

        var s = (surface ?? "both").Trim();
        var rows = s is "cp" or "erp"
            ? await RowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_user_shortcuts` WHERE `user_id` = ? AND (`surface` = ? OR `surface` = 'both') ORDER BY `sort_order` ASC, `time_created` ASC"), cancellationToken, userId, s).ConfigureAwait(false)
            : await RowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_user_shortcuts` WHERE `user_id` = ? ORDER BY `sort_order` ASC, `time_created` ASC"), cancellationToken, userId).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok("OK", 0), AsTiles(rows));
    }

    /// <summary>PHP <c>epc_shortcuts_as_tiles</c>: normalises <c>fa-x</c> → <c>fa fa-x</c>, bare icon, cyclic tone.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> AsTiles(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var tiles = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        var i = 0;
        foreach (var row in rows)
        {
            var icon = (Str(row, "icon_class") ?? "fa fa-star").Trim();
            if (icon.Length == 0)
            {
                icon = "fa fa-star";
            }

            if (!icon.StartsWith("fa ", StringComparison.Ordinal) && icon.StartsWith("fa-", StringComparison.Ordinal))
            {
                icon = "fa " + icon;
            }

            tiles.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Convert.ToInt64(row.GetValueOrDefault("id") ?? 0L, System.Globalization.CultureInfo.InvariantCulture),
                ["key"] = Str(row, "shortcut_key") ?? string.Empty,
                ["label"] = Str(row, "label") ?? "Shortcut",
                ["icon"] = FaPrefix().Replace(icon, string.Empty),
                ["icon_class"] = icon,
                ["color"] = Str(row, "icon_color") ?? "#3498db",
                ["url"] = Str(row, "target_url") ?? "#",
                ["tone"] = Tones[i % Tones.Count],
            });
            i++;
        }

        return tiles;
    }

    private static string? Str(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    [GeneratedRegex("^fa\\s+")]
    private static partial Regex FaPrefix();

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RowsAsync(DbConnection connection, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var map = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(map);
        }

        return rows;
    }
}
