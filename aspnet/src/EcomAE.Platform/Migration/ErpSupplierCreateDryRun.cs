namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>create_supplier</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpSupplierCreateDryRun
{
    ErpSupplierCreateDryRunResult Evaluate(ErpSupplierCreateRequest request);
}

public sealed class ErpSupplierCreateDryRun : IErpSupplierCreateDryRun
{
    public ErpSupplierCreateDryRunResult Evaluate(ErpSupplierCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET create_supplier is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return Refuse("dry-run-invalid", "name_required", "Supplier name is required.", request);
        }

        return new ErpSupplierCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, name,
            [
                "INSERT INTO `epc_erp_suppliers` (…) (NOT executed)",
                "Dimension save vendor (NOT executed)"
            ],
            "Payload shape validated; supplier INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_supplier");
    }

    private static ErpSupplierCreateDryRunResult Refuse(
        string status, string code, string detail, ErpSupplierCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.Name, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_supplier");
}

public sealed record ErpSupplierCreateRequest(string? Name, string? ContactEmail = null, bool ConfirmWrites = false);

public sealed record ErpSupplierCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Name, IReadOnlyList<string> SimulatedSql,
    string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { name = Name }, simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
