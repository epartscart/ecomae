namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET WMS wave-release writer.</summary>
public interface IErpWmsWaveReleaseDryRun { ErpWmsWaveReleaseDryRunResult Evaluate(ErpWmsWaveReleaseRequest request); }
public sealed class ErpWmsWaveReleaseDryRun : IErpWmsWaveReleaseDryRun
{
    public ErpWmsWaveReleaseDryRunResult Evaluate(ErpWmsWaveReleaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET WMS wave-release writer.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.Id,
            ["epc_wms_wave_release(@id) (NOT executed)"],
            "WMS wave release payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_wave_release");
    }
    private static ErpWmsWaveReleaseDryRunResult Refuse(string s,string c,string d,ErpWmsWaveReleaseRequest r)=>
        new(s,0,true,false,false,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=wms_wave_release");
}
public sealed record ErpWmsWaveReleaseRequest(long Id, bool ConfirmWrites=false);
public sealed record ErpWmsWaveReleaseDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
