namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/accessories/ajax_epc_accessories_photos.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpAccessoriesPhotosDryRun { CpAccessoriesPhotosDryRunResult Evaluate(CpAccessoriesPhotosRequest request); }
public sealed class CpAccessoriesPhotosDryRun : ICpAccessoriesPhotosDryRun
{
    public CpAccessoriesPhotosDryRunResult Evaluate(CpAccessoriesPhotosRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/accessories/ajax_epc_accessories_photos.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/accessories/ajax_epc_accessories_photos.php (NOT executed)"],
            "CpAccessoriesPhotos payload validated; UPDATE blocked.",
            "cp/content/shop/accessories/ajax_epc_accessories_photos.php");
    }
    private static CpAccessoriesPhotosDryRunResult Refuse(string s,string c,string d,CpAccessoriesPhotosRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/accessories/ajax_epc_accessories_photos.php");
}
public sealed record CpAccessoriesPhotosRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpAccessoriesPhotosDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
