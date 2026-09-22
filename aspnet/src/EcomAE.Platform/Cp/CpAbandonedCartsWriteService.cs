using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_delete_cart_record.php</c> twin: type-1 lines release their
/// <c>shop_carts_details</c> reservations back to <c>shop_storages_data</c>
/// (<c>deleteRecordType1</c>), type-2 lines are deleted directly (<c>deleteRecordType2</c>).
/// </summary>
public interface ICpAbandonedCartsWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteManyAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);
}

public sealed class CpAbandonedCartsWriteService : ICpAbandonedCartsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpAbandonedCartsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static IReadOnlyList<long> ParseIds(string? csv)
        => (csv ?? "").Split([',', ' ', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToList();

    public Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
        => DeleteManyAsync(id > 0 ? [id] : [], cancellationToken);

    public async Task<ErpSimpleWriteResult> DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cart line id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var writes = 0;
            var deleted = 0;
            foreach (var id in ids)
            {
                var productType = await ErpDb.LongAsync(
                    connection, tx,
                    ErpDb.Positional("SELECT IFNULL(`product_type`,0) FROM `shop_carts` WHERE `id`=?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                if (productType <= 0)
                {
                    continue;
                }

                if (productType == 1)
                {
                    var details = new List<(long DetailId, long StorageRecordId, decimal Reserved)>();
                    await using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`storage_record_id`,0), IFNULL(`count_reserved`,0) FROM `shop_carts_details` WHERE `cart_record_id` = ?");
                        ErpDb.AddParameters(cmd, id);
                        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            details.Add((
                                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                                Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture)));
                        }
                    }

                    foreach (var d in details)
                    {
                        writes += await ErpDb.ExecuteAsync(
                            connection, tx,
                            ErpDb.Positional("UPDATE `shop_storages_data` SET `exist` = `exist`+?, `reserved` = `reserved`-? WHERE `id` = ?"),
                            cancellationToken,
                            d.Reserved, d.Reserved, d.StorageRecordId).ConfigureAwait(false);
                        writes += await ErpDb.ExecuteAsync(
                            connection, tx,
                            ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `id` = ?"),
                            cancellationToken,
                            d.DetailId).ConfigureAwait(false);
                    }
                }

                var n = await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional("DELETE FROM `shop_carts` WHERE `id`=?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                writes += n;
                deleted += n;
            }

            if (deleted == 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Cart line not found");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", deleted == 1 ? "Cart line deleted." : $"Deleted {deleted} cart line(s).", ids[0], writes);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Cart tables are missing or the delete failed.");
        }
    }
}
