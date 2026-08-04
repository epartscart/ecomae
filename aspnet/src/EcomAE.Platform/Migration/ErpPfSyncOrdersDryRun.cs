namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>pf_sync_orders</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPfSyncOrdersDryRun { ErpPfSyncOrdersDryRunResult Evaluate(ErpPfSyncOrdersRequest request); }
public sealed class ErpPfSyncOrdersDryRun : IErpPfSyncOrdersDryRun
{
    public ErpPfSyncOrdersDryRunResult Evaluate(ErpPfSyncOrdersRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET pf_sync_orders is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=pf_sync_orders (NOT executed)"],
            "ERP pf_sync_orders payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pf_sync_orders");
    }
    private static ErpPfSyncOrdersDryRunResult Refuse(string s,string c,string d,ErpPfSyncOrdersRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=pf_sync_orders");
}
public sealed record ErpPfSyncOrdersRequest(bool ConfirmWrites = false);
public sealed record ErpPfSyncOrdersDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="pf_sync_orders"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
