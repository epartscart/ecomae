namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>order_fulfillment_bootstrap</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpOrderFulfillmentBootstrapDryRun { ErpOrderFulfillmentBootstrapDryRunResult Evaluate(ErpOrderFulfillmentBootstrapRequest request); }
public sealed class ErpOrderFulfillmentBootstrapDryRun : IErpOrderFulfillmentBootstrapDryRun
{
    public ErpOrderFulfillmentBootstrapDryRunResult Evaluate(ErpOrderFulfillmentBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET order_fulfillment_bootstrap is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=order_fulfillment_bootstrap (NOT executed)"],
            "ERP order_fulfillment_bootstrap payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_bootstrap");
    }
    private static ErpOrderFulfillmentBootstrapDryRunResult Refuse(string s,string c,string d,ErpOrderFulfillmentBootstrapRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_bootstrap");
}
public sealed record ErpOrderFulfillmentBootstrapRequest(bool ConfirmWrites = false);
public sealed record ErpOrderFulfillmentBootstrapDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="order_fulfillment_bootstrap"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
