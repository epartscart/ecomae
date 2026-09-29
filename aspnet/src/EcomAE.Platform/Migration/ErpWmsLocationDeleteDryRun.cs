namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET WMS location-delete writer.</summary>
public interface IErpWmsLocationDeleteDryRun { ErpWmsLocationDeleteDryRunResult Evaluate(ErpWmsLocationDeleteRequest request); }
public sealed class ErpWmsLocationDeleteDryRun : IErpWmsLocationDeleteDryRun
{
    public ErpWmsLocationDeleteDryRunResult Evaluate(ErpWmsLocationDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET WMS location-delete writer.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.Id,
            ["epc_wms_location_delete(@id) (NOT executed)"],
            "WMS location delete payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_delete");
    }
    private static ErpWmsLocationDeleteDryRunResult Refuse(string s,string c,string d,ErpWmsLocationDeleteRequest r)=>
        new(s,0,true,false,false,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_delete");
}
public sealed record ErpWmsLocationDeleteRequest(long Id, bool ConfirmWrites=false);
public sealed record ErpWmsLocationDeleteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
