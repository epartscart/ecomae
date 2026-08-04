namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/pos/ajax_pos.php?action=save_settings</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPosSaveSettingsDryRun { CpPosSaveSettingsDryRunResult Evaluate(CpPosSaveSettingsRequest request); }
public sealed class CpPosSaveSettingsDryRun : ICpPosSaveSettingsDryRun
{
    public CpPosSaveSettingsDryRunResult Evaluate(CpPosSaveSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/pos/ajax_pos.php?action=save_settings (NOT executed)"],
            "CpPosSaveSettings payload validated; UPDATE blocked.",
            "cp/content/shop/pos/ajax_pos.php?action=save_settings");
    }
    private static CpPosSaveSettingsDryRunResult Refuse(string s,string c,string d,CpPosSaveSettingsRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/pos/ajax_pos.php?action=save_settings");
}
public sealed record CpPosSaveSettingsRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPosSaveSettingsDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
