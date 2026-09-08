using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_bank_reconcile_match</c> + <c>epc_erp_mark_entry_reconciled</c>
/// / ajax <c>bank_reconcile</c> twin. Schema ensure and statement import stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpBankReconcileWriteService
{
    Task<ErpSimpleWriteResult> MatchAsync(
        ErpBankReconcileWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBankReconcileWriteRequest(
    long LineId = 0,
    long EntryId = 0,
    long AdminId = 0);

public sealed class ErpBankReconcileWriteService : IErpBankReconcileWriteService
{
    public const string InvalidMatch = "Invalid match";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpBankReconcileWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> MatchAsync(
        ErpBankReconcileWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LineId <= 0 || request.EntryId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", InvalidMatch);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_bank_statement_lines", "matched_entry_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_cash_bank_entries", "reconciled", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bank reconciliation tables are not provisioned");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_bank_statement_lines` SET `matched_entry_id` = ? WHERE `id` = ?"),
            cancellationToken,
            request.EntryId,
            request.LineId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_cash_bank_entries` SET `reconciled` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            1,
            request.EntryId).ConfigureAwait(false);
        await _audit.LogAsync(
            connection,
            tx,
            (int)request.AdminId,
            "bank_reconcile",
            "cash_entry",
            request.EntryId,
            "Marked reconciled",
            null,
            cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Bank line matched to cash entry", request.LineId);
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
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
