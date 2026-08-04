namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/returns/ajax/ajax_return_action.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpReturnActionDryRun { CpReturnActionDryRunResult Evaluate(CpReturnActionRequest request); }
public sealed class CpReturnActionDryRun : ICpReturnActionDryRun
{
    public CpReturnActionDryRunResult Evaluate(CpReturnActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/returns/ajax/ajax_return_action.php remains authoritative.", request);
        if (request.ReturnId <= 0)
            return Refuse("dry-run-invalid","invalid_request","ReturnId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.ReturnId, request.Action,
            ["cp/content/shop/returns/ajax/ajax_return_action.php (NOT executed)"],
            "CpReturnAction payload validated; UPDATE blocked.",
            "cp/content/shop/returns/ajax/ajax_return_action.php");
    }
    private static CpReturnActionDryRunResult Refuse(string s,string c,string d,CpReturnActionRequest r)=>
        new(s,0,true,false,true,c,false,r.ReturnId, r.Action,[],d,"cp/content/shop/returns/ajax/ajax_return_action.php");
}
public sealed record CpReturnActionRequest(long ReturnId, string? Action, bool ConfirmWrites = false);
public sealed record CpReturnActionDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long ReturnId, string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{returnId=ReturnId,action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
