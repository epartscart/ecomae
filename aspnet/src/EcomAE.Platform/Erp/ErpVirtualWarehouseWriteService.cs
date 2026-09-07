using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_vwh_create_warehouse</c> / <c>epc_vwh_create_transfer</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpVirtualWarehouseWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpVirtualWarehouseCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> TransferAsync(
        ErpVirtualWarehouseTransferRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpVirtualWarehouseCreateRequest(
    int CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? Type = null,
    string? Address = null,
    int ManagerId = 0,
    string? ManagerName = null,
    int IsSellable = 1,
    string? EventName = null,
    string? EventStart = null,
    string? EventEnd = null,
    int ReturnWarehouseId = 0,
    string? Notes = null);

public sealed record ErpVirtualWarehouseTransferLine(
    int ProductId = 0,
    string? Sku = null,
    string? Barcode = null,
    decimal Qty = 0);

public sealed record ErpVirtualWarehouseTransferRequest(
    int CompanyId = 0,
    long FromWarehouseId = 0,
    long ToWarehouseId = 0,
    string? Reason = null,
    string? Notes = null,
    int CreatedBy = 0,
    string? LinesJson = null,
    IReadOnlyList<ErpVirtualWarehouseTransferLine>? Lines = null,
    int ProductId = 0,
    string? Sku = null,
    string? Barcode = null,
    decimal Qty = 0);

public sealed class ErpVirtualWarehouseWriteService : IErpVirtualWarehouseWriteService
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "physical", "virtual", "exhibition", "consignment", "transit"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpVirtualWarehouseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpVirtualWarehouseCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Clip((request.Code ?? string.Empty).Trim(), 20);
        var name = Clip((request.Name ?? string.Empty).Trim(), 200);
        if (code.Length == 0 && name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse code or name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_warehouses", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_warehouses", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Virtual warehouse tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        if (code.Length == 0)
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_warehouses` WHERE `company_id` = ?"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            code = "VW-" + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture) + "-"
                   + (count + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        var hasAddress = await ColumnExistsAsync(connection, "epc_warehouses", "address", cancellationToken).ConfigureAwait(false);
        var hasManager = await ColumnExistsAsync(connection, "epc_warehouses", "manager_name", cancellationToken).ConfigureAwait(false);
        var hasSellable = await ColumnExistsAsync(connection, "epc_warehouses", "is_sellable", cancellationToken).ConfigureAwait(false);
        var hasEvent = await ColumnExistsAsync(connection, "epc_warehouses", "event_name", cancellationToken).ConfigureAwait(false);
        var hasNotes = await ColumnExistsAsync(connection, "epc_warehouses", "notes", cancellationToken).ConfigureAwait(false);

        var columns = new StringBuilder("`company_id`,`code`,`name`,`type`");
        var placeholders = new StringBuilder("?, ?, ?, ?");
        var values = new List<object?>
        {
            companyId,
            code,
            name,
            NormalizeType(request.Type)
        };
        if (hasAddress)
        {
            columns.Append(",`address`");
            placeholders.Append(", ?");
            values.Add(Clip((request.Address ?? string.Empty).Trim(), 500));
        }

        if (hasManager)
        {
            columns.Append(",`manager_id`,`manager_name`");
            placeholders.Append(", ?, ?");
            values.Add(request.ManagerId < 0 ? 0 : request.ManagerId);
            values.Add(Clip((request.ManagerName ?? string.Empty).Trim(), 120));
        }

        if (hasSellable)
        {
            columns.Append(",`is_sellable`");
            placeholders.Append(", ?");
            values.Add(request.IsSellable == 0 ? 0 : 1);
        }

        if (hasEvent)
        {
            columns.Append(",`event_name`,`event_start`,`event_end`,`return_warehouse_id`");
            placeholders.Append(", ?, ?, ?, ?");
            values.Add(Clip((request.EventName ?? string.Empty).Trim(), 200));
            values.Add(FormatDateOrNull(request.EventStart));
            values.Add(FormatDateOrNull(request.EventEnd));
            values.Add(request.ReturnWarehouseId < 0 ? 0 : request.ReturnWarehouseId);
        }

        if (hasNotes)
        {
            columns.Append(",`notes`");
            placeholders.Append(", ?");
            values.Add(request.Notes ?? string.Empty);
        }

        columns.Append(",`time_created`");
        placeholders.Append(", ?");
        values.Add(UnixNow());

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_warehouses` (" + columns + ") VALUES (" + placeholders + ")"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Warehouse " + code + " created", id);
    }

    public async Task<ErpSimpleWriteResult> TransferAsync(
        ErpVirtualWarehouseTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FromWarehouseId <= 0 || request.ToWarehouseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "From and to warehouse ids are required.");
        }

        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            return ErpSimpleWriteResult.Fail("invalid", "From and to warehouses must differ.");
        }

        var lines = ParseLines(request);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one transfer line with qty is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_warehouses", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_warehouses", "code", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_warehouse_transfers", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_warehouse_transfers", "transfer_no", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_warehouse_transfer_lines", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_warehouse_transfer_lines", "qty", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Virtual warehouse tables are not provisioned");
        }

        var fromId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_warehouses` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.FromWarehouseId).ConfigureAwait(false);
        var toId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_warehouses` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ToWarehouseId).ConfigureAwait(false);
        if (fromId <= 0 || toId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse is missing.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var day = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_warehouse_transfers` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        var transferNo = "TRF-" + day + "-" + (count + 1).ToString("D4", CultureInfo.InvariantCulture);
        var totalQty = lines.Sum(l => l.Qty);
        var reason = Clip((request.Reason ?? string.Empty).Trim(), 200);
        if (reason.Length == 0)
        {
            reason = "exhibition";
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_warehouse_transfers` (`company_id`,`transfer_no`,`from_warehouse_id`,`to_warehouse_id`,`reason`,`status`,`total_items`,`total_qty`,`created_by`,`notes`,`time_created`) VALUES (?, ?, ?, ?, ?, 'draft', ?, ?, ?, ?, ?)"),
                cancellationToken,
                companyId,
                transferNo,
                fromId,
                toId,
                reason,
                lines.Count,
                decimal.Round(totalQty, 3, MidpointRounding.AwayFromZero),
                request.CreatedBy < 0 ? 0 : request.CreatedBy,
                request.Notes ?? string.Empty,
                UnixNow()).ConfigureAwait(false);

            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Failed");
            }

            foreach (var line in lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_warehouse_transfer_lines` (`transfer_id`,`product_id`,`sku`,`barcode`,`qty`) VALUES (?, ?, ?, ?, ?)"),
                    cancellationToken,
                    id,
                    line.ProductId < 0 ? 0 : line.ProductId,
                    Clip((line.Sku ?? string.Empty).Trim(), 100),
                    Clip((line.Barcode ?? string.Empty).Trim(), 100),
                    decimal.Round(line.Qty, 3, MidpointRounding.AwayFromZero)).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Transfer " + transferNo + " created", id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static List<ErpVirtualWarehouseTransferLine> ParseLines(ErpVirtualWarehouseTransferRequest request)
    {
        var lines = new List<ErpVirtualWarehouseTransferLine>();
        if (request.Lines is { Count: > 0 })
        {
            lines.AddRange(request.Lines);
        }
        else if (!string.IsNullOrWhiteSpace(request.LinesJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ErpVirtualWarehouseTransferLine>>(
                    request.LinesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is { Count: > 0 })
                {
                    lines.AddRange(parsed);
                }
            }
            catch (JsonException)
            {
                // fall through to single-line fields
            }
        }

        if (lines.Count == 0 && request.Qty > 0)
        {
            lines.Add(new ErpVirtualWarehouseTransferLine(request.ProductId, request.Sku, request.Barcode, request.Qty));
        }

        return lines.Where(l => l.Qty > 0).ToList();
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

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static string NormalizeType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Types.Contains(value) ? value : "virtual";
    }

    private static object? FormatDateOrNull(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
