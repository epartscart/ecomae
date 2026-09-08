namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ctr_sign</c> / <c>epc_ctr_sign</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpCtrSignWriteService</c>.
/// </summary>
public interface IErpCtrSignDryRun
{
    ErpCtrSignDryRunResult Evaluate(ErpCtrSignRequest request);
}

public sealed class ErpCtrSignDryRun : IErpCtrSignDryRun
{
    public ErpCtrSignDryRunResult Evaluate(ErpCtrSignRequest request)
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

        return new ErpCtrSignDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["ajax_erp.php?action=ctr_sign (NOT executed)"],
            "ErpCtrSign payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_sign");
    }

    private static ErpCtrSignDryRunResult Refuse(string s, string c, string d, ErpCtrSignRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_sign");
}

public sealed record ErpCtrSignRequest(long Id = 0, bool ConfirmWrites = false);

public sealed record ErpCtrSignDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "ctr_sign", id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
