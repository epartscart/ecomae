namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>workflow_run</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpWorkflowRunDryRun { ErpWorkflowRunDryRunResult Evaluate(ErpWorkflowRunRequest request); }
public sealed class ErpWorkflowRunDryRun : IErpWorkflowRunDryRun
{
    public ErpWorkflowRunDryRunResult Evaluate(ErpWorkflowRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET workflow_run is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=workflow_run (NOT executed)"],
            "ERP workflow_run payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_run");
    }
    private static ErpWorkflowRunDryRunResult Refuse(string s,string c,string d,ErpWorkflowRunRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_run");
}
public sealed record ErpWorkflowRunRequest(bool ConfirmWrites = false);
public sealed record ErpWorkflowRunDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="workflow_run"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
