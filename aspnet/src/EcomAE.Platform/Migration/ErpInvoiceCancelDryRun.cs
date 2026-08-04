namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>invoice_cancel</c>.
/// Simulates soft-cancel UPDATE on epc_einvoice_documents; credit-notes stay PHP.
/// </summary>
public interface IErpInvoiceCancelDryRun
{
    Task<ErpInvoiceCancelDryRunResult> EvaluateAsync(
        ErpInvoiceCancelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpInvoiceCancelDryRun : IErpInvoiceCancelDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpInvoiceCancelDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpInvoiceCancelDryRunResult> EvaluateAsync(
        ErpInvoiceCancelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET invoice_cancel is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.InvoiceId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "invoiceId must be positive.", request);
        }

        var list = await _dashboards.ListErpInvoicesAsync(200, cancellationToken);
        return EvaluateAgainstInvoices(list.Invoices, request);
    }

    public static ErpInvoiceCancelDryRunResult EvaluateAgainstInvoices(
        IReadOnlyList<ErpInvoiceDigest> invoices,
        ErpInvoiceCancelRequest request)
    {
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET invoice_cancel is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.InvoiceId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "invoiceId must be positive.", request);
        }

        var invoice = invoices.FirstOrDefault(i => i.Id == request.InvoiceId);
        if (invoice is null)
        {
            return Refuse("dry-run-invalid", "invoice_not_in_digest_window",
                $"Invoice {request.InvoiceId} not present in recent /erp/invoices digest window.",
                request);
        }

        var status = (invoice.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is "submitted" or "accepted" or "queued")
        {
            return Refuse("dry-run-invalid", "invoice_not_cancellable",
                $"Invoice status '{invoice.Status}' cannot be cancelled — issue a credit note instead (PHP epc_erp_doc_can_void).",
                request);
        }

        if (status is "cancelled" or "canceled")
        {
            return Refuse("dry-run-invalid", "invoice_already_cancelled",
                $"Invoice {request.InvoiceId} is already cancelled.",
                request);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Invoice cancelled" : request.Reason.Trim();
        if (reason.Length > 255)
        {
            reason = reason[..255];
        }

        return new ErpInvoiceCancelDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            InvoiceStatus: invoice.Status,
            TotalInclVat: invoice.TotalInclVat,
            Reason: reason,
            SimulatedSql:
            [
                "UPDATE `epc_einvoice_documents` SET `status`='cancelled', `active`=0, `time_updated`=@now WHERE `id`=@id AND `status` NOT IN ('submitted','accepted','queued') (NOT executed)",
                "Audit log cancel remains PHP-only in this dry-run slice"
            ],
            Detail: "Invoice found in digest window and not submitted; cancel UPDATE simulated. Credit notes stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_cancel");
    }

    private static ErpInvoiceCancelDryRunResult Refuse(
        string status, string code, string detail, ErpInvoiceCancelRequest request) =>
        new(status, 0, true, false, true, code, false, request.InvoiceId, null, null, null,
            request.Reason, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_cancel");
}

public sealed record ErpInvoiceCancelRequest(long InvoiceId, string? Reason = null, bool ConfirmWrites = false);

public sealed record ErpInvoiceCancelDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long InvoiceId, string? InvoiceNumber, string? InvoiceStatus,
    decimal? TotalInclVat, string? Reason, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { invoice_id = InvoiceId, reason = Reason },
        current = InvoiceNumber is null ? null : new { invoice_number = InvoiceNumber, status = InvoiceStatus, total_incl_vat = TotalInclVat },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
