namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/order_process/ajax_check_order_not_authorized.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontCheckOrderNotAuthorizedDryRun { StorefrontCheckOrderNotAuthorizedDryRunResult Evaluate(StorefrontCheckOrderNotAuthorizedRequest request); }
public sealed class StorefrontCheckOrderNotAuthorizedDryRun : IStorefrontCheckOrderNotAuthorizedDryRun
{
    public StorefrontCheckOrderNotAuthorizedDryRunResult Evaluate(StorefrontCheckOrderNotAuthorizedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/order_process/ajax_check_order_not_authorized.php remains authoritative.", request);
        if (request.OrderId <= 0)
            return Refuse("dry-run-invalid","invalid_request","OrderId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.OrderId,
            ["content/shop/order_process/ajax_check_order_not_authorized.php (NOT executed)"],
            "StorefrontCheckOrderNotAuthorized payload validated; UPDATE blocked.",
            "content/shop/order_process/ajax_check_order_not_authorized.php");
    }
    private static StorefrontCheckOrderNotAuthorizedDryRunResult Refuse(string s,string c,string d,StorefrontCheckOrderNotAuthorizedRequest r)=>
        new(s,0,true,false,true,c,false,r.OrderId,[],d,"content/shop/order_process/ajax_check_order_not_authorized.php");
}
public sealed record StorefrontCheckOrderNotAuthorizedRequest(long OrderId, bool ConfirmWrites = false);
public sealed record StorefrontCheckOrderNotAuthorizedDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long OrderId,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{orderId=OrderId},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
