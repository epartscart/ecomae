namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>inv_sync_warehouses</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInvSyncWarehousesDryRun { ErpInvSyncWarehousesDryRunResult Evaluate(ErpInvSyncWarehousesRequest request); }
public sealed class ErpInvSyncWarehousesDryRun : IErpInvSyncWarehousesDryRun
{
    public ErpInvSyncWarehousesDryRunResult Evaluate(ErpInvSyncWarehousesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET inv_sync_warehouses is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=inv_sync_warehouses (NOT executed)"],
            "ERP inv_sync_warehouses payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=inv_sync_warehouses");
    }
    private static ErpInvSyncWarehousesDryRunResult Refuse(string s,string c,string d,ErpInvSyncWarehousesRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=inv_sync_warehouses");
}
public sealed record ErpInvSyncWarehousesRequest(bool ConfirmWrites = false);
public sealed record ErpInvSyncWarehousesDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="inv_sync_warehouses"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
