namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/control/version_control/ajax/ajax_clear_updates_dir.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpVersionClearUpdatesDryRun { CpVersionClearUpdatesDryRunResult Evaluate(CpVersionClearUpdatesRequest request); }
public sealed class CpVersionClearUpdatesDryRun : ICpVersionClearUpdatesDryRun
{
    public CpVersionClearUpdatesDryRunResult Evaluate(CpVersionClearUpdatesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/control/version_control/ajax/ajax_clear_updates_dir.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/control/version_control/ajax/ajax_clear_updates_dir.php (NOT executed)"],
            "CpVersionClearUpdates payload validated; UPDATE blocked.",
            "cp/content/control/version_control/ajax/ajax_clear_updates_dir.php");
    }
    private static CpVersionClearUpdatesDryRunResult Refuse(string s,string c,string d,CpVersionClearUpdatesRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/control/version_control/ajax/ajax_clear_updates_dir.php");
}
public sealed record CpVersionClearUpdatesRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpVersionClearUpdatesDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
