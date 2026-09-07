namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wht_certificate</c> / <c>epc_wht_certificate_issue</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpWhtCertificateWriteService</c>.
/// </summary>
public interface IErpWhtCertificateDryRun
{
    ErpWhtCertificateDryRunResult Evaluate(ErpWhtCertificateRequest request);
}

public sealed class ErpWhtCertificateDryRun : IErpWhtCertificateDryRun
{
    public ErpWhtCertificateDryRunResult Evaluate(ErpWhtCertificateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        if (request.Id <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Transaction not found", request);
        }

        return new ErpWhtCertificateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPDATE `epc_wht_txn`.certificate_no (NOT executed)"],
            "ErpWhtCertificate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_withholding.php");
    }

    private static ErpWhtCertificateDryRunResult Refuse(string s, string c, string d, ErpWhtCertificateRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_withholding.php");
}

public sealed record ErpWhtCertificateRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    string? CertificateNo = null);

public sealed record ErpWhtCertificateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "wht_certificate", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
