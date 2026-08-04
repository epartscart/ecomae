namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/finance/ajax_create_operation.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontCreateOperationDryRun { StorefrontCreateOperationDryRunResult Evaluate(StorefrontCreateOperationRequest request); }
public sealed class StorefrontCreateOperationDryRun : IStorefrontCreateOperationDryRun
{
    public StorefrontCreateOperationDryRunResult Evaluate(StorefrontCreateOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/finance/ajax_create_operation.php remains authoritative.", request);
        if (request.Amount <= 0)
            return Refuse("dry-run-invalid","invalid_request","Amount must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Amount, request.Kind,
            ["content/shop/finance/ajax_create_operation.php (NOT executed)"],
            "StorefrontCreateOperation payload validated; UPDATE blocked.",
            "content/shop/finance/ajax_create_operation.php");
    }
    private static StorefrontCreateOperationDryRunResult Refuse(string s,string c,string d,StorefrontCreateOperationRequest r)=>
        new(s,0,true,false,true,c,false,r.Amount, r.Kind,[],d,"content/shop/finance/ajax_create_operation.php");
}
public sealed record StorefrontCreateOperationRequest(decimal Amount, string? Kind, bool ConfirmWrites = false);
public sealed record StorefrontCreateOperationDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,decimal Amount, string? Kind,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{amount=Amount,kind=Kind},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
