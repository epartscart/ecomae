namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>order_fulfillment_sync</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpOrderFulfillmentSyncDryRun { ErpOrderFulfillmentSyncDryRunResult Evaluate(ErpOrderFulfillmentSyncRequest request); }
public sealed class ErpOrderFulfillmentSyncDryRun : IErpOrderFulfillmentSyncDryRun
{
    public ErpOrderFulfillmentSyncDryRunResult Evaluate(ErpOrderFulfillmentSyncRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET order_fulfillment_sync is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=order_fulfillment_sync (NOT executed)"],
            "ERP order_fulfillment_sync payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_sync");
    }
    private static ErpOrderFulfillmentSyncDryRunResult Refuse(string s,string c,string d,ErpOrderFulfillmentSyncRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=order_fulfillment_sync");
}
public sealed record ErpOrderFulfillmentSyncRequest(bool ConfirmWrites = false);
public sealed record ErpOrderFulfillmentSyncDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="order_fulfillment_sync"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
