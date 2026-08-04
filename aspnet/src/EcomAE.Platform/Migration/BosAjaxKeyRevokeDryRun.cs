namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>key_revoke</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxKeyRevokeDryRun { BosAjaxKeyRevokeDryRunResult Evaluate(BosAjaxKeyRevokeRequest request); }
public sealed class BosAjaxKeyRevokeDryRun : IBosAjaxKeyRevokeDryRun
{
    public BosAjaxKeyRevokeDryRunResult Evaluate(BosAjaxKeyRevokeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_epc_bos.php?action=key_revoke (NOT executed)"],
            "BOS key_revoke payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=key_revoke");
    }
    private static BosAjaxKeyRevokeDryRunResult Refuse(string s,string c,string d,BosAjaxKeyRevokeRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/BOS/ajax_epc_bos.php?action=key_revoke");
}
public sealed record BosAjaxKeyRevokeRequest(long Id=0, bool ConfirmWrites=false);
public sealed record BosAjaxKeyRevokeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
