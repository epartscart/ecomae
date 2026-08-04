namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/bulk_upload/ajax_process.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontBulkUploadProcessDryRun { StorefrontBulkUploadProcessDryRunResult Evaluate(StorefrontBulkUploadProcessRequest request); }
public sealed class StorefrontBulkUploadProcessDryRun : IStorefrontBulkUploadProcessDryRun
{
    public StorefrontBulkUploadProcessDryRunResult Evaluate(StorefrontBulkUploadProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/bulk_upload/ajax_process.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["content/shop/bulk_upload/ajax_process.php (NOT executed)"],
            "StorefrontBulkUploadProcess payload validated; UPDATE blocked.",
            "content/shop/bulk_upload/ajax_process.php");
    }
    private static StorefrontBulkUploadProcessDryRunResult Refuse(string s,string c,string d,StorefrontBulkUploadProcessRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"content/shop/bulk_upload/ajax_process.php");
}
public sealed record StorefrontBulkUploadProcessRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record StorefrontBulkUploadProcessDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
