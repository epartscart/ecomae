using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutSubcontractReconciliationReadService
{
    Task<ErpFitOutSubcontractReconciliation> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutSubcontractReconciliation(
    long ProjectId,
    decimal OrderedAmount,
    decimal MeasuredAmount,
    decimal CertifiedAmount,
    decimal PaymentCertifiedAmount,
    decimal ApprovedPayments,
    decimal UncertifiedAmount,
    decimal UnpaidCertifiedAmount,
    string Source,
    string Message);

public sealed class ErpFitOutSubcontractReconciliationReadService
    : IErpFitOutSubcontractReconciliationReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutSubcontractReconciliationReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutSubcontractReconciliation> ReadAsync(
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

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT
                    COALESCE(SUM(CASE WHEN `record_type`='subcontract_order'
                        AND `status`<>'rejected' THEN `amount` ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type`='subcontract_measurement'
                        AND `status`<>'rejected' THEN `amount` ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type` IN
                        ('subcontract_certification','subcontractor_progress_claim')
                        AND `status`='approved' THEN `amount` ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type`='subcontract_payment_certificate'
                        AND `status`='approved' THEN `amount` ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type`='payment_voucher'
                        AND `status`='approved' THEN `amount` ELSE 0 END),0)
                FROM `ecomae_fitout_delivery_records`
                WHERE `project_id`=?
                """);
            ErpDb.AddParameters(command, projectId);
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Empty(projectId, string.Empty, "database");
            }

            var ordered = reader.GetDecimal(0);
            var measured = reader.GetDecimal(1);
            var certified = reader.GetDecimal(2);
            var paymentCertified = reader.GetDecimal(3);
            var approvedPayments = reader.GetDecimal(4);
            return new(
                projectId,
                ordered,
                measured,
                certified,
                paymentCertified,
                approvedPayments,
                Math.Max(0m, measured - certified),
                Math.Max(0m, paymentCertified - approvedPayments),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static ErpFitOutSubcontractReconciliation Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, 0m, 0m, 0m, 0m, 0m, 0m, 0m, source, message);
}
