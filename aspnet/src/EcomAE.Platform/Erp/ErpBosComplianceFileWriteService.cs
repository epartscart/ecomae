using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_compliance_set_filing</c> / ajax <c>bos_compliance_file</c>
/// twin. UPSERT <c>epc_bos_compliance_filings</c> on unique
/// <c>(obligation_id, period_label)</c>. Does not CREATE tables. Disable is
/// already ASP.NET-live. Add, retention, seed, and schema ensure stay PHP.
/// </summary>
public interface IErpBosComplianceFileWriteService
{
    Task<ErpSimpleWriteResult> FileAsync(
        ErpBosComplianceFileWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosComplianceFileWriteRequest(
    long ObligationId = 0,
    string? PeriodLabel = null,
    long PeriodEnd = 0,
    long DueDate = 0,
    string? Status = null,
    string? Reference = null,
    string? Notes = null,
    long AdminId = 0);

public sealed class ErpBosComplianceFileWriteService : IErpBosComplianceFileWriteService
{
    internal static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "open", "filed", "waived",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosComplianceFileWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> FileAsync(
        ErpBosComplianceFileWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ObligationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select an obligation");
        }

        var period = Clip((request.PeriodLabel ?? string.Empty).Trim(), 48);
        if (period.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Period label is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = (request.Status ?? "filed").Trim();
        if (!Statuses.Contains(status))
        {
            status = "open";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filedAt = status == "filed" ? now : 0;
        var periodEnd = request.PeriodEnd < 0 ? 0 : request.PeriodEnd;
        var dueDate = request.DueDate < 0 ? 0 : request.DueDate;
        var reference = Clip((request.Reference ?? string.Empty).Trim(), 120);
        var notes = (request.Notes ?? string.Empty).Trim();
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_compliance_filings", "obligation_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_compliance_filings", "period_label", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_compliance_filings", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Compliance filing table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_compliance_filings` (`obligation_id`,`period_label`,`period_end`,`due_date`,`status`,`filed_at`,`reference`,`notes`,`admin_id`,`time`) VALUES (?,?,?,?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `filed_at` = VALUES(`filed_at`), `reference` = VALUES(`reference`), `notes` = VALUES(`notes`), `time` = VALUES(`time`)"),
            cancellationToken,
            request.ObligationId, period, periodEnd, dueDate, status, filedAt, reference, notes, adminId, now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (inserted <= 0)
        {
            inserted = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_bos_compliance_filings` WHERE `obligation_id`=? AND `period_label`=? LIMIT 1"),
                cancellationToken,
                request.ObligationId, period).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Filing status saved", inserted);
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
