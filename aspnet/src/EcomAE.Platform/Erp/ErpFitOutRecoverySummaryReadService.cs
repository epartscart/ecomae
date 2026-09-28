using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutRecoverySummaryReadService
{
    Task<ErpFitOutRecoverySummary> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutRecoverySummary(
    long ProjectId,
    decimal AdvancePercent,
    decimal RetentionPercent,
    decimal CertifiedAmount,
    decimal AdvanceRecovered,
    decimal RetentionRecovered,
    decimal AdvanceOutstanding,
    decimal RetentionOutstanding,
    string Source,
    string Message);

public sealed class ErpFitOutRecoverySummaryReadService : IErpFitOutRecoverySummaryReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutRecoverySummaryReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutRecoverySummary> ReadAsync(
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

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            decimal advancePercent;
            decimal retentionPercent;
            await using (var terms = connection.CreateCommand())
            {
                terms.CommandText = ErpDb.Positional("""
                    SELECT `advance_percent`,`retention_percent`
                    FROM `ecomae_fitout_contract_terms`
                    WHERE `contract_id`=?
                    LIMIT 1
                    """);
                ErpDb.AddParameters(terms, projectId);
                await using var reader = await terms.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Empty(projectId, "No contract terms found for the project.");
                }

                advancePercent = reader.GetDecimal(0);
                retentionPercent = reader.GetDecimal(1);
            }

            decimal certified = 0m;
            decimal advanceRecovered = 0m;
            decimal retentionRecovered = 0m;
            await using (var records = connection.CreateCommand())
            {
                records.CommandText = ErpDb.Positional("""
                    SELECT `record_type`,COALESCE(SUM(`amount`),0)
                    FROM `ecomae_fitout_delivery_records`
                    WHERE `project_id`=?
                      AND `record_type` IN ('subcontract_certification','progress_claim','advance_recovery','retention_recovery')
                    GROUP BY `record_type`
                    """);
                ErpDb.AddParameters(records, projectId);
                await using var reader = await records.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var amount = reader.GetDecimal(1);
                    switch (reader.GetString(0))
                    {
                        case "subcontract_certification":
                        case "progress_claim":
                            certified += amount;
                            break;
                        case "advance_recovery":
                            advanceRecovered += amount;
                            break;
                        case "retention_recovery":
                            retentionRecovered += amount;
                            break;
                    }
                }
            }

            var advanceDue = decimal.Round(certified * advancePercent / 100m, 2, MidpointRounding.AwayFromZero);
            var retentionDue = decimal.Round(certified * retentionPercent / 100m, 2, MidpointRounding.AwayFromZero);
            return new(
                projectId,
                advancePercent,
                retentionPercent,
                certified,
                advanceRecovered,
                retentionRecovered,
                Math.Max(0m, advanceDue - advanceRecovered),
                Math.Max(0m, retentionDue - retentionRecovered),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static ErpFitOutRecoverySummary Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, 0m, 0m, 0m, 0m, 0m, 0m, 0m, source, message);
}
