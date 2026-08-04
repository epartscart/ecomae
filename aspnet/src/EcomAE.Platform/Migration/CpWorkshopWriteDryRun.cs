namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/workshop/ajax_workshop_endpoint.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpWorkshopWriteDryRun { CpWorkshopWriteDryRunResult Evaluate(CpWorkshopWriteRequest request); }
public sealed class CpWorkshopWriteDryRun : ICpWorkshopWriteDryRun
{
    public CpWorkshopWriteDryRunResult Evaluate(CpWorkshopWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/workshop/ajax_workshop_endpoint.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/workshop/ajax_workshop_endpoint.php (NOT executed)"],
            "CpWorkshopWrite payload validated; UPDATE blocked.",
            "cp/content/shop/workshop/ajax_workshop_endpoint.php");
    }
    private static CpWorkshopWriteDryRunResult Refuse(string s,string c,string d,CpWorkshopWriteRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/workshop/ajax_workshop_endpoint.php");
}
public sealed record CpWorkshopWriteRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpWorkshopWriteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
