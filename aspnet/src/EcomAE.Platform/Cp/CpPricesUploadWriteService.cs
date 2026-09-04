using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_6_complete_session</c> last_updated / records_count twin. CSV import and sitemap stay PHP.</summary>
public interface ICpPricesUploadWriteService
{
    Task<ErpSimpleWriteResult> CompleteSessionAsync(long priceId, CancellationToken cancellationToken = default);
}

public sealed class CpPricesUploadWriteService : ICpPricesUploadWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesUploadWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CompleteSessionAsync(
        long priceId,
        CancellationToken cancellationToken = default)
    {
        if (priceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A price list id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            stamp, priceId);

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                cancellationToken,
                priceId);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?"),
                cancellationToken,
                count, priceId);
        }
        catch (Exception)
        {
            // PHP ignores a missing records_count column.
        }

        return ErpSimpleWriteResult.Ok("Price list session completed.", priceId);
    }
}
