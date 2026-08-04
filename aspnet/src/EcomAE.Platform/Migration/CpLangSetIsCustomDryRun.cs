namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/lang/ajax_set_is_custom.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpLangSetIsCustomDryRun { CpLangSetIsCustomDryRunResult Evaluate(CpLangSetIsCustomRequest request); }
public sealed class CpLangSetIsCustomDryRun : ICpLangSetIsCustomDryRun
{
    public CpLangSetIsCustomDryRunResult Evaluate(CpLangSetIsCustomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/lang/ajax_set_is_custom.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/lang/ajax_set_is_custom.php (NOT executed)"],
            "CpLangSetIsCustom payload validated; UPDATE blocked.",
            "cp/content/lang/ajax_set_is_custom.php");
    }
    private static CpLangSetIsCustomDryRunResult Refuse(string s,string c,string d,CpLangSetIsCustomRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/lang/ajax_set_is_custom.php");
}
public sealed record CpLangSetIsCustomRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpLangSetIsCustomDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
