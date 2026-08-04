namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP deploy/on-premises/backup.php. Never mutates. PHP authoritative.</summary>
public interface IOnPremisesBackupDryRun { OnPremisesBackupDryRunResult Evaluate(OnPremisesBackupRequest request); }
public sealed class OnPremisesBackupDryRun : IOnPremisesBackupDryRun
{
    public OnPremisesBackupDryRunResult Evaluate(OnPremisesBackupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP backup.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Label,
            ["deploy/on-premises/backup.php (NOT executed)"],
            "On-premises backup payload validated; archive write blocked. See deploy/on-premises-aspnet/ scaffold.",
            "deploy/on-premises/backup.php");
    }
    private static OnPremisesBackupDryRunResult Refuse(string s,string c,string d,OnPremisesBackupRequest r)=>
        new(s,0,true,false,true,c,false,r.Label,[],d,"deploy/on-premises/backup.php");
}
public sealed record OnPremisesBackupRequest(string? Label = null, bool ConfirmWrites = false);
public sealed record OnPremisesBackupDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Label,IReadOnlyList<string> SimulatedSql,string Detail,string PhpPath)
{
    public object ToPayload()=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{label=Label},simulated=SimulatedSql,php_path=PhpPath,note=Detail};
}
