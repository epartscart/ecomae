namespace EcomAE.Platform.Migration;

/// <summary>HTML form dry-run for jewellery masters / fixing / retail / stock saves. Never UPDATE. PHP authoritative.</summary>
public interface IErpJwModuleSaveDryRun
{
    ErpJwModuleSaveDryRunResult Evaluate(ErpJwModuleSaveRequest request);
}

public sealed class ErpJwModuleSaveDryRun : IErpJwModuleSaveDryRun
{
    public ErpJwModuleSaveDryRunResult Evaluate(ErpJwModuleSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET jewellery module save is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var action = (request.Action ?? string.Empty).Trim();
        if (action.Length == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "action is required.", request);
        }

        var code = (request.Code ?? string.Empty).Trim();
        return new(
            "dry-run-validated",
            0,
            true,
            false,
            true,
            "ok",
            true,
            action,
            code,
            [$"ajax_erp.php?action={action} (NOT executed)"],
            $"ERP {action} payload validated; UPDATE blocked.",
            $"/CP/content/shop/finance/erp/ajax_erp.php?action={Uri.EscapeDataString(action)}");
    }

    private static ErpJwModuleSaveDryRunResult Refuse(string s, string c, string d, ErpJwModuleSaveRequest r) =>
        new(s, 0, true, false, true, c, false, r.Action, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php");
}

public sealed record ErpJwModuleSaveRequest(string Action, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpJwModuleSaveDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    string? Action,
    string? Code,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax);
