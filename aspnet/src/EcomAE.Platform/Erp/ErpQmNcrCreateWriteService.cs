using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_qm_ncr_create</c> twin over the PHP-owned NCR table.</summary>
public interface IErpQmNcrCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpQmNcrCreateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpQmNcrCreateWriteRequest(
    long CompanyId = 0,
    long OrderId = 0,
    string? Title = null,
    string? Severity = null,
    string? Disposition = null,
    int ActorUserId = 0);

public sealed class ErpQmNcrCreateWriteService : IErpQmNcrCreateWriteService
{
    private static readonly HashSet<string> Severities = new(StringComparer.Ordinal)
    {
        "minor", "major", "critical",
    };

    private static readonly HashSet<string> Dispositions = new(StringComparer.Ordinal)
    {
        "use_as_is", "rework", "scrap", "return",
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpQmNcrCreateWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpQmNcrCreateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = (request.Title ?? string.Empty).Trim();
        if (request.CompanyId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Company is required.");
        }

        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "NCR title is required.");
        }

        var severity = request.Severity ?? "minor";
        if (!Severities.Contains(severity))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid severity");
        }

        var disposition = request.Disposition ?? string.Empty;
        if (disposition.Length > 0 && !Dispositions.Contains(disposition))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid disposition");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_qm_ncr", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "NCR table is not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_qm_ncr`"
                + " (`company_id`,`order_id`,`title`,`severity`,`disposition`,`status`,`corrective_action`,`time_created`)"
                + " VALUES (?,?,?,?,?,'open','',?)"),
            cancellationToken,
            request.CompanyId,
            request.OrderId > 0 ? request.OrderId : 0,
            title,
            severity,
            disposition,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var id = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT LAST_INSERT_ID()"),
            cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(
            connection,
            transaction,
            request.ActorUserId,
            "qm_ncr_create",
            "qm_ncr",
            id,
            "Non-conformance created",
            new Dictionary<string, string?>
            {
                ["company_id"] = request.CompanyId.ToString(),
                ["order_id"] = request.OrderId > 0 ? request.OrderId.ToString() : "0",
                ["title"] = title,
                ["severity"] = severity,
                ["disposition"] = disposition,
                ["status"] = "open",
            },
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Non-conformance created", id);
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}
