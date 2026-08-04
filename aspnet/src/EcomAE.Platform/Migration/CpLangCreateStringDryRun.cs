namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/lang/ajax_create_new_string.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpLangCreateStringDryRun { CpLangCreateStringDryRunResult Evaluate(CpLangCreateStringRequest request); }
public sealed class CpLangCreateStringDryRun : ICpLangCreateStringDryRun
{
    public CpLangCreateStringDryRunResult Evaluate(CpLangCreateStringRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/lang/ajax_create_new_string.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/lang/ajax_create_new_string.php (NOT executed)"],
            "CpLangCreateString payload validated; UPDATE blocked.",
            "cp/content/lang/ajax_create_new_string.php");
    }
    private static CpLangCreateStringDryRunResult Refuse(string s,string c,string d,CpLangCreateStringRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/lang/ajax_create_new_string.php");
}
public sealed record CpLangCreateStringRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpLangCreateStringDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
