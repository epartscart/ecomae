using System.Net;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP twins: <c>ajax_add_evaluation.php</c> and customer <c>ajax_send_message.php</c>.
/// </summary>
public interface IStorefrontCustomerWriteService
{
    Task<ErpSimpleWriteResult> AddEvaluationAsync(
        int userId,
        long productId,
        int rating,
        string? text,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendOrderMessageAsync(
        int userId,
        long orderId,
        string? text,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCustomerWriteService : IStorefrontCustomerWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontCustomerWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddEvaluationAsync(
        int userId,
        long productId,
        int rating,
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (productId <= 0 || rating is < 1 or > 5)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Product and a rating from 1 to 5 are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Review database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_products_evaluations` WHERE `user_id` = ? AND `product_id` = ?"),
            cancellationToken,
            userId, productId);
        if (exists > 0)
        {
            return ErpSimpleWriteResult.Fail("already", "You already reviewed this product.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = WebUtility.HtmlEncode((text ?? string.Empty).Trim());
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `shop_products_evaluations`
                (`product_id`, `mark`, `text_plus`, `text_minus`, `text`, `user_id`, `time`, `hide_user_data`)
                VALUES (?, ?, '', '', ?, ?, ?, 0)
                """),
            cancellationToken,
            productId, rating, body, userId, now);
        return ErpSimpleWriteResult.Ok("Review submitted.", productId);
    }

    public async Task<ErpSimpleWriteResult> SendOrderMessageAsync(
        int userId,
        long orderId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var body = WebUtility.HtmlEncode((text ?? string.Empty).Trim());
        if (orderId <= 0 || body.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Message text is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Order database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var owned = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            orderId, userId);
        if (owned <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Order not found.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (?, 1, ?, ?, 0)"),
            cancellationToken,
            orderId, body, now);
        return ErpSimpleWriteResult.Ok("Message sent.", orderId);
    }
}
