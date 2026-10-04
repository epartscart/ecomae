namespace EcomAE.Platform.Migration;

/// <summary>Dry-run envelope for PHP <c>presence_heartbeat</c> when <c>confirmWrites</c> is omitted; live upsert is <c>IErpPresenceWriteService</c>.</summary>
public interface IErpPresenceHeartbeatDryRun { ErpPresenceHeartbeatDryRunResult Evaluate(ErpPresenceHeartbeatRequest request); }
public sealed class ErpPresenceHeartbeatDryRun : IErpPresenceHeartbeatDryRun
{
    public ErpPresenceHeartbeatDryRunResult Evaluate(ErpPresenceHeartbeatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused on the dry-run path; POST confirmWrites=true to run on ASP.NET.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.ResourceKey,
            ["ajax_erp.php?action=presence_heartbeat resource=@resourceKey (NOT executed)"],
            "ERP presence_heartbeat payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=presence_heartbeat");
    }
    private static ErpPresenceHeartbeatDryRunResult Refuse(string s,string c,string d,ErpPresenceHeartbeatRequest r)=>
        new(s,0,true,false,false,c,false,r.ResourceKey,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=presence_heartbeat");
}
public sealed record ErpPresenceHeartbeatRequest(string? ResourceKey = null, bool ConfirmWrites = false);
public sealed record ErpPresenceHeartbeatDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? ResourceKey,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{resourceKey=ResourceKey},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
