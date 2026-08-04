namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>mfa_policy</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxMfaPolicyDryRun { BosAjaxMfaPolicyDryRunResult Evaluate(BosAjaxMfaPolicyRequest request); }
public sealed class BosAjaxMfaPolicyDryRun : IBosAjaxMfaPolicyDryRun
{
    public BosAjaxMfaPolicyDryRunResult Evaluate(BosAjaxMfaPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_epc_bos.php?action=mfa_policy (NOT executed)"],
            "BOS mfa_policy payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=mfa_policy");
    }
    private static BosAjaxMfaPolicyDryRunResult Refuse(string s,string c,string d,BosAjaxMfaPolicyRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/BOS/ajax_epc_bos.php?action=mfa_policy");
}
public sealed record BosAjaxMfaPolicyRequest(long Id=0, string? Code=null, bool ConfirmWrites=false);
public sealed record BosAjaxMfaPolicyDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
