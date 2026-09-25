using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Read side of the PHP CP returns manager (<c>cp/content/shop/returns</c>: router.php, returns_list.php,
/// return_detail.php, reasons_statuses.php, epc_returns_process.php).
/// </summary>
public interface ICpReturnsDeskService
{
    Task<CpReturnsList> ListAsync(CpReturnsFilter filter, CancellationToken cancellationToken = default);

    Task<CpReturnDetail?> DetailAsync(long returnId, CancellationToken cancellationToken = default);

    Task<CpReturnsSetup> SetupAsync(CancellationToken cancellationToken = default);
}

public sealed record CpReturnsFilter(int StatusId, long OrderId, long UserId, bool UnreadOnly);

public sealed record CpReturnStatus(int Id, string Caption, string Label, string Color);

public sealed record CpReturnReason(int Id, string Caption, string Label);

public sealed record CpReturnRow(
    long Id,
    long UserId,
    string CustomerEmail,
    string CustomerPhone,
    int StatusId,
    string StatusLabel,
    string StatusColor,
    int UnreadMessages,
    IReadOnlyList<long> OrderIds,
    int LinesCount,
    decimal Sum)
{
    public string CustomerLabel
    {
        get
        {
            var parts = new[] { CustomerEmail, CustomerPhone }.Where(p => !string.IsNullOrWhiteSpace(p));
            var text = string.Join(" · ", parts);
            return text.Length > 0 ? text : "Customer #" + UserId.ToString(CultureInfo.InvariantCulture);
        }
    }
}

public sealed record CpReturnLine(
    long Id,
    long ItemId,
    long OrderId,
    string ReturnSuccess,
    int ReturnQty,
    int OrderQty,
    decimal Price,
    string ReasonLabel,
    string Comment,
    string Manufacturer,
    string Article,
    string Name)
{
    public string Decision => ReturnSuccess switch { "1" => "Approved", "0" => "Denied", _ => "Pending" };

    public int Qty => ReturnQty > 0 ? ReturnQty : OrderQty;

    public string Part
    {
        get
        {
            var part = (Manufacturer + " " + Article + " — " + Name).Trim();
            return part == "—" ? "Item #" + ItemId.ToString(CultureInfo.InvariantCulture) : part;
        }
    }
}

public sealed record CpReturnDetail(
    long Id,
    long UserId,
    string CustomerEmail,
    string CustomerPhone,
    int StatusId,
    string StatusLabel,
    string StatusColor,
    bool ReturnComplete,
    decimal Sum,
    IReadOnlyList<long> OrderIds,
    IReadOnlyList<CpReturnLine> Lines,
    IReadOnlyList<CpReturnStatus> Statuses)
{
    public long PrimaryOrderId => OrderIds.Count > 0 ? OrderIds[0] : 0;
}

public sealed record CpReturnsList(
    bool Available,
    string Error,
    IReadOnlyList<string> AutomationReport,
    IReadOnlyList<CpReturnRow> Rows,
    IReadOnlyList<CpReturnStatus> Statuses)
{
    public static CpReturnsList Unavailable(string error) => new(false, error, [], [], []);
}

public sealed record CpReturnItemStatusFlags(
    int Id,
    string Label,
    string Color,
    bool CheckForReturn,
    bool ForReturn,
    bool CompleteReturn,
    bool RejectReturn,
    bool IssueFlag,
    bool ForFinish)
{
    public bool Show => CheckForReturn || ForReturn || CompleteReturn || RejectReturn || IssueFlag || ForFinish;
}

public sealed record CpReturnsSetup(
    bool Available,
    string Error,
    IReadOnlyList<string> AutomationReport,
    IReadOnlyList<CpReturnReason> Reasons,
    IReadOnlyList<CpReturnStatus> Statuses,
    IReadOnlyList<CpReturnItemStatusFlags> ItemFlags)
{
    public static CpReturnsSetup Unavailable(string error) => new(false, error, [], [], [], []);
}

public sealed class CpReturnsDeskService : ICpReturnsDeskService
{
    public const int ListLimit = 300;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpReturnWriteService _writes;

    public CpReturnsDeskService(IErpWriteConnectionFactory connections, ICpReturnWriteService writes)
    {
        _connections = connections;
        _writes = writes;
    }

    public async Task<CpReturnsList> ListAsync(CpReturnsFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpReturnsList.Unavailable("No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var automation = await _writes.EnsureAutomationAsync(connection, cancellationToken).ConfigureAwait(false);
            var translate = Translator(connection, cancellationToken);
            var statuses = await StatusesAsync(connection, translate, cancellationToken).ConfigureAwait(false);

            var sql = """
                SELECT r.`id`, r.`user_id`, r.`status_id`, IFNULL(r.`sum`,0),
                    IFNULL(s.`caption`,''), IFNULL(s.`color`,''),
                    (SELECT COUNT(*) FROM `shop_orders_messages` m WHERE m.`return_id` = r.`id` AND m.`read` = 0 AND m.`is_customer` = 1),
                    IFNULL((SELECT GROUP_CONCAT(DISTINCT oi.`order_id` ORDER BY oi.`order_id` SEPARATOR ',')
                        FROM `shop_orders_returns_items` ri
                        INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                        WHERE ri.`return_id` = r.`id`),''),
                    (SELECT COUNT(*) FROM `shop_orders_returns_items` ri2 WHERE ri2.`return_id` = r.`id`),
                    IFNULL(u.`email`,''), IFNULL(u.`phone`,'')
                FROM `shop_orders_returns` r
                LEFT JOIN `shop_orders_returns_statuses` s ON s.`id` = r.`status_id`
                LEFT JOIN `users` u ON u.`user_id` = r.`user_id`
                WHERE 1=1
                """;
            var args = new List<object?>();
            if (filter.StatusId > 0)
            {
                sql += " AND r.`status_id` = ?";
                args.Add(filter.StatusId);
            }

            if (filter.UserId > 0)
            {
                sql += " AND r.`user_id` = ?";
                args.Add(filter.UserId);
            }

            if (filter.OrderId > 0)
            {
                sql += """
                     AND r.`id` IN (
                        SELECT ri.`return_id` FROM `shop_orders_returns_items` ri
                        INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                        WHERE oi.`order_id` = ?)
                    """;
                args.Add(filter.OrderId);
            }

            if (filter.UnreadOnly)
            {
                sql += " AND r.`id` IN (SELECT DISTINCT `return_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 1 AND `return_id` > 0)";
            }

            sql += " ORDER BY r.`id` DESC LIMIT " + ListLimit.ToString(CultureInfo.InvariantCulture);

            var raw = new List<(long Id, long UserId, int StatusId, decimal Sum, string Caption, string Color, int Unread, string OrderIds, int Lines, string Email, string Phone)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(sql);
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                        reader.GetString(4),
                        reader.GetString(5),
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture),
                        reader.GetString(7),
                        Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                        reader.GetString(9),
                        reader.GetString(10)));
                }
            }

            var rows = new List<CpReturnRow>(raw.Count);
            foreach (var r in raw)
            {
                rows.Add(new CpReturnRow(
                    r.Id, r.UserId, r.Email, r.Phone, r.StatusId,
                    await translate(r.Caption).ConfigureAwait(false),
                    r.Color, r.Unread, ParseIds(r.OrderIds), r.Lines, r.Sum));
            }

            return new CpReturnsList(true, "", automation.Report, rows, statuses);
        }
        catch (DbException ex)
        {
            return CpReturnsList.Unavailable(ex.Message);
        }
    }

    public async Task<CpReturnDetail?> DetailAsync(long returnId, CancellationToken cancellationToken = default)
    {
        if (returnId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await _writes.EnsureAutomationAsync(connection, cancellationToken).ConfigureAwait(false);
            var translate = Translator(connection, cancellationToken);

            long userId;
            int statusId;
            decimal sum;
            string caption;
            string color;
            bool complete;
            await using (var head = connection.CreateCommand())
            {
                head.CommandText = ErpDb.Positional("""
                    SELECT r.`user_id`, r.`status_id`, IFNULL(r.`sum`,0), IFNULL(r.`return_complete`,0), IFNULL(s.`caption`,''), IFNULL(s.`color`,'')
                    FROM `shop_orders_returns` r
                    LEFT JOIN `shop_orders_returns_statuses` s ON s.`id` = r.`status_id`
                    WHERE r.`id` = ? LIMIT 1
                    """);
                ErpDb.AddParameters(head, returnId);
                await using var reader = await head.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                statusId = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                sum = Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
                complete = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 1;
                caption = reader.GetString(4);
                color = reader.GetString(5);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `shop_orders_messages` SET `read` = 1 WHERE `return_id` = ? AND `is_customer` = 1"),
                cancellationToken, returnId).ConfigureAwait(false);

            var orderIds = await OrderIdsAsync(connection, returnId, cancellationToken).ConfigureAwait(false);

            var email = "";
            var phone = "";
            await using (var cust = connection.CreateCommand())
            {
                cust.CommandText = ErpDb.Positional("SELECT IFNULL(`email`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` = ? LIMIT 1");
                ErpDb.AddParameters(cust, userId);
                await using var reader = await cust.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    email = reader.GetString(0);
                    phone = reader.GetString(1);
                }
            }

            var rawLines = new List<(long Id, long ItemId, long OrderId, string Success, int ReturnQty, int OrderQty, decimal Price, string Reason, string Comment, string Man, string Art, string Name)>();
            await using (var lines = connection.CreateCommand())
            {
                lines.CommandText = ErpDb.Positional("""
                    SELECT ri.`id`, ri.`item_id`, IFNULL(oi.`order_id`,0), IFNULL(ri.`return_success`,''), IFNULL(ri.`count_need`,0),
                        IFNULL(oi.`count_need`,0), IFNULL(oi.`price`,0), IFNULL(rr.`caption`,''), IFNULL(ri.`comment`,''),
                        IFNULL(oi.`t2_manufacturer`,''), IFNULL(oi.`t2_article`,''), IFNULL(oi.`t2_name`,'')
                    FROM `shop_orders_returns_items` ri
                    LEFT JOIN `shop_orders_returns_reasons` rr ON rr.`id` = ri.`reason_id`
                    LEFT JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                    WHERE ri.`return_id` = ?
                    ORDER BY ri.`id` ASC
                    """);
                ErpDb.AddParameters(lines, returnId);
                await using var reader = await lines.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rawLines.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
                        Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                        reader.GetString(7),
                        reader.GetString(8),
                        reader.GetString(9),
                        reader.GetString(10),
                        reader.GetString(11)));
                }
            }

            var lineRows = new List<CpReturnLine>(rawLines.Count);
            foreach (var l in rawLines)
            {
                lineRows.Add(new CpReturnLine(
                    l.Id, l.ItemId, l.OrderId, l.Success, l.ReturnQty, l.OrderQty, l.Price,
                    await translate(l.Reason).ConfigureAwait(false), l.Comment, l.Man, l.Art, l.Name));
            }

            var statuses = await StatusesAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            return new CpReturnDetail(
                returnId, userId, email, phone, statusId,
                await translate(caption).ConfigureAwait(false), color, complete, sum, orderIds, lineRows, statuses);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpReturnsSetup> SetupAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpReturnsSetup.Unavailable("No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var automation = await _writes.EnsureAutomationAsync(connection, cancellationToken).ConfigureAwait(false);
            var translate = Translator(connection, cancellationToken);

            var rawReasons = new List<(int Id, string Caption)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`caption`,'') FROM `shop_orders_returns_reasons` ORDER BY `id` ASC";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rawReasons.Add((Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
                }
            }

            var reasons = new List<CpReturnReason>(rawReasons.Count);
            foreach (var r in rawReasons)
            {
                reasons.Add(new CpReturnReason(r.Id, r.Caption, await translate(r.Caption).ConfigureAwait(false)));
            }

            var statuses = await StatusesAsync(connection, translate, cancellationToken).ConfigureAwait(false);

            var rawFlags = new List<(int Id, string Name, string Color, bool Check, bool For, bool Complete, bool Reject, bool Issue, bool Finish)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT `id`, IFNULL(`name`,''), IFNULL(`color`,''), IFNULL(`check_for_return`,0), IFNULL(`for_return`,0),
                        IFNULL(`complete_return`,0), IFNULL(`reject_return`,0), IFNULL(`issue_flag`,0), IFNULL(`for_finish`,0)
                    FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC, `id` ASC
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rawFlags.Add((
                        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.GetString(1),
                        reader.GetString(2),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture) != 0));
                }
            }

            var flags = new List<CpReturnItemStatusFlags>(rawFlags.Count);
            foreach (var f in rawFlags)
            {
                flags.Add(new CpReturnItemStatusFlags(
                    f.Id, await translate(f.Name).ConfigureAwait(false), f.Color,
                    f.Check, f.For, f.Complete, f.Reject, f.Issue, f.Finish));
            }

            return new CpReturnsSetup(true, "", automation.Report, reasons, statuses, flags);
        }
        catch (DbException ex)
        {
            return CpReturnsSetup.Unavailable(ex.Message);
        }
    }

    internal static async Task<IReadOnlyList<long>> OrderIdsAsync(DbConnection connection, long returnId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("""
            SELECT DISTINCT oi.`order_id` FROM `shop_orders_returns_items` ri
            INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
            WHERE ri.`return_id` = ? AND oi.`order_id` > 0
            ORDER BY oi.`order_id` ASC
            """);
        ErpDb.AddParameters(cmd, returnId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var ids = new List<long>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static async Task<IReadOnlyList<CpReturnStatus>> StatusesAsync(
        DbConnection connection,
        Func<string, Task<string>> translate,
        CancellationToken cancellationToken)
    {
        var raw = new List<(int Id, string Caption, string Color)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`caption`,''), IFNULL(`color`,'') FROM `shop_orders_returns_statuses` ORDER BY `id` ASC";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), reader.GetString(2)));
            }
        }

        var rows = new List<CpReturnStatus>(raw.Count);
        foreach (var r in raw)
        {
            rows.Add(new CpReturnStatus(r.Id, r.Caption, await translate(r.Caption).ConfigureAwait(false), r.Color));
        }

        return rows;
    }

    /// <summary>PHP <c>epc_returns_label</c>: translate a str_key (numeric id or custom key), else echo the key.</summary>
    internal static Func<string, Task<string>> Translator(DbConnection connection, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        return async key =>
        {
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return key;
            }

            if (!cache.TryGetValue(key, out var text))
            {
                text = await ErpDb.StringAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? ORDER BY `lang_code` = 'en' DESC LIMIT 1"),
                    cancellationToken, key).ConfigureAwait(false);
                text = string.IsNullOrWhiteSpace(text) || text == "==Empty string==" ? key : text;
                cache[key] = text;
            }

            return text;
        };
    }

    internal static IReadOnlyList<long> ParseIds(string csv)
    {
        var ids = new List<long>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
