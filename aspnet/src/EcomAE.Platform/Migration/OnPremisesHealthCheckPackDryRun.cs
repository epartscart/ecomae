namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>deploy/on-premises/health-check.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IOnPremisesHealthCheckPackDryRun { OnPremisesHealthCheckPackDryRunResult Evaluate(OnPremisesHealthCheckPackRequest request); }
public sealed class OnPremisesHealthCheckPackDryRun : IOnPremisesHealthCheckPackDryRun
{
    public OnPremisesHealthCheckPackDryRunResult Evaluate(OnPremisesHealthCheckPackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["deploy/on-premises/health-check.php (NOT executed)"],
            "OnPremisesHealthCheckPack payload validated; UPDATE blocked.",
            "deploy/on-premises/health-check.php");
    }
    private static OnPremisesHealthCheckPackDryRunResult Refuse(string s,string c,string d,OnPremisesHealthCheckPackRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"deploy/on-premises/health-check.php");
}
public sealed record OnPremisesHealthCheckPackRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record OnPremisesHealthCheckPackDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload()=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,note=Detail};
}
