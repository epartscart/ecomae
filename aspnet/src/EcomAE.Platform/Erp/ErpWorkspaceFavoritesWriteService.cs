namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>erp_fav_add</c> / <c>erp_fav_remove</c> twins. Schema ensure and shortcut CRUD stay PHP.</summary>
public interface IErpWorkspaceFavoritesWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(int userId, string? areaKey, string? tabKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RemoveAsync(int userId, string? tabKey, CancellationToken cancellationToken = default);
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
