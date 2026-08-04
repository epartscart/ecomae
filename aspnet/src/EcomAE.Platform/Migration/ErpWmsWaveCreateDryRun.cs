namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>wms_wave_create</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpWmsWaveCreateDryRun { ErpWmsWaveCreateDryRunResult Evaluate(ErpWmsWaveCreateRequest request); }
public sealed class ErpWmsWaveCreateDryRun : IErpWmsWaveCreateDryRun
{
    public ErpWmsWaveCreateDryRunResult Evaluate(ErpWmsWaveCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET wms_wave_create is not implemented; PHP ajax_erp.php remains authoritative.", request);
        var item = (request.Item ?? "").Trim();
        if (item.Length == 0 || request.Qty <= 0)
            return Refuse("dry-run-invalid","invalid_request","item and positive qty required for pick work.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,item,request.Qty,request.Reference,
            ["epc_wms_wave_create + epc_wms_wave_add_pick (NOT executed)"],
            "WMS wave create payload validated; INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_wave_create");
    }
    private static ErpWmsWaveCreateDryRunResult Refuse(string s,string c,string d,ErpWmsWaveCreateRequest r)=>
        new(s,0,true,false,true,c,false,r.Item,r.Qty,r.Reference,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=wms_wave_create");
}
public sealed record ErpWmsWaveCreateRequest(string? Item, decimal Qty, string? Reference=null, bool ConfirmWrites=false);
public sealed record ErpWmsWaveCreateDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Item,decimal Qty,string? Reference,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{item=Item,qty=Qty,reference=Reference},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
