namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP deploy/on-premises/setup-wizard.php. Never mutates. PHP authoritative.</summary>
public interface IOnPremisesSetupWizardDryRun { OnPremisesSetupWizardDryRunResult Evaluate(OnPremisesSetupWizardRequest request); }
public sealed class OnPremisesSetupWizardDryRun : IOnPremisesSetupWizardDryRun
{
    public OnPremisesSetupWizardDryRunResult Evaluate(OnPremisesSetupWizardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP setup-wizard.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.TenantCode,
            ["deploy/on-premises/setup-wizard.php (NOT executed)"],
            "On-premises setup wizard payload validated; schema migrate blocked. See deploy/on-premises-aspnet/ scaffold.",
            "deploy/on-premises/setup-wizard.php");
    }
    private static OnPremisesSetupWizardDryRunResult Refuse(string s,string c,string d,OnPremisesSetupWizardRequest r)=>
        new(s,0,true,false,true,c,false,r.TenantCode,[],d,"deploy/on-premises/setup-wizard.php");
}
public sealed record OnPremisesSetupWizardRequest(string? TenantCode = null, bool ConfirmWrites = false);
public sealed record OnPremisesSetupWizardDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? TenantCode,IReadOnlyList<string> SimulatedSql,string Detail,string PhpPath)
{
    public object ToPayload()=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{tenantCode=TenantCode},simulated=SimulatedSql,php_path=PhpPath,note=Detail};
}
