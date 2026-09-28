namespace EcomAE.Platform.Erp;

public interface IErpFitOutAcceptanceEvidenceReadService
{
    Task<ErpFitOutAcceptanceEvidence> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutAcceptanceCheck(
    string Key,
    string Status,
    string Evidence);

public sealed record ErpFitOutAcceptanceEvidence(
    long ProjectId,
    IReadOnlyList<ErpFitOutAcceptanceCheck> Checks,
    bool MachineChecksPassed,
    string Source,
    string Message);

public sealed class ErpFitOutAcceptanceEvidenceReadService
    : IErpFitOutAcceptanceEvidenceReadService
{
    private readonly IErpFitOutInvoiceBridgeReadService _invoices;
    private readonly IErpFitOutInvoiceReconciliationReadService _reconciliation;
    private readonly IErpFitOutDeliveryDashboardReadService _delivery;
    private readonly IErpFitOutFinanceOperationsReportReadService _finance;

    public ErpFitOutAcceptanceEvidenceReadService(
        IErpFitOutInvoiceBridgeReadService invoices,
        IErpFitOutInvoiceReconciliationReadService reconciliation,
        IErpFitOutDeliveryDashboardReadService delivery,
        IErpFitOutFinanceOperationsReportReadService finance)
    {
        _invoices = invoices;
        _reconciliation = reconciliation;
        _delivery = delivery;
        _finance = finance;
    }

    public async Task<ErpFitOutAcceptanceEvidence> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
        {
            return Empty(projectId, "Project id is required.");
        }

        var invoiceTask = _invoices.ReadAsync(projectId, cancellationToken);
        var reconciliationTask = _reconciliation.ReadAsync(projectId, cancellationToken);
        var deliveryTask = _delivery.ReadAsync(projectId, cancellationToken);
        var financeTask = _finance.ReadAsync(projectId, cancellationToken);
        await Task.WhenAll(invoiceTask, reconciliationTask, deliveryTask, financeTask)
            .ConfigureAwait(false);

        var invoices = await invoiceTask.ConfigureAwait(false);
        var reconciliation = await reconciliationTask.ConfigureAwait(false);
        var delivery = await deliveryTask.ConfigureAwait(false);
        var finance = await financeTask.ConfigureAwait(false);
        var checks = new[]
        {
            Check(
                "invoice-links",
                invoices.Source,
                $"{invoices.Rows.Count} linked invoice row(s)"),
            Check(
                "billing-reconciliation",
                reconciliation.Source,
                $"certified={reconciliation.CertifiedAmount:0.00}; unbilled={reconciliation.UnbilledCertifiedAmount:0.00}"),
            Check(
                "delivery-dashboard",
                delivery.Source,
                $"{delivery.Rows.Count} delivery aggregate row(s)"),
            Check(
                "finance-report",
                finance.Source,
                $"{finance.Rows.Count} finance report row(s)")
        };
        var source = checks.Any(check => check.Status == "database-error")
            ? "database-error"
            : checks.Any(check => check.Status == "migration")
                ? "migration"
                : "database";
        var message = checks.FirstOrDefault(check => check.Status == "database-error")?.Evidence
            ?? string.Empty;
        return new(projectId, checks, source == "database", source, message);
    }

    private static ErpFitOutAcceptanceCheck Check(
        string key,
        string source,
        string evidence)
        => new(key, source, evidence);

    private static ErpFitOutAcceptanceEvidence Empty(
        long projectId,
        string message)
        => new(projectId, Array.Empty<ErpFitOutAcceptanceCheck>(), false, "migration", message);
}
