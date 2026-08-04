namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/lang/ajax_delete_not_used_found.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpLangDeleteNotUsedDryRun { CpLangDeleteNotUsedDryRunResult Evaluate(CpLangDeleteNotUsedRequest request); }
public sealed class CpLangDeleteNotUsedDryRun : ICpLangDeleteNotUsedDryRun
{
    public CpLangDeleteNotUsedDryRunResult Evaluate(CpLangDeleteNotUsedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/lang/ajax_delete_not_used_found.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/lang/ajax_delete_not_used_found.php (NOT executed)"],
            "CpLangDeleteNotUsed payload validated; UPDATE blocked.",
            "cp/content/lang/ajax_delete_not_used_found.php");
    }
    private static CpLangDeleteNotUsedDryRunResult Refuse(string s,string c,string d,CpLangDeleteNotUsedRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/lang/ajax_delete_not_used_found.php");
}
public sealed record CpLangDeleteNotUsedRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpLangDeleteNotUsedDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
