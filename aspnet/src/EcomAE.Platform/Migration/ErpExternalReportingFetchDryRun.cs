namespace EcomAE.Platform.Migration;

/// <summary>Dry-run for PHP External Reporting fetch / import / intake. Never writes. PHP remains authoritative.</summary>
public interface IErpExternalReportingFetchDryRun
{
    ErpExternalReportingFetchDryRunResult Evaluate(ErpExternalReportingFetchRequest request);
}

public sealed class ErpExternalReportingFetchDryRun : IErpExternalReportingFetchDryRun
{
    public ErpExternalReportingFetchDryRunResult Evaluate(ErpExternalReportingFetchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var action = string.IsNullOrWhiteSpace(request.Action) ? "fetch" : request.Action.Trim().ToLowerInvariant();
        if (action is not ("fetch" or "import" or "intake"))
        {
            return Refuse("dry-run-invalid", "invalid_request", "action must be fetch, import, or intake.", request);
        }

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET external-report generate is not implemented; PHP erp_tabs_external_reports.php remains authoritative.",
                request);
        }

        var php = "/CP/content/shop/finance/erp/erp_tabs_external_reports.php";
        return new(
            "dry-run-validated",
            0,
            true,
            false,
            true,
            "ok",
            true,
            action,
            request.ReportKey,
            [$"{php}?tool={action} (NOT executed)"],
            "External reporting " + action + " validated; generate/file write blocked.",
            php);
    }

    private static ErpExternalReportingFetchDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpExternalReportingFetchRequest request) =>
        new(status, 0, true, false, true, code, false, request.Action, request.ReportKey, [], detail,
            "/CP/content/shop/finance/erp/erp_tabs_external_reports.php");
}

public sealed record ErpExternalReportingFetchRequest(
    string? Action = "fetch",
    string? ReportKey = null,
    bool ConfirmWrites = false);

public sealed record ErpExternalReportingFetchDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    string? Action,
    string? ReportKey,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
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
        intended = new { action = Action, report = ReportKey },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
