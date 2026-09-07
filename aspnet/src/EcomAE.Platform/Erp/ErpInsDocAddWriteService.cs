using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_doc_add</c> / ajax <c>ins_doc_add</c> twin. INSERT
/// <c>epc_erp_ins_documents</c> with a path/URL string. Does not CREATE tables
/// and does not accept file bytes. Policy save/delete, expiry sync, and schema
/// ensure stay PHP.
/// </summary>
public interface IErpInsDocAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpInsDocAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpInsDocAddWriteRequest(
    long PolicyId = 0,
    string? DocType = null,
    string? Title = null,
    string? FilePath = null,
    string? Note = null);

public sealed class ErpInsDocAddWriteService : IErpInsDocAddWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsDocAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpInsDocAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PolicyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select a policy");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var docType = Clip((request.DocType ?? string.Empty).Trim(), 60);
        if (docType.Length == 0)
        {
            docType = "policy";
        }

        var title = Clip((request.Title ?? string.Empty).Trim(), 200);
        var path = Clip((request.FilePath ?? string.Empty).Trim(), 255);
        var note = Clip((request.Note ?? string.Empty).Trim(), 255);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_ins_documents", "policy_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_ins_documents", "file_path", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Insurance document table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ins_documents` (`policy_id`,`doc_type`,`title`,`file_path`,`note`,`time_created`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            request.PolicyId,
            docType,
            title,
            path,
            note,
            now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Document added", inserted);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
