namespace EcomAE.Platform.Migration;

/// <summary>Generic Wave B dry-run for any PHP bos/ajax_epc_bos.php action. Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxWriteRegistryDryRun { BosAjaxWriteRegistryDryRunResult Evaluate(BosAjaxWriteRegistryRequest request); }
public sealed class BosAjaxWriteRegistryDryRun : IBosAjaxWriteRegistryDryRun
{
    private readonly IBosAjaxWriteCatalog _catalog;
    public BosAjaxWriteRegistryDryRun(IBosAjaxWriteCatalog catalog) => _catalog = catalog;
    public BosAjaxWriteRegistryDryRunResult Evaluate(BosAjaxWriteRegistryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var action = (request.Action ?? "").Trim();
        if (string.IsNullOrWhiteSpace(action))
            return Refuse("dry-run-invalid","invalid_request","action is required.", action);
        if (!_catalog.TryGet(action, out var entry))
            return Refuse("dry-run-unknown-action","unknown_action",$"action '{action}' is not in ajax_epc_bos.php catalog.", action);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused",$"confirm_writes refused; PHP ajax_epc_bos.php remains authoritative for {action}.", action, entry);
        return new("dry-run-validated",0,true,false,true,"ok",true,action,entry.Coverage,entry.AspNetRouteHint,
            [$"ajax_epc_bos.php?action={action} (NOT executed)"],
            $"BOS ajax action '{action}' catalogued; UPDATE blocked.",
            $"/BOS/ajax_epc_bos.php?action={action}");
    }
    private static BosAjaxWriteRegistryDryRunResult Refuse(string s,string c,string d,string action, BosAjaxWriteCatalogEntry? entry=null)=>
        new(s,0,true,false,true,c,false,action,entry?.Coverage??"unknown",entry?.AspNetRouteHint??"/bos/ajax-writes/dry-run/{action}",[],d,
            string.IsNullOrEmpty(action)?"/BOS/ajax_epc_bos.php":$"/BOS/ajax_epc_bos.php?action={action}");
}
public sealed record BosAjaxWriteRegistryRequest(string Action, bool ConfirmWrites=false);
public sealed record BosAjaxWriteRegistryDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string Action,string Coverage,string AspNetRouteHint,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action,coverage=Coverage,aspNetRouteHint=AspNetRouteHint},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
