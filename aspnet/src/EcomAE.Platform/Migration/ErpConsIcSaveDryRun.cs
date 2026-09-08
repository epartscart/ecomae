namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cons_ic_save</c> / <c>epc_cons_ic_save</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT is <c>IErpConsIcSaveWriteService</c>.
/// </summary>
public interface IErpConsIcSaveDryRun
{
    ErpConsIcSaveDryRunResult Evaluate(ErpConsIcSaveRequest request);
}

public sealed class ErpConsIcSaveDryRun : IErpConsIcSaveDryRun
{
    public ErpConsIcSaveDryRunResult Evaluate(ErpConsIcSaveRequest request)
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

        var from = (request.FromEntity ?? string.Empty).Trim();
        var to = (request.ToEntity ?? string.Empty).Trim();
        if (from.Length == 0 || to.Length == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "From and to entities are required", request);
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Intercompany needs two different entities", request);
        }

        if (request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Amount must be positive", request);
        }

        return new ErpConsIcSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.FromEntity, request.ToEntity, request.Amount,
            ["INSERT `epc_cons_ic` (NOT executed)"],
            "ErpConsIcSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_consolidation.php");
    }

    private static ErpConsIcSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpConsIcSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.FromEntity, request.ToEntity, request.Amount, [], detail,
            "content/shop/finance/epc_erp_consolidation.php");
}

public sealed record ErpConsIcSaveRequest(
    string? FromEntity = null,
    string? ToEntity = null,
    decimal Amount = 0,
    bool ConfirmWrites = false);

public sealed record ErpConsIcSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? FromEntity, string? ToEntity, decimal Amount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { fromEntity = FromEntity, toEntity = ToEntity, amount = Amount, action = "cons_ic_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
