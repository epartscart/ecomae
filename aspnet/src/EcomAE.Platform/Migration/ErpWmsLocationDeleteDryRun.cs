namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>wms_location_delete</c>. Never DELETE. PHP authoritative.</summary>
public interface IErpWmsLocationDeleteDryRun { ErpWmsLocationDeleteDryRunResult Evaluate(ErpWmsLocationDeleteRequest request); }
public sealed class ErpWmsLocationDeleteDryRun : IErpWmsLocationDeleteDryRun
{
    public ErpWmsLocationDeleteDryRunResult Evaluate(ErpWmsLocationDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET wms_location_delete is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["epc_wms_location_delete(@id) (NOT executed)"],
            "WMS location delete payload validated; DELETE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_delete");
    }
    private static ErpWmsLocationDeleteDryRunResult Refuse(string s,string c,string d,ErpWmsLocationDeleteRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_delete");
}
public sealed record ErpWmsLocationDeleteRequest(long Id, bool ConfirmWrites=false);
public sealed record ErpWmsLocationDeleteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
