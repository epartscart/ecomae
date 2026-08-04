namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/requests/ajax_set_users_vin_viewed.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpSetUsersVinViewedDryRun { CpSetUsersVinViewedDryRunResult Evaluate(CpSetUsersVinViewedRequest request); }
public sealed class CpSetUsersVinViewedDryRun : ICpSetUsersVinViewedDryRun
{
    public CpSetUsersVinViewedDryRunResult Evaluate(CpSetUsersVinViewedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/requests/ajax_set_users_vin_viewed.php remains authoritative.", request);
        if (request.RequestId <= 0)
            return Refuse("dry-run-invalid","invalid_request","RequestId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.RequestId,
            ["cp/content/requests/ajax_set_users_vin_viewed.php (NOT executed)"],
            "CpSetUsersVinViewed payload validated; UPDATE blocked.",
            "cp/content/requests/ajax_set_users_vin_viewed.php");
    }
    private static CpSetUsersVinViewedDryRunResult Refuse(string s,string c,string d,CpSetUsersVinViewedRequest r)=>
        new(s,0,true,false,true,c,false,r.RequestId,[],d,"cp/content/requests/ajax_set_users_vin_viewed.php");
}
public sealed record CpSetUsersVinViewedRequest(long RequestId, bool ConfirmWrites = false);
public sealed record CpSetUsersVinViewedDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long RequestId,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{requestId=RequestId},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
