namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>qm_order_create</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpQmOrderCreateDryRun { ErpQmOrderCreateDryRunResult Evaluate(ErpQmOrderCreateRequest request); }
public sealed class ErpQmOrderCreateDryRun : IErpQmOrderCreateDryRun
{
    public ErpQmOrderCreateDryRunResult Evaluate(ErpQmOrderCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes is required for persistence and is handled by the guarded ASP.NET writer.", request);
        if (request.CompanyId <= 0)
            return Refuse("dry-run-invalid","company_required","company_id is required.", request);
        if (request.PlanId < 0 || request.ItemId < 0 || request.Qty < 0)
            return Refuse("dry-run-invalid","invalid_request","plan_id, item_id, and qty must be non-negative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.CompanyId, request.PlanId,
            request.RefType, request.RefId, request.ItemId, request.Qty,
            ["ajax_erp.php?action=qm_order_create (NOT executed)"],
            "ERP qm_order_create payload validated; INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=qm_order_create");
    }
    private static ErpQmOrderCreateDryRunResult Refuse(string s,string c,string d,ErpQmOrderCreateRequest r)=>
        new(s,0,true,false,true,c,false,r.CompanyId, r.PlanId, r.RefType, r.RefId, r.ItemId, r.Qty, [],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=qm_order_create");
}
public sealed record ErpQmOrderCreateRequest(
    long CompanyId = 0,
    long PlanId = 0,
    string? RefType = null,
    string? RefId = null,
    long ItemId = 0,
    decimal Qty = 0,
    bool ConfirmWrites = false);
public sealed record ErpQmOrderCreateDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long CompanyId,
    long PlanId,
    string? RefType,
    string? RefId,
    long ItemId,
    decimal Qty,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
{
    public object ToPayload(object session)=>new
    {
        ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,
        cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,
        validation_code=ValidationCode,would_write=WouldWrite,
        intended=new { company_id=CompanyId, plan_id=PlanId, ref_type=RefType ?? "item",
            ref_id=RefId ?? string.Empty, item_id=ItemId, qty=Qty, status="open", verdict=string.Empty },
        simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail
    };
}
