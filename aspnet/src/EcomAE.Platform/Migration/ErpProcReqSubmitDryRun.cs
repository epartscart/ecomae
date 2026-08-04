namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>proc_req_submit</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpProcReqSubmitDryRun { ErpProcReqSubmitDryRunResult Evaluate(ErpProcReqSubmitRequest request); }
public sealed class ErpProcReqSubmitDryRun : IErpProcReqSubmitDryRun
{
    public ErpProcReqSubmitDryRunResult Evaluate(ErpProcReqSubmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET proc_req_submit is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["epc_proc_req_submit(@id) policy auto-approve path (NOT executed)"],
            "Procurement requisition submit payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=proc_req_submit");
    }
    private static ErpProcReqSubmitDryRunResult Refuse(string s,string c,string d,ErpProcReqSubmitRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=proc_req_submit");
}
public sealed record ErpProcReqSubmitRequest(long Id, bool ConfirmWrites=false);
public sealed record ErpProcReqSubmitDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
