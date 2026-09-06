using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>reviews.php</c> delete twin. Storefront review create stays on the
/// customer write path. There is no CP create/edit in PHP.
/// </summary>
public interface ICpCatalogueReviewWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long reviewId, CancellationToken cancellationToken = default);
}

public sealed class CpCatalogueReviewWriteService : ICpCatalogueReviewWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogueReviewWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeAction(string? action)
    {
        var key = (action ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "delete" or "delete_review" => "delete",
            _ => key
        };
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long reviewId,
        CancellationToken cancellationToken = default)
    {
        if (reviewId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A review id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_products_evaluations` WHERE `id` = ?"),
            cancellationToken,
            reviewId).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Review deleted.", reviewId)
            : ErpSimpleWriteResult.Fail("not_found", "Review was not found.");
    }
}
