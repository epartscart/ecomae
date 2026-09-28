using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutRetentionAgeingReadService
{
    Task<ErpFitOutRetentionAgeing> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutRetentionAgeing(
    long ProjectId,
    decimal RetentionHeld,
    DateOnly? BaseDate,
    DateOnly? WarrantyEndDate,
    int DaysHeld,
    string AgeingBucket,
    bool ReleaseEligible,
    string EligibilityReason,
    string Source,
    string Message);

public sealed class ErpFitOutRetentionAgeingReadService
    : IErpFitOutRetentionAgeingReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpFitOutCommercialReconciliationReadService _commercial;

    public ErpFitOutRetentionAgeingReadService(
        IErpWriteConnectionFactory connections,
        IErpFitOutCommercialReconciliationReadService commercial)
    {
        _connections = connections;
        _commercial = commercial;
    }

    public async Task<ErpFitOutRetentionAgeing> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
        {
            return Empty(projectId, "Project id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return Empty(projectId, "TenantRegistry DB is not configured.");
        }

        var commercial = await _commercial
            .ReadAsync(projectId, cancellationToken)
            .ConfigureAwait(false);
        if (commercial.Source == "database-error")
        {
            return Empty(projectId, commercial.Message, commercial.Source);
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var baseDate = await ReadBaseDateAsync(connection, projectId, cancellationToken)
                .ConfigureAwait(false);
            var warrantyMonths = await ReadWarrantyMonthsAsync(connection, projectId, cancellationToken)
                .ConfigureAwait(false);
            var warrantyEndDate = baseDate?.AddMonths(warrantyMonths);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daysHeld = baseDate is null
                ? 0
                : Math.Max(0, today.DayNumber - baseDate.Value.DayNumber);
            var eligible = commercial.RetentionHeld <= 0m
                || (warrantyEndDate is not null && today >= warrantyEndDate.Value);
            var reason = commercial.RetentionHeld <= 0m
                ? "No retention balance remains."
                : baseDate is null
                    ? "An approved certified event is required before retention release eligibility can be assessed."
                    : eligible
                        ? "Warranty period has elapsed."
                        : $"Warranty period ends on {warrantyEndDate:yyyy-MM-dd}.";

            return new(
                projectId,
                commercial.RetentionHeld,
                baseDate,
                warrantyEndDate,
                daysHeld,
                Bucket(daysHeld),
                eligible,
                reason,
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static async Task<DateOnly?> ReadBaseDateAsync(
        DbConnection connection,
        long projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT MIN(`event_date`)
            FROM `ecomae_fitout_delivery_records`
            WHERE `project_id`=?
              AND `status`='approved'
              AND `record_type` IN (
                  'work_completion_certificate',
                  'subcontract_certification',
                  'progress_claim',
                  'subcontractor_progress_claim',
                  'client_progress_claim'
              )
            """);
        ErpDb.AddParameters(command, projectId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DateTime date
            ? DateOnly.FromDateTime(date)
            : null;
    }

    private static async Task<int> ReadWarrantyMonthsAsync(
        DbConnection connection,
        long projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COALESCE(`warranty_months`,0) FROM `ecomae_fitout_contract_terms` WHERE `contract_id`=? LIMIT 1");
        ErpDb.AddParameters(command, projectId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null || value is DBNull
            ? 0
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Bucket(int days) =>
        days switch
        {
            < 31 => "0-30",
            < 91 => "31-90",
            < 181 => "91-180",
            _ => "181+"
        };

    private static ErpFitOutRetentionAgeing Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(
            projectId,
            0m,
            null,
            null,
            0,
            "0-30",
            false,
            message,
            source,
            message);
}
