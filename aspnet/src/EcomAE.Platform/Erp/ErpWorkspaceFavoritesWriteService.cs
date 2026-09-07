namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>erp_fav_add</c> / <c>erp_fav_remove</c> / <c>shortcut_add</c> / <c>shortcut_reorder</c> / <c>shortcut_delete</c> / <c>shortcut_delete_key</c> / <c>shortcut_reset</c> twins. Schema-ensure stays PHP.</summary>
public interface IErpWorkspaceFavoritesWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(int userId, string? areaKey, string? tabKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RemoveAsync(int userId, string? tabKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddShortcutAsync(
        int userId,
        string? label,
        string? targetUrl,
        string? shortcutKey = null,
        string? surface = null,
        string? iconClass = null,
        string? iconColor = null,
        string? targetTab = null,
        long companyId = 0,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ReorderShortcutsAsync(int userId, IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteShortcutAsync(int userId, long shortcutId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteShortcutByKeyAsync(int userId, string? shortcutKey, string? surface, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ResetShortcutsAsync(int userId, string? surface, CancellationToken cancellationToken = default);
}

public sealed class ErpWorkspaceFavoritesWriteService : IErpWorkspaceFavoritesWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWorkspaceFavoritesWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        int userId,
        string? areaKey,
        string? tabKey,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        if (!TryNormalizeKey(tabKey, required: true, out var tab))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing tab_key");
        }

        if (!TryNormalizeKey(areaKey, required: false, out var area))
        {
            return ErpSimpleWriteResult.Fail("invalid", "area_key is too long.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT IGNORE INTO `epc_erp_favourites` (`user_id`, `area_key`, `tab_key`, `created_at`) VALUES (?,?,?,?)"),
            cancellationToken,
            userId, area, tab, createdAt);
        return ErpSimpleWriteResult.Ok("Added to favourites", userId);
    }

    public async Task<ErpSimpleWriteResult> RemoveAsync(
        int userId,
        string? tabKey,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        if (!TryNormalizeKey(tabKey, required: true, out var tab))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing tab_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_favourites` WHERE `user_id` = ? AND `tab_key` = ?"),
            cancellationToken,
            userId, tab);
        return ErpSimpleWriteResult.Ok("Removed from favourites", userId);
    }

    public async Task<ErpSimpleWriteResult> AddShortcutAsync(
        int userId,
        string? label,
        string? targetUrl,
        string? shortcutKey = null,
        string? surface = null,
        string? iconClass = null,
        string? iconColor = null,
        string? targetTab = null,
        long companyId = 0,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        var trimmedLabel = (label ?? string.Empty).Trim();
        var url = (targetUrl ?? string.Empty).Trim();
        if (trimmedLabel.Length == 0 || url.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Label and URL are required");
        }

        if (IsBlockedUrl(url))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid URL");
        }

        var key = SanitizeShortcutKey(shortcutKey);
        var face = NormalizeSurfaceForAdd(surface);
        var icon = NormalizeIconClass(iconClass);
        var color = NormalizeIconColor(iconColor);
        var tab = Truncate((targetTab ?? string.Empty).Trim(), 50);
        var storedLabel = Truncate(trimmedLabel, 100);
        var storedUrl = Truncate(url, 500);
        var storedIcon = Truncate(icon, 100);
        var storedColor = Truncate(color, 20);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (key.Length > 0)
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_user_shortcuts` WHERE `user_id` = ? AND `shortcut_key` = ? AND (`surface` = ? OR `surface` = 'both') LIMIT 1"),
                cancellationToken,
                userId, key, face).ConfigureAwait(false);
            if (existing > 0)
            {
                return ErpSimpleWriteResult.Ok("Shortcut added", existing);
            }
        }

        var maxSort = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT MAX(`sort_order`) FROM `epc_user_shortcuts` WHERE `user_id` = ?"),
            cancellationToken,
            userId).ConfigureAwait(false);
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_user_shortcuts`
                (`company_id`,`user_id`,`surface`,`shortcut_key`,`label`,`icon_class`,`icon_color`,`target_url`,`target_tab`,`sort_order`,`time_created`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            companyId, userId, face, key, storedLabel, storedIcon, storedColor, storedUrl, tab, maxSort + 1, createdAt);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Shortcut added", id);
    }

    public async Task<ErpSimpleWriteResult> ReorderShortcutsAsync(
        int userId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var list = ids ?? [];
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        for (var order = 0; order < list.Count; order++)
        {
            var id = list[order];
            if (id <= 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_user_shortcuts` SET `sort_order` = ? WHERE `id` = ? AND `user_id` = ?"),
                cancellationToken,
                order, id, userId);
        }

        return ErpSimpleWriteResult.Ok("Order saved", userId);
    }

    public async Task<ErpSimpleWriteResult> DeleteShortcutAsync(
        int userId,
        long shortcutId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        if (shortcutId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing id");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_user_shortcuts` WHERE `id` = ? AND `user_id` = ?"),
            cancellationToken,
            shortcutId, userId);
        return ErpSimpleWriteResult.Ok("Shortcut removed", shortcutId);
    }

    public async Task<ErpSimpleWriteResult> DeleteShortcutByKeyAsync(
        int userId,
        string? shortcutKey,
        string? surface,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        var key = SanitizeShortcutKey(shortcutKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing shortcut_key");
        }

        var face = NormalizeSurface(surface);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (face.Length > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ? AND `shortcut_key` = ? AND (`surface` = ? OR `surface` = 'both')"),
                cancellationToken,
                userId, key, face);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ? AND `shortcut_key` = ?"),
                cancellationToken,
                userId, key);
        }

        return ErpSimpleWriteResult.Ok("Shortcut removed", userId);
    }

    public async Task<ErpSimpleWriteResult> ResetShortcutsAsync(
        int userId,
        string? surface,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "No user session");
        }

        var face = NormalizeSurface(surface);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (face.Length > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ? AND (`surface` = ? OR `surface` = 'both')"),
                cancellationToken,
                userId, face);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ?"),
                cancellationToken,
                userId);
        }

        return ErpSimpleWriteResult.Ok("Shortcuts reset", userId);
    }

    internal static string SanitizeShortcutKey(string? raw)
    {
        var source = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var chars = new char[source.Length];
        var n = 0;
        foreach (var ch in source)
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-')
            {
                chars[n++] = ch;
            }
        }

        return n == 0 ? string.Empty : new string(chars, 0, n);
    }

    internal static string NormalizeSurface(string? raw)
    {
        var face = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return face is "cp" or "erp" ? face : string.Empty;
    }

    internal static string NormalizeSurfaceForAdd(string? raw)
    {
        var face = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return face is "cp" or "erp" or "both" ? face : "both";
    }

    internal static IReadOnlyList<long> ParseShortcutIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var ids = new List<long>();
        foreach (var part in raw.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static bool IsBlockedUrl(string url)
        => url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
           || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIconClass(string? raw)
    {
        var icon = (raw ?? "fa fa-star").Trim();
        if (icon.Length > 0 && !icon.StartsWith("fa ", StringComparison.Ordinal) && icon.StartsWith("fa-", StringComparison.Ordinal))
        {
            icon = "fa " + icon;
        }

        return icon;
    }

    private static string NormalizeIconColor(string? raw)
    {
        var color = (raw ?? "#3498db").Trim();
        return color.Length == 0 || color[0] != '#' ? "#3498db" : color;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    internal static bool TryNormalizeKey(string? raw, bool required, out string key)
    {
        key = (raw ?? string.Empty).Trim();
        if (key.Length > 60)
        {
            key = string.Empty;
            return false;
        }

        return !required || key.Length > 0;
    }
}
