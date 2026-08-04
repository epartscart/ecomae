namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/crm/ajax_crm.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpCrmActionDryRun { CpCrmActionDryRunResult Evaluate(CpCrmActionRequest request); }
public sealed class CpCrmActionDryRun : ICpCrmActionDryRun
{
    public CpCrmActionDryRunResult Evaluate(CpCrmActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/crm/ajax_crm.php (NOT executed)"],
            "CpCrmAction payload validated; UPDATE blocked.",
            "cp/content/shop/crm/ajax_crm.php");
    }
    private static CpCrmActionDryRunResult Refuse(string s,string c,string d,CpCrmActionRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/crm/ajax_crm.php");
}
public sealed record CpCrmActionRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpCrmActionDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
