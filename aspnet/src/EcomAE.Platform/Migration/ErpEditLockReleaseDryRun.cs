namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>edit_lock_release</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpEditLockReleaseDryRun { ErpEditLockReleaseDryRunResult Evaluate(ErpEditLockReleaseRequest request); }
public sealed class ErpEditLockReleaseDryRun : IErpEditLockReleaseDryRun
{
    public ErpEditLockReleaseDryRunResult Evaluate(ErpEditLockReleaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET edit_lock_release is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.ResourceKey))
            return Refuse("dry-run-invalid","invalid_request","resourceKey is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.ResourceKey,
            ["ajax_erp.php?action=edit_lock_release resource=@resourceKey (NOT executed)"],
            "ERP edit_lock_release payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=edit_lock_release");
    }
    private static ErpEditLockReleaseDryRunResult Refuse(string s,string c,string d,ErpEditLockReleaseRequest r)=>
        new(s,0,true,false,true,c,false,r.ResourceKey,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=edit_lock_release");
}
public sealed record ErpEditLockReleaseRequest(string? ResourceKey = null, bool ConfirmWrites = false);
public sealed record ErpEditLockReleaseDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? ResourceKey,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{resourceKey=ResourceKey},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
