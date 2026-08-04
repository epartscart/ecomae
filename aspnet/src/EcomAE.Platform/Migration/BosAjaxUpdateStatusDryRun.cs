namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>update_status</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxUpdateStatusDryRun { BosAjaxUpdateStatusDryRunResult Evaluate(BosAjaxUpdateStatusRequest request); }
public sealed class BosAjaxUpdateStatusDryRun : IBosAjaxUpdateStatusDryRun
{
    public BosAjaxUpdateStatusDryRunResult Evaluate(BosAjaxUpdateStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_epc_bos.php?action=update_status (NOT executed)"],
            "BOS update_status payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=update_status");
    }
    private static BosAjaxUpdateStatusDryRunResult Refuse(string s,string c,string d,BosAjaxUpdateStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/BOS/ajax_epc_bos.php?action=update_status");
}
public sealed record BosAjaxUpdateStatusRequest(long Id, string? TargetStatus=null, bool ConfirmWrites=false);
public sealed record BosAjaxUpdateStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
