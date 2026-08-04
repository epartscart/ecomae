namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>proc_req_save</c>. Never INSERT/UPDATE. PHP authoritative.</summary>
public interface IErpProcReqSaveDryRun
{
    ErpProcReqSaveDryRunResult Evaluate(ErpProcReqSaveRequest request);
}

public sealed class ErpProcReqSaveDryRun : IErpProcReqSaveDryRun
{
    public ErpProcReqSaveDryRunResult Evaluate(ErpProcReqSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET proc_req_save is not implemented; PHP ajax_erp.php remains authoritative.", request);

        var requester = (request.Requester ?? string.Empty).Trim();
        if (requester.Length == 0)
            return Refuse("dry-run-invalid", "requester_required", "Requester is required (PHP).", request);

        return new ErpProcReqSaveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, requester, request.Id,
            ["epc_proc_req_save(@data, @id) (NOT executed)"],
            "Procurement requisition save payload validated; write blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=proc_req_save");
    }

    private static ErpProcReqSaveDryRunResult Refuse(string status, string code, string detail, ErpProcReqSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.Requester, request.Id, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=proc_req_save");
}

public sealed record ErpProcReqSaveRequest(string? Requester, long Id = 0, bool ConfirmWrites = false);
public sealed record ErpProcReqSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Requester, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { requester = Requester, id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
