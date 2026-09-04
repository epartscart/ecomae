using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP logistics groups <c>add_group</c> / <c>del</c> twins. Membership UI stay PHP.</summary>
public interface ICpStorageGroupWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(string? name, string? storages, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long groupId, CancellationToken cancellationToken = default);
}

public sealed class CpStorageGroupWriteService : ICpStorageGroupWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpStorageGroupWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        string? name,
        string? storages,
        CancellationToken cancellationToken = default)
    {
        var caption = (name ?? string.Empty).Trim();
        var ids = ParseStorageIds(storages);
        if (caption.Length == 0 || ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Name and warehouses required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var nextOrder = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`order`), 0) FROM `shop_storages_groups`"),
            cancellationToken) + 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_storages_groups` (`name`, `storages`, `order`) VALUES (?, ?, ?)"),
            cancellationToken,
            caption, string.Join(',', ids), nextOrder);
        return ErpSimpleWriteResult.Ok("Storage group added.", 0);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A storage group id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_storages_groups` WHERE `id` = ?"),
            cancellationToken,
            groupId);
        return ErpSimpleWriteResult.Ok("Storage group deleted.", groupId);
    }

    internal static IReadOnlyList<long> ParseStorageIds(string? raw)
    {
        var ids = new List<long>();
        foreach (var part in (raw ?? string.Empty).Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part, out var id) && id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
