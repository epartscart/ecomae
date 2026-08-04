namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>ctr_save</c>. Never INSERT/UPDATE. PHP authoritative.</summary>
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
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET ctr_save is not implemented; PHP ajax_erp.php remains authoritative.", request);

        var code = (request.Code ?? string.Empty).Trim();
        var title = (request.Title ?? string.Empty).Trim();
        if (code.Length == 0 || title.Length == 0)
            return Refuse("dry-run-invalid", "code_title_required",
                "Code and title are required (PHP).", request);

        return new ErpContractSaveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, code, title, request.Id,
            ["epc_ctr_save(@data, @id) INSERT/UPDATE (NOT executed)"],
            "Contract save payload validated; write blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_save");
    }

    private static ErpContractSaveDryRunResult Refuse(string status, string code, string detail, ErpContractSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.Code, request.Title, request.Id, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_save");
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
