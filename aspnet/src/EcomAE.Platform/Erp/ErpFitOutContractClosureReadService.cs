using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutContractClosureReadService
{
    Task<ErpFitOutContractClosure> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutContractClosure(
    long ProjectId,
    bool CanClose,
    int PendingApprovalCount,
    decimal PendingApprovalAmount,
    int ApprovedCompletionCertificateCount,
    int ApprovedFinalSettlementCount,
    decimal UnbilledClientAmount,
    decimal UnpaidVendorAmount,
    decimal UnpaidSubcontractCertifiedAmount,
    decimal SubcontractOverrunAmount,
    decimal RetentionHeld,
    IReadOnlyList<string> Blockers,
    string Source,
    string Message);

public sealed class ErpFitOutContractClosureReadService
    : IErpFitOutContractClosureReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpFitOutCommercialReconciliationReadService _commercial;
    private readonly IErpFitOutSubcontractReconciliationReadService _subcontract;

    public ErpFitOutContractClosureReadService(
        IErpWriteConnectionFactory connections,
        IErpFitOutCommercialReconciliationReadService commercial,
        IErpFitOutSubcontractReconciliationReadService subcontract)
    {
        _connections = connections;
        _commercial = commercial;
        _subcontract = subcontract;
    }

    public async Task<ErpFitOutContractClosure> ReadAsync(
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

        var subcontract = await _subcontract
            .ReadAsync(projectId, cancellationToken)
            .ConfigureAwait(false);
        if (subcontract.Source == "database-error")
        {
            return Empty(projectId, subcontract.Message, subcontract.Source);
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT
                    COALESCE(SUM(CASE WHEN `status`='pending'
                                      AND `record_type` IN (
                                          'approval_request','site_engineer_approval',
                                          'project_manager_approval','variation_approval',
                                          'final_settlement','work_completion_certificate',
                                          'subcontract_payment_certificate',
                                          'client_payment_certificate','retention_release',
                                          'vendor_bill','payment_voucher')
                                      THEN 1 ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `status`='pending'
                                      AND `record_type` IN (
                                          'approval_request','site_engineer_approval',
                                          'project_manager_approval','variation_approval',
                                          'final_settlement','work_completion_certificate',
                                          'subcontract_payment_certificate',
                                          'client_payment_certificate','retention_release',
                                          'vendor_bill','payment_voucher')
                                      THEN `amount` ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type`='work_completion_certificate'
                                      AND `status`='approved' THEN 1 ELSE 0 END),0),
                    COALESCE(SUM(CASE WHEN `record_type`='final_settlement'
                                      AND `status`='approved' THEN 1 ELSE 0 END),0)
                FROM `ecomae_fitout_delivery_records`
                WHERE `project_id`=?
                """);
            ErpDb.AddParameters(command, projectId);
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Empty(projectId, "No fit-out records found for the project.");
            }

            var pendingCount = Convert.ToInt32(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
            var pendingAmount = reader.GetDecimal(1);
            var completionCount = Convert.ToInt32(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture);
            var settlementCount = Convert.ToInt32(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture);
            var blockers = new List<string>();
            if (pendingCount > 0)
            {
                blockers.Add("Pending approval records remain.");
            }

            if (commercial.UnbilledClientAmount > 0m)
            {
                blockers.Add("Certified client work remains unbilled.");
            }

            if (commercial.UnpaidVendorAmount > 0m)
            {
                blockers.Add("Vendor bills remain unpaid.");
            }

            if (subcontract.UnpaidCertifiedAmount > 0m)
            {
                blockers.Add("Certified subcontract work remains unpaid.");
            }

            var subcontractOverrun = Math.Max(
                0m,
                subcontract.MeasuredAmount - subcontract.OrderedAmount);
            if (subcontractOverrun > 0m)
            {
                blockers.Add("Measured subcontract work exceeds ordered value.");
            }

            if (commercial.RetentionHeld > 0m)
            {
                blockers.Add("Retention remains held.");
            }

            if (completionCount == 0)
            {
                blockers.Add("No approved work-completion certificate exists.");
            }

            if (settlementCount == 0)
            {
                blockers.Add("No approved final settlement exists.");
            }

            return new(
                projectId,
                blockers.Count == 0,
                pendingCount,
                pendingAmount,
                completionCount,
                settlementCount,
                commercial.UnbilledClientAmount,
                commercial.UnpaidVendorAmount,
                subcontract.UnpaidCertifiedAmount,
                subcontractOverrun,
                commercial.RetentionHeld,
                blockers,
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static ErpFitOutContractClosure Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(
            projectId,
            false,
            0,
            0m,
            0,
            0,
            0m,
            0m,
            0m,
            0m,
            0m,
            [],
            source,
            message);
}
