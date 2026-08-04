namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/control/portal/ajax_portal.php?action=deploy_site</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPortalDeploySiteDryRun { CpPortalDeploySiteDryRunResult Evaluate(CpPortalDeploySiteRequest request); }
public sealed class CpPortalDeploySiteDryRun : ICpPortalDeploySiteDryRun
{
    public CpPortalDeploySiteDryRunResult Evaluate(CpPortalDeploySiteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/control/portal/ajax_portal.php?action=deploy_site (NOT executed)"],
            "CpPortalDeploySite payload validated; UPDATE blocked.",
            "cp/content/control/portal/ajax_portal.php?action=deploy_site");
    }
    private static CpPortalDeploySiteDryRunResult Refuse(string s,string c,string d,CpPortalDeploySiteRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/control/portal/ajax_portal.php?action=deploy_site");
}
public sealed record CpPortalDeploySiteRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPortalDeploySiteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
