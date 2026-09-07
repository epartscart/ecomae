namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_ctr_save</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpContractSaveWriteService</c>.
/// </summary>
public interface IErpContractSaveDryRun
{
    ErpContractSaveDryRunResult Evaluate(ErpContractSaveRequest request);
}

public sealed class ErpContractSaveDryRun : IErpContractSaveDryRun
{
    public ErpContractSaveDryRunResult Evaluate(ErpContractSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        var title = (request.Title ?? string.Empty).Trim();
        if (code.Length == 0 || title.Length == 0)
        {
            return Refuse("dry-run-invalid", "code_title_required",
                "Code and title are required (PHP).", request);
        }

        return new ErpContractSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, code, title, request.Id,
            ["INSERT/UPDATE `epc_erp_contracts` (NOT executed)"],
            "ErpContractSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_contracts.php");
    }

    private static ErpContractSaveDryRunResult Refuse(string status, string code, string detail, ErpContractSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Code, request.Title, request.Id, [], detail,
            "content/shop/finance/epc_erp_contracts.php");
}

public sealed record ErpContractSaveRequest(string? Code, string? Title, long Id = 0, bool ConfirmWrites = false);
public sealed record ErpContractSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code, string? Title, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { code = Code, title = Title, id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
