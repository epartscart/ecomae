using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public interface IErpSheetEntryWriteService
{
    Task<ErpSimpleWriteResult> PostAsync(
        string sheet,
        IReadOnlyDictionary<string, string> fields,
        int userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Posts the entry form on pages that previously had only a list.
/// Stock transfer, stock movement, and opening batch use the existing writers.
/// </summary>
public sealed class ErpSheetEntryWriteService : IErpSheetEntryWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVirtualWarehouseWriteService _transfers;
    private readonly IErpInventoryMovementWriteService _movements;
    private readonly IErpOpeningCreateBatchWriteService _openings;
    private readonly IErpOpeningAddCoaLineWriteService _openingLines;

    public ErpSheetEntryWriteService(
        IErpWriteConnectionFactory connections,
        IErpVirtualWarehouseWriteService transfers,
        IErpInventoryMovementWriteService movements,
        IErpOpeningCreateBatchWriteService openings,
        IErpOpeningAddCoaLineWriteService openingLines)
    {
        _connections = connections;
        _transfers = transfers;
        _movements = movements;
        _openings = openings;
        _openingLines = openingLines;
    }

    public async Task<ErpSimpleWriteResult> PostAsync(
        string sheet,
        IReadOnlyDictionary<string, string> fields,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var check = ErpSheetEntryCatalog.Check(sheet, fields);
        if (!check.Ok)
        {
            return ErpSimpleWriteResult.Fail("invalid", check.Message);
        }

        if (!ErpSheetEntryCatalog.TryGet(sheet, out var definition))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown entry.");
        }

        return definition.Key switch
        {
            ErpSheetEntryCatalog.DataMigration => await PostMigrationAsync(fields, userId, cancellationToken).ConfigureAwait(false),
            ErpSheetEntryCatalog.StockTransfer => await _transfers.TransferAsync(
                new ErpVirtualWarehouseTransferRequest(
                    Company(fields),
                    ErpSheetEntryCatalog.Long(fields, "from_warehouse_id"),
                    ErpSheetEntryCatalog.Long(fields, "to_warehouse_id"),
                    ErpSheetEntryCatalog.Text(fields, "reason"),
                    ErpSheetEntryCatalog.Text(fields, "notes"),
                    userId,
                    Sku: ErpSheetEntryCatalog.Text(fields, "sku"),
                    Qty: ErpSheetEntryCatalog.Dec(fields, "qty")),
                cancellationToken).ConfigureAwait(false),
            ErpSheetEntryCatalog.StockMovement => await _movements.RecordMovementAsync(
                new ErpInventoryMovementWriteRequest(
                    userId,
                    ErpSheetEntryCatalog.Text(fields, "movement_type"),
                    ErpSheetEntryCatalog.Long(fields, "warehouse_id"),
                    ErpSheetEntryCatalog.Long(fields, "item_id"),
                    ErpSheetEntryCatalog.Dec(fields, "qty"),
                    ErpSheetEntryCatalog.Dec(fields, "unit_cost"),
                    Reference: ErpSheetEntryCatalog.Text(fields, "reference"),
                    Note: ErpSheetEntryCatalog.Text(fields, "note"),
                    MovementDate: ErpSheetEntryCatalog.Text(fields, "movement_date")),
                cancellationToken).ConfigureAwait(false),
            ErpSheetEntryCatalog.StaffProfile => await PostStaffAsync(fields, cancellationToken).ConfigureAwait(false),
            ErpSheetEntryCatalog.InventoryReport => await PostInventoryAsync(fields, cancellationToken).ConfigureAwait(false),
            ErpSheetEntryCatalog.FinanceClose => await PostOpeningAsync(fields, userId, cancellationToken).ConfigureAwait(false),
            _ => ErpSimpleWriteResult.Fail("invalid", "Unknown entry."),
        };
    }

    private async Task<ErpSimpleWriteResult> PostMigrationAsync(
        IReadOnlyDictionary<string, string> fields,
        int userId,
        CancellationToken cancellationToken)
    {
        var inspect = ErpDataMigrationCsv.Inspect(
            ErpSheetEntryCatalog.Text(fields, "migration_type"),
            ErpSheetEntryCatalog.Text(fields, "entity_type"),
            ErpSheetEntryCatalog.Text(fields, "file_name"),
            ErpSheetEntryCatalog.Text(fields, "csv"));
        if (!inspect.Ok)
        {
            return ErpSimpleWriteResult.Fail("invalid", inspect.Message);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var mapping = inspect.Headers
            .Where(header => header.Trim().Length > 0)
            .GroupBy(ErpDataMigrationCsv.NormalizeHeader, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Key, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureDataMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_data_migrations` (`company_id`,`migration_type`,`entity_type`,`file_name`,`file_path`,`total_rows`,`valid_rows`,`error_rows`,`imported_rows`,`status`,`column_mapping`,`validation_errors`,`options`,`imported_by`,`imported_by_name`,`time_created`,`time_completed`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                Company(fields),
                inspect.MigrationType,
                inspect.Entity,
                inspect.FileName,
                "",
                inspect.Rows.Count,
                inspect.ValidRows,
                inspect.ErrorRows,
                0,
                "validated",
                JsonSerializer.Serialize(mapping),
                "[]",
                "{\"source\":\"csv_text\"}",
                userId < 0 ? 0 : userId,
                "",
                now,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var row in inspect.Rows)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_data_migration_rows` (`migration_id`,`row_number`,`raw_data`,`mapped_data`,`status`,`error_message`,`created_entity_id`) VALUES (?,?,?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    row.RowNumber,
                    row.RawJson,
                    row.MappedJson,
                    row.Status,
                    row.Error,
                    0).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Import " + id.ToString(CultureInfo.InvariantCulture) + " posted with "
                + inspect.ValidRows.ToString(CultureInfo.InvariantCulture) + " rows.",
                id);
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", "The import could not be saved.");
        }
    }

    private async Task<ErpSimpleWriteResult> PostStaffAsync(
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePayrollAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_staff_profiles` (`user_id`,`department_code`,`display_name`,`job_title`,`email`,`phone`,`active`,`time_created`) VALUES (?,?,?,?,?,?,1,?)"),
                cancellationToken,
                ErpSheetEntryCatalog.Long(fields, "user_id"),
                Clip(ErpSheetEntryCatalog.Text(fields, "department_code"), 32),
                Clip(ErpSheetEntryCatalog.Text(fields, "display_name"), 128),
                Clip(ErpSheetEntryCatalog.Text(fields, "job_title"), 128),
                Clip(ErpSheetEntryCatalog.Text(fields, "email"), 128),
                Clip(ErpSheetEntryCatalog.Text(fields, "phone"), 64),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Staff profile posted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This user already has a staff profile.");
        }
    }

    private async Task<ErpSimpleWriteResult> PostInventoryAsync(
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var company = Company(fields);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureInventoryReportAsync(connection, cancellationToken).ConfigureAwait(false);
        if (ErpSheetEntryCatalog.Text(fields, "kind") == "category")
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_inventory_categories` (`company_id`,`parent_id`,`code`,`name`,`level`,`sort_order`,`is_active`,`time_created`) VALUES (?,?,?,?,?,?,1,?)"),
                cancellationToken,
                company,
                0,
                Clip(ErpSheetEntryCatalog.Text(fields, "code"), 30),
                Clip(ErpSheetEntryCatalog.Text(fields, "name"), 200),
                1,
                0,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Category posted.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_inventory_snapshots` (`company_id`,`snapshot_date`,`category_id`,`total_skus`,`total_qty`,`total_value`,`avg_age_days`,`time_created`) VALUES (?,?,?,?,?,?,0,?)"),
            cancellationToken,
            company,
            ErpSheetEntryCatalog.Text(fields, "snapshot_date"),
            ErpSheetEntryCatalog.Long(fields, "category_id"),
            (int)Math.Clamp(ErpSheetEntryCatalog.Long(fields, "total_skus"), 0, int.MaxValue),
            ErpSheetEntryCatalog.Dec(fields, "total_qty"),
            ErpSheetEntryCatalog.Dec(fields, "total_value"),
            now).ConfigureAwait(false);
        var snapshotId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Snapshot posted.", snapshotId);
    }

    private async Task<ErpSimpleWriteResult> PostOpeningAsync(
        IReadOnlyDictionary<string, string> fields,
        int userId,
        CancellationToken cancellationToken)
    {
        var created = await _openings.CreateAsync(
            new ErpOpeningCreateBatchWriteRequest(
                ErpSheetEntryCatalog.Text(fields, "module"),
                ErpSheetEntryCatalog.Text(fields, "as_of_date"),
                ErpSheetEntryCatalog.Text(fields, "reference"),
                ErpSheetEntryCatalog.Text(fields, "note"),
                userId),
            cancellationToken).ConfigureAwait(false);
        if (!created.Succeeded || created.Id <= 0)
        {
            return created;
        }

        var line = await _openingLines.AddAsync(
            new ErpOpeningAddCoaLineWriteRequest(
                created.Id,
                ErpSheetEntryCatalog.Long(fields, "account_id"),
                ErpSheetEntryCatalog.Dec(fields, "debit"),
                ErpSheetEntryCatalog.Dec(fields, "credit")),
            cancellationToken).ConfigureAwait(false);
        if (!line.Succeeded)
        {
            return line;
        }

        return ErpSimpleWriteResult.Ok(
            "Opening batch " + created.Id.ToString(CultureInfo.InvariantCulture) + " saved as draft.",
            created.Id);
    }

    private static int Company(IReadOnlyDictionary<string, string> fields)
    {
        var company = (int)Math.Clamp(ErpSheetEntryCatalog.Long(fields, "company_id"), 0, int.MaxValue);
        return company <= 0 ? 1 : company;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}
