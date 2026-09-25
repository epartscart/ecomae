using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_return_action.php</c> twins for set_return_status / decide_line / finalize_return, plus
/// <c>reasons_statuses.php</c> add_reason / add_status and <c>epc_returns_ensure_automation</c> seeding.
/// </summary>
public interface ICpReturnWriteService
{
    Task<CpReturnsAutomation> EnsureAutomationAsync(DbConnection connection, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddReasonAsync(string caption, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddStatusAsync(string caption, string color, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetStatusAsync(long returnId, int statusId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DecideLineAsync(long returnId, long lineId, int decide, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> FinalizeAsync(long returnId, int adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>PHP <c>epc_returns_ensure_automation</c> return shape.</summary>
public sealed record CpReturnsAutomation(int CheckStatusId, int ForReturnStatusId, long CompleteStatusId, long RejectStatusId, IReadOnlyList<string> Report);

public sealed class CpReturnWriteService : ICpReturnWriteService
{
    private static readonly (string Caption, string Color)[] SeedStatuses =
    [
        ("Created", "#dae1dd"),
        ("Under consideration", "#f5f3cc"),
        ("Closed", "#26ad5f"),
    ];

    private static readonly string[] SeedReasons = ["The product did not fit", "Manufacturing defects"];

    private static readonly string[] ClosedCaptions = ["3798", "epc_ret_st_closed"];
    private static readonly string[] OpenCaptions = ["3806", "3796", "epc_ret_st_under_consideration", "epc_ret_st_created"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpReturnWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpReturnsAutomation> EnsureAutomationAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        var report = new List<string>();

        var enabled = await ErpDb.ExecuteAsync(
            connection, null,
            "UPDATE `shop_orders_items_statuses_ref` SET `check_for_return` = 1 WHERE `id` = 5 AND `check_for_return` = 0",
            cancellationToken).ConfigureAwait(false);
        if (enabled > 0)
        {
            report.Add("Enabled return requests on Issued status");
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            "UPDATE `shop_orders_items_statuses_ref` SET `for_return` = 1 WHERE `id` = 7 AND `for_return` = 0",
            cancellationToken).ConfigureAwait(false);

        var completeId = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        var rejectId = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken).ConfigureAwait(false);

        if (completeId < 1)
        {
            var maxOrder = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(MAX(`order`),0) FROM `shop_orders_items_statuses_ref`", cancellationToken).ConfigureAwait(false);
            var key = await EnsureLangStringAsync(connection, "epc_return_item_approved", "Return approved", cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_items_statuses_ref` (`name`,`color`,`for_created`,`for_finish`,`order`,`count_flag`,`issue_flag`,`to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`,`for_return`,`check_for_return`,`complete_return`,`reject_return`) VALUES (?,?,0,0,?,1,0,1,1,1,1,0,0,1,0)"),
                cancellationToken, key, "#c8f7c5", maxOrder + 1).ConfigureAwait(false);
            completeId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            report.Add("Created item status: Return approved");
        }

        if (rejectId < 1)
        {
            var maxOrder = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(MAX(`order`),0) FROM `shop_orders_items_statuses_ref`", cancellationToken).ConfigureAwait(false);
            var key = await EnsureLangStringAsync(connection, "epc_return_item_rejected", "Return rejected", cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_items_statuses_ref` (`name`,`color`,`for_created`,`for_finish`,`order`,`count_flag`,`issue_flag`,`to_manager_email`,`to_manager_sms`,`to_customer_email`,`to_customer_sms`,`for_return`,`check_for_return`,`complete_return`,`reject_return`) VALUES (?,?,0,0,?,0,0,1,1,1,1,0,0,0,1)"),
                cancellationToken, key, "#ffd0d0", maxOrder + 1).ConfigureAwait(false);
            rejectId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            report.Add("Created item status: Return rejected");
        }

        var statusCount = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_orders_returns_statuses`", cancellationToken).ConfigureAwait(false);
        if (statusCount < 1)
        {
            foreach (var (caption, color) in SeedStatuses)
            {
                var key = await EnsureLangStringAsync(connection, "epc_ret_st_" + SlugKey(caption), caption, cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT INTO `shop_orders_returns_statuses` (`color`,`caption`) VALUES (?,?)"),
                    cancellationToken, color, key).ConfigureAwait(false);
            }

            report.Add("Seeded return request statuses");
        }

        var reasonCount = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_orders_returns_reasons`", cancellationToken).ConfigureAwait(false);
        if (reasonCount < 1)
        {
            foreach (var caption in SeedReasons)
            {
                var key = await EnsureLangStringAsync(connection, "epc_ret_rs_" + SlugKey(caption), caption, cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT INTO `shop_orders_returns_reasons` (`caption`) VALUES (?)"),
                    cancellationToken, key).ConfigureAwait(false);
            }

            report.Add("Seeded return reasons");
        }

        return new CpReturnsAutomation(5, 7, completeId, rejectId, report);
    }

    public async Task<ErpSimpleWriteResult> AddReasonAsync(string caption, CancellationToken cancellationToken = default)
    {
        caption = (caption ?? string.Empty).Trim();
        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Caption is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var key = await EnsureLangStringAsync(connection, "epc_ret_rs_" + Md5Key(caption), caption, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_orders_returns_reasons` (`caption`) VALUES (?)"),
            cancellationToken, key).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Saved", id);
    }

    public async Task<ErpSimpleWriteResult> AddStatusAsync(string caption, string color, CancellationToken cancellationToken = default)
    {
        caption = (caption ?? string.Empty).Trim();
        color = (color ?? string.Empty).Trim();
        if (color.Length == 0)
        {
            color = "#eeeeee";
        }

        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Caption is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var key = await EnsureLangStringAsync(connection, "epc_ret_st_" + Md5Key(caption), caption, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `shop_orders_returns_statuses` (`color`,`caption`) VALUES (?,?)"),
            cancellationToken, color, key).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Saved", id);
    }

    /// <summary>PHP <c>epc_returns_ensure_lang_string</c>.</summary>
    internal static async Task<string> EnsureLangStringAsync(DbConnection connection, string strKey, string enValue, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.StringAsync(
            connection, null,
            ErpDb.Positional("SELECT `str_key` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1"),
            cancellationToken, strKey).ConfigureAwait(false);
        if (string.IsNullOrEmpty(exists))
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `lang_text_strings` (`str_key`,`description`,`same`,`is_error`,`is_custom`,`used_found`) VALUES (?,?,0,0,1,1)"),
                cancellationToken, strKey, enValue).ConfigureAwait(false);
        }

        var translated = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `id` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1"),
            cancellationToken, strKey, "en").ConfigureAwait(false);
        if (translated <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`str_key`,`lang_code`,`value`) VALUES (?,?,?)"),
                cancellationToken, strKey, "en", enValue).ConfigureAwait(false);
        }

        return strKey;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9]+/i', '_', strtolower($caption))</c>.</summary>
    internal static string SlugKey(string caption)
    {
        var sb = new StringBuilder(caption.Length);
        var pendingSep = false;
        foreach (var ch in caption.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                if (pendingSep)
                {
                    sb.Append('_');
                    pendingSep = false;
                }

                sb.Append(ch);
            }
            else
            {
                pendingSep = true;
            }
        }

        if (pendingSep)
        {
            sb.Append('_');
        }

        return sb.ToString();
    }

    /// <summary>PHP <c>md5(strtolower($caption))</c>.</summary>
    internal static string Md5Key(string caption)
        => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(caption.ToLowerInvariant())));

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long returnId,
        int statusId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0 || statusId < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection, null, ErpDb.Positional("SELECT `id` FROM `shop_orders_returns` WHERE `id` = ? LIMIT 1"),
            cancellationToken, returnId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Return not found.");
        }

        var closedId = await StatusIdByCaptionsAsync(connection, ClosedCaptions, cancellationToken).ConfigureAwait(false);
        var complete = statusId == closedId ? 1 : 0;
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = ? WHERE `id` = ?"),
            cancellationToken, statusId, complete, returnId);
        return ErpSimpleWriteResult.Ok("Return status updated.", returnId);
    }

    public async Task<ErpSimpleWriteResult> DecideLineAsync(
        long returnId,
        long lineId,
        int decide,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0 || lineId <= 0 || decide is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid line decision.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var itemId = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT `item_id` FROM `shop_orders_returns_items` WHERE `id` = ? AND `return_id` = ? LIMIT 1"),
            cancellationToken, lineId, returnId);
        if (itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Line not found.");
        }

        var completeStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var rejectStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var newStatus = decide == 1 ? completeStatus : rejectStatus;
        if (newStatus <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Return item statuses are not configured.");
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns_items` SET `return_success` = ? WHERE `id` = ?"),
            cancellationToken, decide, lineId);
        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
            cancellationToken, newStatus, itemId);

        var orderId = await ErpDb.LongAsync(
            connection, null, ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1"),
            cancellationToken, itemId);
        if (orderId > 0)
        {
            var text = decide == 1
                ? "Return line approved for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture)
                : "Return line denied for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                cancellationToken, orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, text);
        }

        var openId = await StatusIdByCaptionsAsync(connection, OpenCaptions, cancellationToken).ConfigureAwait(false);
        if (openId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 0 WHERE `id` = ? AND (`return_complete` IS NULL OR `return_complete` = 0)"),
                cancellationToken, openId, returnId);
        }

        return ErpSimpleWriteResult.Ok("Return line decided.", lineId);
    }

    public async Task<ErpSimpleWriteResult> FinalizeAsync(
        long returnId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (returnId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid return.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var pending = await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("""
                SELECT COUNT(*) FROM `shop_orders_returns_items`
                WHERE `return_id` = ? AND (`return_success` IS NULL
                    OR (`return_success` NOT IN (0,1) AND `return_success` NOT IN ('0','1')))
                """),
            cancellationToken, returnId);
        if (pending > 0)
        {
            return ErpSimpleWriteResult.Fail("pending", "Decide every line (Approve or Deny) before closing.");
        }

        var completeStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `complete_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        var rejectStatus = await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `reject_return` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
        if (completeStatus <= 0 || rejectStatus <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Return item statuses are not configured.");
        }

        await using var lines = connection.CreateCommand();
        lines.CommandText = ErpDb.Positional("SELECT `item_id`, `return_success` FROM `shop_orders_returns_items` WHERE `return_id` = ?");
        ErpDb.AddParameters(lines, returnId);
        await using var reader = await lines.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var itemStatuses = new List<(long ItemId, int Decide)>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var itemId = Convert.ToInt64(reader["item_id"], CultureInfo.InvariantCulture);
            var raw = Convert.ToString(reader["return_success"], CultureInfo.InvariantCulture) ?? "0";
            itemStatuses.Add((itemId, raw is "1" ? 1 : 0));
        }

        await reader.CloseAsync().ConfigureAwait(false);
        foreach (var (itemId, decide) in itemStatuses)
        {
            if (itemId <= 0)
            {
                continue;
            }

            var newStatus = decide == 1 ? completeStatus : rejectStatus;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
                cancellationToken, newStatus, itemId);
        }

        var approvedSum = await ErpDb.DecimalAsync(
            connection, null,
            ErpDb.Positional("""
                SELECT COALESCE(SUM(oi.`price` * oi.`count_need`), 0) FROM `shop_orders_returns_items` ri
                INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                WHERE ri.`return_id` = ? AND ri.`return_success` IN (1,'1')
                """),
            cancellationToken, returnId);
        var closedId = await StatusIdByCaptionsAsync(connection, ClosedCaptions, cancellationToken).ConfigureAwait(false);
        if (closedId <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "Closed return status is not configured.");
        }

        await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 1, `sum` = ? WHERE `id` = ?"),
            cancellationToken, closedId, approvedSum, returnId);

        await using var orders = connection.CreateCommand();
        orders.CommandText = ErpDb.Positional("""
            SELECT DISTINCT oi.`order_id` FROM `shop_orders_returns_items` ri
            INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
            WHERE ri.`return_id` = ? AND oi.`order_id` > 0
            """);
        ErpDb.AddParameters(orders, returnId);
        await using var orderReader = await orders.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var orderIds = new List<long>();
        while (await orderReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            orderIds.Add(Convert.ToInt64(orderReader["order_id"], CultureInfo.InvariantCulture));
        }

        await orderReader.CloseAsync().ConfigureAwait(false);
        var log = "Return #" + returnId.ToString(CultureInfo.InvariantCulture) + " closed. Approved sum: "
                  + approvedSum.ToString("0.00", CultureInfo.InvariantCulture);
        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var orderId in orderIds)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                cancellationToken, orderId, time, adminUserId, log);
        }

        return ErpSimpleWriteResult.Ok("Return closed.", returnId);
    }

    private static async Task<long> StatusIdByCaptionsAsync(
        System.Data.Common.DbConnection connection,
        IReadOnlyList<string> captions,
        CancellationToken cancellationToken)
    {
        foreach (var caption in captions)
        {
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                cancellationToken, caption);
            if (id > 0)
            {
                return id;
            }
        }

        return await ErpDb.LongAsync(
            connection, null, "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1",
            cancellationToken);
    }
}
