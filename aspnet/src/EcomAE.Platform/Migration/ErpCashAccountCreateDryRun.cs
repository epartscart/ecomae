namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>create_account</c> / <c>epc_erp_create_cash_account</c>.</summary>
public interface IErpCashAccountCreateDryRun
{
    ErpCashAccountCreateDryRunResult Evaluate(ErpCashAccountCreateRequest request);
}

public sealed class ErpCashAccountCreateDryRun : IErpCashAccountCreateDryRun
{
    public ErpCashAccountCreateDryRunResult Evaluate(ErpCashAccountCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET create_account is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return Refuse("dry-run-invalid", "name_required", "Cash/bank account name is required.", request);
        }

        var type = string.Equals(request.AccountType, "bank", StringComparison.OrdinalIgnoreCase) ? "bank" : "cash";
        return new ErpCashAccountCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, name, type,
            ["INSERT INTO `epc_erp_cash_bank_accounts` (…) (NOT executed)"],
            "Payload shape validated; cash account INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_account");
    }

    private static ErpCashAccountCreateDryRunResult Refuse(
        string status, string code, string detail, ErpCashAccountCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.Name, request.AccountType, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_account");
}

public sealed record ErpCashAccountCreateRequest(string? Name, string? AccountType = "cash", bool ConfirmWrites = false);

public sealed record ErpCashAccountCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Name, string? AccountType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { name = Name, account_type = AccountType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
