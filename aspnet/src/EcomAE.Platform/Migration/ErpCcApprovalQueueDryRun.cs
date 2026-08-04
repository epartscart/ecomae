namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cc_approval_queue</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCcApprovalQueueDryRun { ErpCcApprovalQueueDryRunResult Evaluate(ErpCcApprovalQueueRequest request); }
public sealed class ErpCcApprovalQueueDryRun : IErpCcApprovalQueueDryRun
{
    public ErpCcApprovalQueueDryRunResult Evaluate(ErpCcApprovalQueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET cc_approval_queue is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=cc_approval_queue (NOT executed)"],
            "ERP cc_approval_queue payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cc_approval_queue");
    }
    private static ErpCcApprovalQueueDryRunResult Refuse(string s,string c,string d,ErpCcApprovalQueueRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=cc_approval_queue");
}
public sealed record ErpCcApprovalQueueRequest(bool ConfirmWrites = false);
public sealed record ErpCcApprovalQueueDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="cc_approval_queue"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
