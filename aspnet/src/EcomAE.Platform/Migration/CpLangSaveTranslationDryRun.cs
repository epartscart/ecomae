namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/lang/ajax_save_string_translation.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpLangSaveTranslationDryRun { CpLangSaveTranslationDryRunResult Evaluate(CpLangSaveTranslationRequest request); }
public sealed class CpLangSaveTranslationDryRun : ICpLangSaveTranslationDryRun
{
    public CpLangSaveTranslationDryRunResult Evaluate(CpLangSaveTranslationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/lang/ajax_save_string_translation.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/lang/ajax_save_string_translation.php (NOT executed)"],
            "CpLangSaveTranslation payload validated; UPDATE blocked.",
            "cp/content/lang/ajax_save_string_translation.php");
    }
    private static CpLangSaveTranslationDryRunResult Refuse(string s,string c,string d,CpLangSaveTranslationRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/lang/ajax_save_string_translation.php");
}
public sealed record CpLangSaveTranslationRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpLangSaveTranslationDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
