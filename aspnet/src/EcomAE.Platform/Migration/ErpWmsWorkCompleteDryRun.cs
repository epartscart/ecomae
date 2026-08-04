namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>wms_work_complete</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpWmsWorkCompleteDryRun { ErpWmsWorkCompleteDryRunResult Evaluate(ErpWmsWorkCompleteRequest request); }
public sealed class ErpWmsWorkCompleteDryRun : IErpWmsWorkCompleteDryRun
{
    public ErpWmsWorkCompleteDryRunResult Evaluate(ErpWmsWorkCompleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET wms_work_complete is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["epc_wms_work_complete(@id) (NOT executed)"],
            "WMS work complete payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_work_complete");
    }
    private static ErpWmsWorkCompleteDryRunResult Refuse(string s,string c,string d,ErpWmsWorkCompleteRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=wms_work_complete");
}
public sealed record ErpWmsWorkCompleteRequest(long Id, bool ConfirmWrites=false);
public sealed record ErpWmsWorkCompleteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
