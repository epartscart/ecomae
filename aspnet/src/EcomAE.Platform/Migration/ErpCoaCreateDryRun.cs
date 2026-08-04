namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>create_coa</c> / <c>epc_erp_gl_create_coa</c>.</summary>
public interface IErpCoaCreateDryRun
{
    ErpCoaCreateDryRunResult Evaluate(ErpCoaCreateRequest request);
}

public sealed class ErpCoaCreateDryRun : IErpCoaCreateDryRun
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "asset", "liability", "equity", "revenue", "expense"
    };

    public ErpCoaCreateDryRunResult Evaluate(ErpCoaCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET create_coa is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return Refuse("dry-run-invalid", "code_required", "Account code required (PHP).", request);
        }

        var type = (request.AccountType ?? "expense").Trim();
        if (!AllowedTypes.Contains(type))
        {
            return Refuse("dry-run-invalid", "invalid_account_type", "Invalid account type (PHP).", request);
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return Refuse("dry-run-invalid", "name_required", "COA account name is required.", request);
        }

        return new ErpCoaCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, code, name, type,
            ["INSERT INTO `epc_erp_coa_accounts` (…) (NOT executed)"],
            "Payload shape validated; COA INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_coa");
    }

    private static ErpCoaCreateDryRunResult Refuse(
        string status, string code, string detail, ErpCoaCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.Code, request.Name, request.AccountType,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=create_coa");
}

public sealed record ErpCoaCreateRequest(string? Code, string? Name, string? AccountType = "expense", bool ConfirmWrites = false);

public sealed record ErpCoaCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code, string? Name, string? AccountType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { code = Code, name = Name, account_type = AccountType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
