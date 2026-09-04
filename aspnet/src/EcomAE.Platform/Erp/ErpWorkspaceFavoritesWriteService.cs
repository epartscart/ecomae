namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>erp_fav_add</c> / <c>erp_fav_remove</c> / <c>shortcut_delete</c> / <c>shortcut_delete_key</c> / <c>shortcut_reset</c> twins. Add/reorder stay PHP.</summary>
public interface IErpWorkspaceFavoritesWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(int userId, string? areaKey, string? tabKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RemoveAsync(int userId, string? tabKey, CancellationToken cancellationToken = default);

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
