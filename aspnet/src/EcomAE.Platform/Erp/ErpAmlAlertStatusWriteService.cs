using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_aml_alert_set_status</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpAmlAlertStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(
        ErpAmlAlertStatusWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAmlAlertStatusWriteRequest(
    long Id = 0,
    string? TargetStatus = null,
    int ReviewedBy = 0,
    bool FileSar = false,
    string? SarReference = null);

public sealed class ErpAmlAlertStatusWriteService : IErpAmlAlertStatusWriteService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "open", "reviewed", "escalated", "closed"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlAlertStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        ErpAmlAlertStatusWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "id must be positive.");
        }

        var status = (request.TargetStatus ?? string.Empty).Trim();
        if (!Allowed.Contains(status))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_aml_transactions", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_aml_transactions", "review_status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "AML transaction tables are not provisioned");
        }

        var reviewedBy = request.ReviewedBy < 0 ? 0 : request.ReviewedBy;
        var reviewedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var fileSar = request.FileSar ? 1 : 0;
        var sarRef = Clip((request.SarReference ?? string.Empty).Trim(), 64);

        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_aml_transactions` SET `review_status`=?, `reviewed_by`=?, `reviewed_at`=?, `sar_filed`=IF(?=1,1,`sar_filed`), `sar_reference`=IF(?<>'',?,`sar_reference`) WHERE `id`=?"),
            cancellationToken,
            status,
            reviewedBy,
            reviewedAt,
            fileSar,
            sarRef,
            sarRef,
            request.Id).ConfigureAwait(false);
        if (updated <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Alert not found");
        }

        return ErpSimpleWriteResult.Ok("Alert updated", request.Id);
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

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
