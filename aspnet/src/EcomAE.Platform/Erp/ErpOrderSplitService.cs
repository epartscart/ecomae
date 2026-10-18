using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpOrderSplitService
{
    Task<ErpOrderSplitSnapshot> LoadAsync(string side, long orderId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PostAsync(ErpOrderSplitPost request, CancellationToken cancellationToken = default);
}

public sealed record ErpOrderSplitLineRow(
    long OrderId,
    long LineId,
    string Description,
    decimal Ordered,
    decimal Confirmed,
    decimal Delivered,
    decimal Invoiced,
    decimal Cancelled,
    decimal Backorder);

public sealed record ErpOrderSplitDocRow(long Id, string DocNo, string Action, decimal Qty, long TimeCreated);

public sealed record ErpOrderSplitSnapshot(
    string Side,
    long OrderId,
    IReadOnlyList<ErpOrderSplitLineRow> Lines,
    IReadOnlyList<ErpOrderSplitDocRow> Documents);

public sealed record ErpOrderSplitPost(string Side, string Action, IReadOnlyList<ErpOrderSplit.Move> Moves);

public sealed class ErpOrderSplitService : IErpOrderSplitService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOrderSplitService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpOrderSplitSnapshot> LoadAsync(string side, long orderId, CancellationToken cancellationToken = default)
    {
        var clean = ErpOrderSplit.KnownSide(side) ? side.Trim().ToLowerInvariant() : ErpOrderSplit.Sales;
        if (!_connections.IsConfigured || orderId <= 0)
        {
            return new ErpOrderSplitSnapshot(clean, orderId, [], []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var lines = await LinesAsync(connection, clean, orderId, cancellationToken).ConfigureAwait(false);
        var documents = await DocumentsAsync(connection, clean, orderId, cancellationToken).ConfigureAwait(false);
        return new ErpOrderSplitSnapshot(clean, orderId, lines, documents);
    }

    public async Task<ErpSimpleWriteResult> PostAsync(ErpOrderSplitPost request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ErpOrderSplit.KnownSide(request.Side))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a sales order or a purchase order.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var side = request.Side.Trim().ToLowerInvariant();
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        var moves = request.Moves ?? [];
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var orders = moves.Select(move => move.OrderId).Where(id => id > 0).Distinct().ToArray();
        var lines = new List<ErpOrderSplit.Line>();
        foreach (var orderId in orders)
        {
            foreach (var row in await LinesAsync(connection, side, orderId, cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new ErpOrderSplit.Line(row.OrderId, row.LineId, row.Ordered, row.Confirmed, row.Delivered, row.Invoiced, row.Cancelled));
            }
        }

        var reasons = ErpOrderSplit.Refuse(lines, action, moves);
        if (reasons.Count > 0)
        {
            return ErpSimpleWriteResult.Fail("blocked", string.Join(" ", reasons));
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var docNo = await NextNoAsync(connection, action, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `epc_erp_order_splits` (`doc_no`,`side`,`action`,`time_created`) VALUES (?,?,?,?)"),
                cancellationToken,
                docNo,
                side,
                action,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var move in moves)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `epc_erp_order_split_lines` (`split_id`,`side`,`order_id`,`line_id`,`qty`) VALUES (?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    side,
                    move.OrderId,
                    move.LineId,
                    ErpOrderSplit.Round(move.Qty)).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(ErpOrderSplit.ActionLabel(action) + " " + docNo + " posted. The undelivered balance stays a backorder.", id);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private static async Task<IReadOnlyList<ErpOrderSplitLineRow>> LinesAsync(
        DbConnection connection,
        string side,
        long orderId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(long LineId, string Description, decimal Ordered)>();
        try
        {
            await using var command = connection.CreateCommand();
            if (side == ErpOrderSplit.Purchase)
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`description`,''), IFNULL(`qty`,0), IFNULL(`qty_cancelled`,0) FROM `epc_erp_po_lines` WHERE `po_id`=? ORDER BY `line_no`,`id`");
            }
            else
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`description`,''), IFNULL(`qty`,0), 0 FROM `epc_erp_sales_order_lines` WHERE `sales_order_id`=? ORDER BY `line_no`,`id`");
            }

            ErpDb.AddParameters(command, orderId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var ordered = Dec(reader, 2) - Dec(reader, 3);
                rows.Add((Long(reader, 0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), ordered < 0m ? 0m : ordered));
            }
        }
        catch (DbException)
        {
            return [];
        }

        var sums = await SumsAsync(connection, side, orderId, cancellationToken).ConfigureAwait(false);
        return rows.Select(row =>
        {
            sums.TryGetValue((row.LineId, ErpOrderSplit.Confirm), out var confirmed);
            sums.TryGetValue((row.LineId, ErpOrderSplit.Deliver), out var delivered);
            sums.TryGetValue((row.LineId, ErpOrderSplit.Invoice), out var invoiced);
            sums.TryGetValue((row.LineId, ErpOrderSplit.CancelBackorder), out var cancelled);
            var line = new ErpOrderSplit.Line(orderId, row.LineId, row.Ordered, confirmed, delivered, invoiced, cancelled);
            return new ErpOrderSplitLineRow(orderId, row.LineId, row.Description, line.Ordered, line.Confirmed, line.Delivered, line.Invoiced, line.Cancelled, line.Backorder);
        }).ToArray();
    }

    private static async Task<Dictionary<(long LineId, string Action), decimal>> SumsAsync(
        DbConnection connection,
        string side,
        long orderId,
        CancellationToken cancellationToken)
    {
        var sums = new Dictionary<(long, string), decimal>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT l.`line_id`, s.`action`, SUM(l.`qty`) FROM `epc_erp_order_split_lines` l"
                + " INNER JOIN `epc_erp_order_splits` s ON s.`id`=l.`split_id`"
                + " WHERE l.`side`=? AND l.`order_id`=? GROUP BY l.`line_id`, s.`action`");
            ErpDb.AddParameters(command, side, orderId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var action = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                sums[(Long(reader, 0), action)] = Dec(reader, 2);
            }
        }
        catch (DbException)
        {
            return sums;
        }

        return sums;
    }

    private static async Task<IReadOnlyList<ErpOrderSplitDocRow>> DocumentsAsync(
        DbConnection connection,
        string side,
        long orderId,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpOrderSplitDocRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT s.`id`, IFNULL(s.`doc_no`,''), IFNULL(s.`action`,''), SUM(l.`qty`), IFNULL(s.`time_created`,0)"
                + " FROM `epc_erp_order_splits` s INNER JOIN `epc_erp_order_split_lines` l ON l.`split_id`=s.`id`"
                + " WHERE l.`side`=? AND l.`order_id`=? GROUP BY s.`id`, s.`doc_no`, s.`action`, s.`time_created`"
                + " ORDER BY s.`id` DESC LIMIT 12");
            ErpDb.AddParameters(command, side, orderId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpOrderSplitDocRow(
                    Long(reader, 0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Dec(reader, 3),
                    Long(reader, 4)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<string> NextNoAsync(DbConnection connection, string action, CancellationToken cancellationToken)
    {
        var prefix = action switch
        {
            ErpOrderSplit.Confirm => "CFM",
            ErpOrderSplit.Deliver => "DLV",
            ErpOrderSplit.Invoice => "OIV",
            _ => "CBK",
        };
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_order_splits` WHERE `action`=?"),
            cancellationToken,
            action).ConfigureAwait(false);
        return prefix + "-" + (count + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_order_splits` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`doc_no` varchar(32) NOT NULL DEFAULT '',"
            + "`side` varchar(12) NOT NULL DEFAULT '',"
            + "`action` varchar(24) NOT NULL DEFAULT '',"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `x_side` (`side`,`action`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Partial order documents'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_order_split_lines` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`split_id` int(11) NOT NULL DEFAULT 0,"
            + "`side` varchar(12) NOT NULL DEFAULT '',"
            + "`order_id` int(11) NOT NULL DEFAULT 0,"
            + "`line_id` int(11) NOT NULL DEFAULT 0,"
            + "`qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + "PRIMARY KEY (`id`), KEY `x_order` (`side`,`order_id`,`line_id`), KEY `x_split` (`split_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Partial order document lines'",
            cancellationToken).ConfigureAwait(false);
    }

    private static decimal Dec(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static long Long(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
