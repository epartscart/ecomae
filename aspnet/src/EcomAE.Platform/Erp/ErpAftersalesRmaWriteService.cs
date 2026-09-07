using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_as_rma_create</c> twin. Schema-ensure and Blockchain BOS
/// lookup/anchor stay PHP — missing <c>epc_as_rma</c> refuses instead of CREATE.
/// </summary>
public interface IErpAftersalesRmaWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpAftersalesRmaCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ResolveAsync(
        ErpAftersalesRmaResolveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAftersalesRmaResolveRequest(
    long RmaId = 0,
    string? Disposition = null,
    decimal RefundAmount = -1);

public sealed record ErpAftersalesRmaCreateRequest(
    long CustomerId = 0,
    long SourceId = 0,
    string? RmaNo = null,
    string? Reason = null,
    bool Restock = false,
    IReadOnlyList<ErpAftersalesRmaLine>? Lines = null);

public sealed record ErpAftersalesRmaLine(
    long ItemId,
    decimal Qty,
    decimal UnitPrice = 0,
    string? ConditionNote = null);

public sealed class ErpAftersalesRmaWriteService : IErpAftersalesRmaWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAftersalesRmaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpAftersalesRmaCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = request.Lines ?? [];
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one return line (item_id,qty,...)");
        }

        foreach (var line in lines)
        {
            if (line.ItemId <= 0 || line.Qty <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Each line requires itemId > 0 and qty > 0.");
            }
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_as_rma", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_as_rma_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "After-sales RMA tables are not provisioned");
        }

        var postedNo = (request.RmaNo ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rmaNo = postedNo.Length > 0 ? Clip(postedNo, 40) : "RMA-" + now.ToString(CultureInfo.InvariantCulture);
        var reason = Clip((request.Reason ?? string.Empty).Trim(), 190);
        var customerId = request.CustomerId < 0 ? 0 : request.CustomerId;
        var sourceId = request.SourceId < 0 ? 0 : request.SourceId;
        var restock = request.Restock ? 1 : 0;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_as_rma` (`rma_no`,`customer_id`,`source_type`,`source_id`,`reason`,`disposition`,`status`,`restock`,`time_created`,`time_updated`) VALUES (?, ?, 'sales_order', ?, ?, 'pending', 'open', ?, ?, ?)"),
            cancellationToken,
            rmaNo,
            customerId,
            sourceId,
            reason.Length == 0 ? null : reason,
            restock,
            now,
            now).ConfigureAwait(false);

        var rmaId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (postedNo.Length == 0 && rmaId > 0)
        {
            rmaNo = "RMA-" + rmaId.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_as_rma` SET `rma_no` = ? WHERE `id` = ?"),
                cancellationToken,
                rmaNo,
                rmaId).ConfigureAwait(false);
        }

        foreach (var line in lines)
        {
            var note = Clip((line.ConditionNote ?? string.Empty).Trim(), 190);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_as_rma_lines` (`rma_id`,`item_id`,`qty`,`unit_price`,`condition_note`) VALUES (?, ?, ?, ?, ?)"),
                cancellationToken,
                rmaId,
                line.ItemId,
                decimal.Round(line.Qty, 4, MidpointRounding.AwayFromZero),
                decimal.Round(line.UnitPrice, 2, MidpointRounding.AwayFromZero),
                note.Length == 0 ? null : note).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("RMA " + rmaNo + " created", rmaId);
    }

    public async Task<ErpSimpleWriteResult> ResolveAsync(
        ErpAftersalesRmaResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RmaId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RMA id is required.");
        }

        var disposition = (request.Disposition ?? string.Empty).Trim().ToLowerInvariant();
        if (disposition is not ("refund" or "replace" or "repair" or "reject"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid disposition");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_as_rma", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_as_rma_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "After-sales RMA tables are not provisioned");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_as_rma` WHERE `id` = ?"),
            cancellationToken,
            request.RmaId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RMA not found");
        }

        var linesTotal = decimal.Round(
            await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(SUM(`qty` * `unit_price`), 0) FROM `epc_as_rma_lines` WHERE `rma_id` = ?"),
                cancellationToken,
                request.RmaId).ConfigureAwait(false),
            2,
            MidpointRounding.AwayFromZero);
        var refund = request.RefundAmount;
        if (refund < 0)
        {
            refund = disposition == "refund" ? linesTotal : 0m;
        }

        refund = decimal.Round(refund < 0 ? 0 : refund, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_as_rma` SET `disposition` = ?, `status` = 'closed', `refund_amount` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            disposition,
            refund,
            now,
            request.RmaId).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok("RMA resolved (" + disposition + ")", request.RmaId);
    }

    /// <summary>PHP ajax <c>lines_csv</c>: <c>item_id,qty,unit_price,condition_note</c> per row.</summary>
    public static IReadOnlyList<ErpAftersalesRmaLine> ParseLinesCsv(string? csv)
    {
        var lines = new List<ErpAftersalesRmaLine>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return lines;
        }

        foreach (var raw in csv.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
                     .Split('\n', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0 || raw.StartsWith("item_id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = SplitCsvRow(raw);
            if (parts.Count < 2)
            {
                continue;
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId)
                || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
            {
                continue;
            }

            var unit = 0m;
            if (parts.Count > 2)
            {
                decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out unit);
            }

            var note = parts.Count > 3 ? parts[3] : string.Empty;
            lines.Add(new ErpAftersalesRmaLine(itemId, qty, unit, note));
        }

        return lines;
    }

    private static List<string> SplitCsvRow(string row)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < row.Length; i++)
        {
            var ch = row[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    quoted = false;
                    continue;
                }

                current.Append(ch);
                continue;
            }

            if (ch == '"')
            {
                quoted = true;
                continue;
            }

            if (ch == ',')
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        parts.Add(current.ToString().Trim());
        return parts;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
