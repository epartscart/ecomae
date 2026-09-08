namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>costm_item_set</c> / <c>epc_costm_item_set</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpCostmItemSetWriteService</c>.
/// </summary>
public interface IErpCostmItemSetDryRun
{
    ErpCostmItemSetDryRunResult Evaluate(ErpCostmItemSetRequest request);
}

public sealed class ErpCostmItemSetDryRun : IErpCostmItemSetDryRun
{
    public ErpCostmItemSetDryRunResult Evaluate(ErpCostmItemSetRequest request)
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

        var model = request.Model ?? "moving_avg";
        var invalid = EcomAE.Platform.Erp.ErpCostmItemSetWriteService.Validate(model);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpCostmItemSetDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ItemId, model,
            ["INSERT `epc_costm_item` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpCostmItemSet payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cost_models.php");
    }

    private static ErpCostmItemSetDryRunResult Refuse(string s, string c, string d, ErpCostmItemSetRequest r) =>
        new(s, 0, true, false, false, c, false, r.ItemId, r.Model, [], d, "content/shop/finance/epc_erp_cost_models.php");
}

public sealed record ErpCostmItemSetRequest(
    long ItemId = 0,
    string? Model = null,
    decimal StdCost = 0,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpCostmItemSetDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ItemId, string? Model,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "costm_item_set", item_id = ItemId, model = Model },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
