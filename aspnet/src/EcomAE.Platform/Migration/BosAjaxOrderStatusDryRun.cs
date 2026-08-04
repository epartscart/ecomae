namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>order_status</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxOrderStatusDryRun { BosAjaxOrderStatusDryRunResult Evaluate(BosAjaxOrderStatusRequest request); }
public sealed class BosAjaxOrderStatusDryRun : IBosAjaxOrderStatusDryRun
{
    public BosAjaxOrderStatusDryRunResult Evaluate(BosAjaxOrderStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_epc_bos.php?action=order_status (NOT executed)"],
            "BOS order_status payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=order_status");
    }
    private static BosAjaxOrderStatusDryRunResult Refuse(string s,string c,string d,BosAjaxOrderStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/BOS/ajax_epc_bos.php?action=order_status");
}
public sealed record BosAjaxOrderStatusRequest(long Id, string? TargetStatus=null, bool ConfirmWrites=false);
public sealed record BosAjaxOrderStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
