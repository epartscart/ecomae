namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>inv_create_warehouse</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInvCreateWarehouseDryRun { ErpInvCreateWarehouseDryRunResult Evaluate(ErpInvCreateWarehouseRequest request); }
public sealed class ErpInvCreateWarehouseDryRun : IErpInvCreateWarehouseDryRun
{
    public ErpInvCreateWarehouseDryRunResult Evaluate(ErpInvCreateWarehouseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET inv_create_warehouse is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=inv_create_warehouse (NOT executed)"],
            "ERP inv_create_warehouse payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=inv_create_warehouse");
    }
    private static ErpInvCreateWarehouseDryRunResult Refuse(string s,string c,string d,ErpInvCreateWarehouseRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=inv_create_warehouse");
}
public sealed record ErpInvCreateWarehouseRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpInvCreateWarehouseDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
