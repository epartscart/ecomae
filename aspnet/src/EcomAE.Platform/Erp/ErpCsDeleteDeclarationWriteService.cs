using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cs_delete_declaration</c> / ajax <c>cs_delete_declaration</c> twin.
/// DELETE line items then the declaration row, unlinking the staged PDF file as PHP does.
/// Does not CREATE tables.
/// </summary>
public interface IErpCsDeleteDeclarationWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCsDeleteDeclarationWriteService : IErpCsDeleteDeclarationWriteService
{
    public const string InvalidId = "Invalid declaration id";
    public const string NotFound = "Declaration not found";
    public const string DeletedMessage = "Declaration deleted";
    public const string TableMissing = "Custom shipping tables are not provisioned";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public ErpCsDeleteDeclarationWriteService(IErpWriteConnectionFactory connections, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _connections = connections;
        _env = env;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", InvalidId);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_custom_shipping_declarations", "category", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_custom_shipping_declaration_items", "declaration_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", TableMissing);
        }

        var hasPdfColumn = await ColumnExistsAsync(connection, "epc_custom_shipping_declarations", "pdf_file_path", cancellationToken).ConfigureAwait(false);
        string? category = null;
        string? pdfPath = null;
        await using (var findCmd = connection.CreateCommand())
        {
            findCmd.CommandText = ErpDb.Positional("SELECT `category`" + (hasPdfColumn ? ", `pdf_file_path`" : string.Empty) + " FROM `epc_custom_shipping_declarations` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(findCmd, id);
            await using var reader = await findCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                category = reader.IsDBNull(0) ? null : reader.GetString(0);
                pdfPath = hasPdfColumn && !reader.IsDBNull(1) ? reader.GetString(1) : null;
            }
        }

        if (category is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", NotFound);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
        // PHP unlinks the attached PDF copy only when it lives under content/files/epc_custom_shipping_pdfs.
        if (!string.IsNullOrEmpty(pdfPath))
        {
            var rel = pdfPath.Replace('\\', '/').TrimStart('/');
            if (rel.Contains("content/files/epc_custom_shipping_pdfs/", StringComparison.Ordinal))
            {
                var full = Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(_env), rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
        }
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `epc_custom_shipping_declarations` WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(DeletedMessage, id);
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
}
