namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/packs_control/ajax_delete_pack.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPacksDeleteDryRun { CpPacksDeleteDryRunResult Evaluate(CpPacksDeleteRequest request); }
public sealed class CpPacksDeleteDryRun : ICpPacksDeleteDryRun
{
    public CpPacksDeleteDryRunResult Evaluate(CpPacksDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/packs_control/ajax_delete_pack.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/packs_control/ajax_delete_pack.php (NOT executed)"],
            "CpPacksDelete payload validated; UPDATE blocked.",
            "cp/content/packs_control/ajax_delete_pack.php");
    }
    private static CpPacksDeleteDryRunResult Refuse(string s,string c,string d,CpPacksDeleteRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/packs_control/ajax_delete_pack.php");
}
public sealed record CpPacksDeleteRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPacksDeleteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
