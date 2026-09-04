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

    Task<ErpSimpleWriteResult> SubscribeNewsletterAsync(
        string? email,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetUserOptionAsync(
        int userId,
        string? optionKey,
        string? optionValue,
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

    public async Task<ErpSimpleWriteResult> SubscribeNewsletterAsync(
        string? email,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var address = (email ?? string.Empty).Trim();
        if (address.Length == 0 || !address.Contains('@', StringComparison.Ordinal) || address.Contains(' ', StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A valid email is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Newsletter database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_newsletter_subscribers` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `email` VARCHAR(255) NOT NULL,
                `subscribed_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `status` ENUM('active','unsubscribed') NOT NULL DEFAULT 'active',
                `ip_address` VARCHAR(45) DEFAULT NULL,
                `source` VARCHAR(50) DEFAULT 'storefront',
                UNIQUE KEY `uk_email` (`email`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken);
        var ip = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        if (ip is { Length: > 45 })
        {
            ip = ip[..45];
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_newsletter_subscribers` (`email`, `ip_address`, `source`)
                VALUES (?, ?, 'storefront')
                ON DUPLICATE KEY UPDATE `status` = 'active', `subscribed_at` = NOW()
                """),
            cancellationToken,
            address, ip);
        return ErpSimpleWriteResult.Ok("Subscribed.", 0);
    }

    public async Task<ErpSimpleWriteResult> SetUserOptionAsync(
        int userId,
        string? optionKey,
        string? optionValue,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var key = (optionKey ?? string.Empty).Trim();
        if (!IsAllowedOptionKey(key))
        {
            return ErpSimpleWriteResult.Fail("invalid", "This setting cannot be saved.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Account database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `users_options` WHERE `user_id` = ? AND `session_id` = 0 AND `data_key` = ?"),
            cancellationToken,
            userId, key);
        if (exists > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users_options` SET `data_value` = ? WHERE `user_id` = ? AND `session_id` = 0 AND `data_key` = ?"),
                cancellationToken,
                optionValue ?? "", userId, key);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users_options` (`user_id`, `session_id`, `data_key`, `data_value`) VALUES (?, 0, ?, ?)"),
                cancellationToken,
                userId, key, optionValue ?? "");
        }

        return ErpSimpleWriteResult.Ok("Setting saved.", userId);
    }

    private static bool IsAllowedOptionKey(string key)
    {
        if (key.Equals("selected_manufacturer", StringComparison.OrdinalIgnoreCase)
            || key.Equals("propucts_request_0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return key.StartsWith("propucts_request_", StringComparison.OrdinalIgnoreCase)
               && key.Length > "propucts_request_".Length;
    }
}
