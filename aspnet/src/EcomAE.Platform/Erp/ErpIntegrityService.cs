using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_db_integrity.php</c> twin: referential-integrity scanner
/// (<c>epc_erp_integrity_scan</c>) and guarded foreign-key applier
/// (<c>epc_erp_integrity_apply_fks</c>) — FKs only where the relationship is clean
/// (zero orphans), after ensuring the supporting index exists.
/// </summary>
public interface IErpIntegrityService
{
    Task<IReadOnlyList<ErpIntegrityScanRow>> ScanAsync(CancellationToken cancellationToken = default);
    Task<ErpIntegrityApplyResult> ApplyFksAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpIntegrityScanRow(string Child, string Col, string Parent, string Pcol, long Orphans, string Status);

public sealed record ErpIntegrityApplyResult(IReadOnlyList<string> Applied, IReadOnlyList<string> Skipped, IReadOnlyList<string> Errors);

public sealed class ErpIntegrityService : IErpIntegrityService
{
    private sealed record Rel(string Child, string Col, string Parent, string Pcol, string OnDelete);

    // PHP epc_erp_integrity_relationships() — verbatim catalogue.
    private static readonly Rel[] Relationships =
    {
        new("epc_erp_gl_lines", "journal_id", "epc_erp_gl_journals", "id", "CASCADE"),
        new("epc_erp_gl_lines", "coa_id", "epc_erp_coa_accounts", "id", "RESTRICT"),
        new("epc_erp_inv_movements", "item_id", "epc_erp_inv_items", "id", "RESTRICT"),
        new("epc_erp_inv_movements", "warehouse_id", "epc_erp_inv_warehouses", "id", "RESTRICT"),
        new("epc_erp_inv_stock", "item_id", "epc_erp_inv_items", "id", "CASCADE"),
        new("epc_erp_inv_stock", "warehouse_id", "epc_erp_inv_warehouses", "id", "CASCADE"),
        new("epc_erp_inv_serials", "item_id", "epc_erp_inv_items", "id", "CASCADE"),
        new("epc_erp_inv_item_fields", "item_id", "epc_erp_inv_items", "id", "CASCADE"),
        new("epc_einvoice_lines", "document_id", "epc_einvoice_documents", "id", "CASCADE"),
        new("epc_erp_purchase_inv_lines", "purchase_id", "epc_erp_purchases", "id", "CASCADE"),
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpIntegrityService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<IReadOnlyList<ErpIntegrityScanRow>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<ErpIntegrityScanRow>(Relationships.Length);
        if (!_connections.IsConfigured) return rows;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rel in Relationships)
        {
            var orphans = await OrphanCountAsync(connection, rel, cancellationToken).ConfigureAwait(false);
            string status;
            if (orphans < 0)
                status = "missing";
            else if (await HasFkAsync(connection, rel.Child, rel.Col, cancellationToken).ConfigureAwait(false))
                status = "exists";
            else if (orphans == 0)
                status = "clean";
            else
                status = "dirty";
            rows.Add(new ErpIntegrityScanRow(rel.Child, rel.Col, rel.Parent, rel.Pcol, orphans, status));
        }
        return rows;
    }

    public async Task<ErpIntegrityApplyResult> ApplyFksAsync(CancellationToken cancellationToken = default)
    {
        var applied = new List<string>();
        var skipped = new List<string>();
        var errors = new List<string>();
        if (!_connections.IsConfigured) return new(applied, skipped, new List<string> { "TenantRegistry DB is not configured." });

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var rel in Relationships)
        {
            var orphans = await OrphanCountAsync(connection, rel, cancellationToken).ConfigureAwait(false);
            var label = rel.Child + "." + rel.Col + " -> " + rel.Parent + "." + rel.Pcol;
            if (orphans < 0) { skipped.Add(label + " (table/column missing)"); continue; }
            if (await HasFkAsync(connection, rel.Child, rel.Col, cancellationToken).ConfigureAwait(false)) { skipped.Add(label + " (FK already present)"); continue; }
            if (orphans > 0) { skipped.Add(label + " (" + orphans + " orphan rows — clean data first)"); continue; }

            try
            {
                if (!await HasIndexAsync(connection, rel.Child, rel.Col, cancellationToken).ConfigureAwait(false))
                {
                    await ErpDb.ExecuteAsync(connection, null, "ALTER TABLE `" + rel.Child + "` ADD INDEX `fk_" + rel.Col + "` (`" + rel.Col + "`)", cancellationToken).ConfigureAwait(false);
                }
                var fkName = ("fk_" + rel.Child + "_" + rel.Col);
                if (fkName.Length > 60) fkName = fkName[..60];
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "ALTER TABLE `" + rel.Child + "` ADD CONSTRAINT `" + fkName + "` FOREIGN KEY (`" + rel.Col + "`) REFERENCES `" + rel.Parent + "` (`" + rel.Pcol + "`) ON DELETE " + rel.OnDelete + " ON UPDATE CASCADE",
                    cancellationToken).ConfigureAwait(false);
                applied.Add(label);
            }
            catch (DbException ex)
            {
                errors.Add(label + ": " + ex.Message);
            }
        }
        return new(applied, skipped, errors);
    }

    private static async Task<long> OrphanCountAsync(DbConnection connection, Rel rel, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, rel.Child, ct).ConfigureAwait(false)
            || !await TableExistsAsync(connection, rel.Parent, ct).ConfigureAwait(false))
            return -1;
        if (await ColumnTypeAsync(connection, rel.Child, rel.Col, ct).ConfigureAwait(false) is null
            || await ColumnTypeAsync(connection, rel.Parent, rel.Pcol, ct).ConfigureAwait(false) is null)
            return -1;

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM `" + rel.Child + "` c"
            + " LEFT JOIN `" + rel.Parent + "` p ON c.`" + rel.Col + "` = p.`" + rel.Pcol + "`"
            + " WHERE c.`" + rel.Col + "` IS NOT NULL AND c.`" + rel.Col + "` <> 0 AND p.`" + rel.Pcol + "` IS NULL";
        var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt64(scalar);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken ct)
        => await InfoScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?",
            ct,
            table).ConfigureAwait(false) > 0;

    private static async Task<string?> ColumnTypeAsync(DbConnection connection, string table, string col, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?");
        ErpDb.AddParameters(command, table, col);
        var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task<bool> HasFkAsync(DbConnection connection, string table, string col, CancellationToken ct)
        => await InfoScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ? AND REFERENCED_TABLE_NAME IS NOT NULL",
            ct,
            table,
            col).ConfigureAwait(false) > 0;

    private static async Task<bool> HasIndexAsync(DbConnection connection, string table, string col, CancellationToken ct)
        => await InfoScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ? AND SEQ_IN_INDEX = 1",
            ct,
            table,
            col).ConfigureAwait(false) > 0;

    private static async Task<long> InfoScalarAsync(DbConnection connection, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, parameters);
        var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt64(scalar);
    }
}
