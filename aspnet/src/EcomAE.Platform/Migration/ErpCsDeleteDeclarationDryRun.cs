namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cs_delete_declaration</c> / <c>epc_cs_delete_declaration</c>
/// when <c>confirmWrites</c> is omitted. Live DELETE is
/// <c>IErpCsDeleteDeclarationWriteService</c>.
/// </summary>
public interface IErpCsDeleteDeclarationDryRun
{
    ErpCsDeleteDeclarationDryRunResult Evaluate(ErpCsDeleteDeclarationRequest request);
}

public sealed class ErpCsDeleteDeclarationDryRun : IErpCsDeleteDeclarationDryRun
{
    public ErpCsDeleteDeclarationDryRunResult Evaluate(ErpCsDeleteDeclarationRequest request)
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

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpCsDeleteDeclarationDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["DELETE `epc_custom_shipping_declaration_items` + `epc_custom_shipping_declarations` (NOT executed)"],
            "ErpCsDeleteDeclaration payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_custom_shipping.php");
    }

    private static ErpCsDeleteDeclarationDryRunResult Refuse(string s, string c, string d, ErpCsDeleteDeclarationRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_custom_shipping.php");
}

public sealed record ErpCsDeleteDeclarationRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpCsDeleteDeclarationDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "cs_delete_declaration", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
