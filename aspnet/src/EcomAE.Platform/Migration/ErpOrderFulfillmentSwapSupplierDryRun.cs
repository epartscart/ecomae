namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>order_fulfillment_swap_supplier</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpOrderFulfillmentSwapSupplierDryRun { ErpOrderFulfillmentSwapSupplierDryRunResult Evaluate(ErpOrderFulfillmentSwapSupplierRequest request); }
public sealed class ErpOrderFulfillmentSwapSupplierDryRun : IErpOrderFulfillmentSwapSupplierDryRun
{
    public ErpOrderFulfillmentSwapSupplierDryRunResult Evaluate(ErpOrderFulfillmentSwapSupplierRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET order_fulfillment_swap_supplier is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=order_fulfillment_swap_supplier (NOT executed)"],
            "ERP order_fulfillment_swap_supplier payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_swap_supplier");
    }
    private static ErpOrderFulfillmentSwapSupplierDryRunResult Refuse(string s,string c,string d,ErpOrderFulfillmentSwapSupplierRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_swap_supplier");
}
public sealed record ErpOrderFulfillmentSwapSupplierRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpOrderFulfillmentSwapSupplierDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
