using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_delete</c> / ajax <c>ins_delete</c> twin. DELETE policy
/// plus optional document/claim/expiry rows. Does not CREATE tables. Policy
/// save, doc add, expiry upsert, and schema ensure stay PHP.
/// </summary>
public interface IErpInsDeleteWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpInsDeleteWriteService : IErpInsDeleteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsDeleteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A policy id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_ins_policies", "policy_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Insurance policy table is not provisioned");
        }

        var hasDocs = await ColumnExistsAsync(connection, "epc_erp_ins_documents", "policy_id", cancellationToken).ConfigureAwait(false);
        var hasClaims = await ColumnExistsAsync(connection, "epc_erp_ins_claims", "policy_id", cancellationToken).ConfigureAwait(false);
        var hasExpiry = await ColumnExistsAsync(connection, "epc_erp_doc_expiry", "source_module", cancellationToken).ConfigureAwait(false)
            && await ColumnExistsAsync(connection, "epc_erp_doc_expiry", "source_ref_id", cancellationToken).ConfigureAwait(false);
        var hasReminders = await ColumnExistsAsync(connection, "epc_erp_doc_expiry_reminders", "doc_id", cancellationToken).ConfigureAwait(false);

        if (hasExpiry)
        {
            var expiryId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT IFNULL((SELECT `id` FROM `epc_erp_doc_expiry` WHERE `source_module`=? AND `source_ref_id`=? LIMIT 1), 0)"),
                cancellationToken,
                "insurance",
                id).ConfigureAwait(false);
            if (expiryId > 0)
            {
                if (hasReminders)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?"),
                        cancellationToken,
                        expiryId).ConfigureAwait(false);
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("DELETE FROM `epc_erp_doc_expiry` WHERE `id`=?"),
                    cancellationToken,
                    expiryId).ConfigureAwait(false);
            }
        }

        if (hasDocs)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_erp_ins_documents` WHERE `policy_id`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
        }

        if (hasClaims)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_erp_ins_claims` WHERE `policy_id`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_ins_policies` WHERE `id`=?"),
            cancellationToken,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Policy deleted", id);
    }

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
