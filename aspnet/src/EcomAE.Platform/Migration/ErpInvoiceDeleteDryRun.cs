namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>invoice_delete</c> (draft only). Never DELETE. PHP authoritative.</summary>
public interface IErpInvoiceDeleteDryRun
{
    Task<ErpInvoiceDeleteDryRunResult> EvaluateAsync(ErpInvoiceDeleteRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpInvoiceDeleteDryRun : IErpInvoiceDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;
    public ErpInvoiceDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpInvoiceDeleteDryRunResult> EvaluateAsync(
        ErpInvoiceDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET invoice_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.InvoiceId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "invoiceId must be positive.", request);
        }

        var list = await _dashboards.ListErpInvoicesAsync(150, cancellationToken);
        return EvaluateAgainstInvoices(list.Invoices, request);
    }

    public static ErpInvoiceDeleteDryRunResult EvaluateAgainstInvoices(
        IReadOnlyList<ErpInvoiceDigest> invoices, ErpInvoiceDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET invoice_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.InvoiceId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "invoiceId must be positive.", request);
        }

        var row = invoices.FirstOrDefault(i => i.Id == request.InvoiceId);
        if (row is null)
        {
            return Refuse("dry-run-invalid", "invoice_not_in_digest_window",
                $"Invoice {request.InvoiceId} not in recent digest window.", request);
        }

        if (!string.Equals(row.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse("dry-run-invalid", "not_draft",
                $"Only draft invoices can be deleted — status '{row.Status}' (PHP).", request);
        }

        return new ErpInvoiceDeleteDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, row.Id, row.Status,
            [
                "DELETE FROM `epc_einvoice_lines` WHERE document_id=@id (NOT executed)",
                "DELETE FROM `epc_einvoice_documents` WHERE id=@id AND status='draft' (NOT executed)"
            ],
            "Draft invoice found; hard-delete simulated. Cancel/credit-note path stays PHP for non-draft.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_delete");
    }

    private static ErpInvoiceDeleteDryRunResult Refuse(
        string status, string code, string detail, ErpInvoiceDeleteRequest request) =>
        new(status, 0, true, false, true, code, false, request.InvoiceId, null, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_delete");
}

public sealed record ErpInvoiceDeleteRequest(long InvoiceId, bool ConfirmWrites = false);

public sealed record ErpInvoiceDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long InvoiceId, string? InvoiceStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { invoice_id = InvoiceId },
        current = InvoiceStatus is null ? null : new { status = InvoiceStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
