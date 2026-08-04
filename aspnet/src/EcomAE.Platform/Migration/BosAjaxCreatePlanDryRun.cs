namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>create_plan</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxCreatePlanDryRun { BosAjaxCreatePlanDryRunResult Evaluate(BosAjaxCreatePlanRequest request); }
public sealed class BosAjaxCreatePlanDryRun : IBosAjaxCreatePlanDryRun
{
    public BosAjaxCreatePlanDryRunResult Evaluate(BosAjaxCreatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_epc_bos.php?action=create_plan (NOT executed)"],
            "BOS create_plan payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=create_plan");
    }
    private static BosAjaxCreatePlanDryRunResult Refuse(string s,string c,string d,BosAjaxCreatePlanRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/BOS/ajax_epc_bos.php?action=create_plan");
}
public sealed record BosAjaxCreatePlanRequest(long Id=0, string? Code=null, bool ConfirmWrites=false);
public sealed record BosAjaxCreatePlanDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
