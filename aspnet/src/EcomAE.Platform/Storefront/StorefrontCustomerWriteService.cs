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

    Task<ErpSimpleWriteResult> SaveProfileAsync(
        int userId,
        IReadOnlyDictionary<string, string>? fields,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendReturnMessageAsync(
        int userId,
        long returnId,
        string? text,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateReturnAsync(
        int userId,
        long orderId,
        long itemId,
        int reasonId,
        int count,
        string? comment,
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

    public async Task<ErpSimpleWriteResult> SendReturnMessageAsync(
        int userId,
        long returnId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var body = WebUtility.HtmlEncode((text ?? string.Empty).Trim());
        if (returnId <= 0 || body.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Message text is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Returns database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var owned = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders_returns` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            returnId, userId);
        if (owned <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Return not found.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (0, 1, ?, ?, ?)"),
            cancellationToken,
            body, now, returnId);
        return ErpSimpleWriteResult.Ok("Message sent.", returnId);
    }

    public async Task<ErpSimpleWriteResult> CreateReturnAsync(
        int userId,
        long orderId,
        long itemId,
        int reasonId,
        int count,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (orderId <= 0 || itemId <= 0 || reasonId <= 0 || count < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Order, item, reason, and quantity are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Returns database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var itemCmd = connection.CreateCommand();
            itemCmd.Transaction = tx;
            itemCmd.CommandText = ErpDb.Positional("""
                SELECT soi.`id`, soi.`order_id`, soi.`count_need`, soi.`price`
                FROM `shop_orders_items` soi
                INNER JOIN `shop_orders` so ON so.`id` = soi.`order_id`
                WHERE soi.`id` = ? AND soi.`order_id` = ? AND so.`user_id` = ?
                LIMIT 1
                """);
            ErpDb.AddParameters(itemCmd, itemId, orderId, userId);
            await using var reader = await itemCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Order item not found.");
            }

            var countNeed = Convert.ToInt32(reader["count_need"], System.Globalization.CultureInfo.InvariantCulture);
            var price = Convert.ToDecimal(reader["price"], System.Globalization.CultureInfo.InvariantCulture);
            await reader.CloseAsync().ConfigureAwait(false);

            if (count != countNeed)
            {
                return ErpSimpleWriteResult.Fail("split", "Partial-quantity returns stay on the classic form.");
            }

            var already = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `item_id` = ?"),
                cancellationToken,
                itemId);
            if (already > 0)
            {
                return ErpSimpleWriteResult.Fail("already", "This line already has a return.");
            }

            var statusId = 0L;
            foreach (var caption in new[] { "3806", "3796", "epc_ret_st_under_consideration", "epc_ret_st_created" })
            {
                statusId = await ErpDb.LongAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                    cancellationToken,
                    caption);
                if (statusId > 0)
                {
                    break;
                }
            }

            if (statusId <= 0)
            {
                statusId = await ErpDb.LongAsync(
                    connection,
                    tx,
                    "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1",
                    cancellationToken);
            }

            if (statusId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Return status is not configured.");
            }

            var sum = decimal.Round(price * count, 2, MidpointRounding.AwayFromZero);
            var note = WebUtility.HtmlEncode((comment ?? string.Empty).Trim());
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("INSERT INTO `shop_orders_returns` (`status_id`, `user_id`, `sum`) VALUES (?, ?, ?)"),
                cancellationToken,
                statusId, userId, sum);
            var returnId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
            if (returnId <= 0)
            {
                return ErpSimpleWriteResult.Fail("insert_failed", "Could not create the return.");
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("INSERT INTO `shop_orders_returns_items` (`comment`, `reason_id`, `return_id`, `item_id`, `count_need`) VALUES (?, ?, ?, ?, ?)"),
                cancellationToken,
                note, reasonId, returnId, itemId, count);

            var returnStatus = await ErpDb.LongAsync(
                connection,
                tx,
                "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_return` = 1 LIMIT 1",
                cancellationToken);
            if (returnStatus > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
                    cancellationToken,
                    returnStatus, itemId);
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, 0, 0, ?, 1)"),
                    cancellationToken,
                    orderId,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    "Return created for item [" + itemId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Return request submitted.", returnId);
        }
        catch (Exception ex) when (ex is ErpWriteException or System.Data.Common.DbException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("insert_failed", ex.Message);
        }
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

    public async Task<ErpSimpleWriteResult> SaveProfileAsync(
        int userId,
        IReadOnlyDictionary<string, string>? fields,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var clean = NormalizeProfileFields(fields);
        if (clean.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one profile field is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Account database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = 0;
        foreach (var (key, value) in clean)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"),
                cancellationToken,
                userId, key);
            if (exists > 0)
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `users_profiles` SET `data_value` = ? WHERE `user_id` = ? AND `data_key` = ?"),
                    cancellationToken,
                    value, userId, key);
            }
            else
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
                    cancellationToken,
                    userId, key, value);
            }
        }

        return new ErpSimpleWriteResult(true, "ok", "Profile saved.", userId, writes);
    }

    public static Dictionary<string, string> NormalizeProfileFields(IReadOnlyDictionary<string, string>? fields)
    {
        var clean = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (fields is null)
        {
            return clean;
        }

        foreach (var (rawKey, rawValue) in fields)
        {
            var key = (rawKey ?? string.Empty).Trim();
            if (!IsAllowedProfileKey(key))
            {
                continue;
            }

            var value = WebUtility.HtmlEncode((rawValue ?? string.Empty).Trim());
            if (value.Length == 0)
            {
                continue;
            }

            if (value.Length > 255)
            {
                value = value[..255];
            }

            clean[key] = value;
        }

        return clean;
    }

    public static bool IsAllowedProfileKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("csrf", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("epc_doc_", StringComparison.OrdinalIgnoreCase)
            || key.Equals("email", StringComparison.OrdinalIgnoreCase)
            || key.Equals("phone", StringComparison.OrdinalIgnoreCase)
            || key.Equals("epc_trade_approval_status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AllowedProfileKeys.Contains(key);
    }

    internal static IReadOnlyCollection<string> AllowedProfileFieldNames => AllowedProfileKeys;

    private static readonly HashSet<string> AllowedProfileKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "surname", "fio", "full_name", "company_name",
        "epc_reg_job_title", "epc_emirates_id_no", "epc_passport_no", "epc_nationality",
        "epc_reg_legal_name", "epc_reg_business_type", "epc_reg_country", "epc_reg_emirate",
        "epc_reg_city", "epc_reg_address", "epc_reg_postal", "epc_reg_website",
        "epc_reg_trn", "epc_reg_trn_mode", "epc_reg_trade_licence", "epc_legal_reg_type", "epc_tin",
        "epc_authorized_signatory", "epc_authorized_signatory_id", "epc_ubo_name",
        "epc_source_of_funds", "epc_pep_declaration", "epc_sanctions_declaration",
    };

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
