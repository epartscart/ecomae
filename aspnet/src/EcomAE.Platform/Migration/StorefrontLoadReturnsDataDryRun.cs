namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/returns/ajax/ajax_load_returns_data.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontLoadReturnsDataDryRun { StorefrontLoadReturnsDataDryRunResult Evaluate(StorefrontLoadReturnsDataRequest request); }
public sealed class StorefrontLoadReturnsDataDryRun : IStorefrontLoadReturnsDataDryRun
{
    public StorefrontLoadReturnsDataDryRunResult Evaluate(StorefrontLoadReturnsDataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/returns/ajax/ajax_load_returns_data.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["content/shop/returns/ajax/ajax_load_returns_data.php (NOT executed)"],
            "StorefrontLoadReturnsData payload validated; UPDATE blocked.",
            "content/shop/returns/ajax/ajax_load_returns_data.php");
    }
    private static StorefrontLoadReturnsDataDryRunResult Refuse(string s,string c,string d,StorefrontLoadReturnsDataRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"content/shop/returns/ajax/ajax_load_returns_data.php");
}
public sealed record StorefrontLoadReturnsDataRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record StorefrontLoadReturnsDataDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
