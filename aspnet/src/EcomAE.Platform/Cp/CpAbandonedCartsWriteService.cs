using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_delete_cart_record.php</c> twin of admin type-2
/// <c>deleteRecordType2</c>. Catalogue type-1 reserve release stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpAbandonedCartsWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class CpAbandonedCartsWriteService : ICpAbandonedCartsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpAbandonedCartsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
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
            var productType = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(`product_type`,0) FROM `shop_carts` WHERE `id`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
            if (productType <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Cart line not found");
            }

            if (productType != 2)
            {
                return ErpSimpleWriteResult.Fail("classic", "Catalogue reserve release stay Classic.");
            }

            var writes = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `shop_carts` WHERE `id`=? AND `product_type`=2"),
                cancellationToken,
                id).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Cart line not found");
            }

            return ErpSimpleWriteResult.Ok("Cart line deleted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Cart table is missing — schema-ensure stays Classic.");
        }
    }
}
