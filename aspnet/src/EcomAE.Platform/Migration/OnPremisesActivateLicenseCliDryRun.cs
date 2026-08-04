namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>deploy/on-premises/activate-license.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IOnPremisesActivateLicenseCliDryRun { OnPremisesActivateLicenseCliDryRunResult Evaluate(OnPremisesActivateLicenseCliRequest request); }
public sealed class OnPremisesActivateLicenseCliDryRun : IOnPremisesActivateLicenseCliDryRun
{
    public OnPremisesActivateLicenseCliDryRunResult Evaluate(OnPremisesActivateLicenseCliRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["deploy/on-premises/activate-license.php (NOT executed)"],
            "OnPremisesActivateLicenseCli payload validated; UPDATE blocked.",
            "deploy/on-premises/activate-license.php");
    }
    private static OnPremisesActivateLicenseCliDryRunResult Refuse(string s,string c,string d,OnPremisesActivateLicenseCliRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"deploy/on-premises/activate-license.php");
}
public sealed record OnPremisesActivateLicenseCliRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record OnPremisesActivateLicenseCliDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload()=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,note=Detail};
}
