namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ins_delete</c> / <c>epc_ins_delete</c> when
/// <c>confirmWrites</c> is omitted. Live DELETE is <c>IErpInsDeleteWriteService</c>.
/// </summary>
public interface IErpInsDeleteDryRun
{
    ErpInsDeleteDryRunResult Evaluate(ErpInsDeleteRequest request);
}

public sealed class ErpInsDeleteDryRun : IErpInsDeleteDryRun
{
    public ErpInsDeleteDryRunResult Evaluate(ErpInsDeleteRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "A policy id is required.", request);
        }

        return new ErpInsDeleteDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id,
            ["DELETE `epc_erp_ins_policies` (NOT executed)"],
            "ErpInsDelete payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_insurance.php");
    }

    private static ErpInsDeleteDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpInsDeleteRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, [], detail,
            "content/shop/finance/epc_erp_insurance.php");
}

public sealed record ErpInsDeleteRequest(long Id = 0, bool ConfirmWrites = false);

public sealed record ErpInsDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, action = "ins_delete" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
