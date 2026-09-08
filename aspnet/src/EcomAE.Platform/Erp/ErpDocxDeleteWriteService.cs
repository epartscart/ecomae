using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_docx_delete</c> / ajax <c>docx_delete</c> twin.
/// DELETE reminder rows then the <c>epc_erp_doc_expiry</c> register row.
/// Save, file bytes, reminder dispatch, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpDocxDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        ErpDocxDeleteWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpDocxDeleteWriteRequest(long Id = 0);

public sealed class ErpDocxDeleteWriteService : IErpDocxDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpDocxDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        ErpDocxDeleteWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_doc_expiry", "doc_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document expiry table is not provisioned");
        }

        if (await ColumnExistsAsync(connection, "epc_erp_doc_expiry_reminders", "doc_id", cancellationToken).ConfigureAwait(false))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry` WHERE `id`=?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Document removed from register", request.Id);
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
