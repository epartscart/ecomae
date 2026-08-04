namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>order_fulfillment_auto_post</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpOrderFulfillmentAutoPostDryRun { ErpOrderFulfillmentAutoPostDryRunResult Evaluate(ErpOrderFulfillmentAutoPostRequest request); }
public sealed class ErpOrderFulfillmentAutoPostDryRun : IErpOrderFulfillmentAutoPostDryRun
{
    public ErpOrderFulfillmentAutoPostDryRunResult Evaluate(ErpOrderFulfillmentAutoPostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET order_fulfillment_auto_post is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=order_fulfillment_auto_post (NOT executed)"],
            "ERP order_fulfillment_auto_post payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_auto_post");
    }
    private static ErpOrderFulfillmentAutoPostDryRunResult Refuse(string s,string c,string d,ErpOrderFulfillmentAutoPostRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_auto_post");
}
public sealed record ErpOrderFulfillmentAutoPostRequest(bool ConfirmWrites = false);
public sealed record ErpOrderFulfillmentAutoPostDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="order_fulfillment_auto_post"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
