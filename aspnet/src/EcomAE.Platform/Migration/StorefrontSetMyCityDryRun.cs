namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>modules/shop/geo/ajax_set_my_city.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontSetMyCityDryRun { StorefrontSetMyCityDryRunResult Evaluate(StorefrontSetMyCityRequest request); }
public sealed class StorefrontSetMyCityDryRun : IStorefrontSetMyCityDryRun
{
    public StorefrontSetMyCityDryRunResult Evaluate(StorefrontSetMyCityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP modules/shop/geo/ajax_set_my_city.php remains authoritative.", request);
        if (request.CityId <= 0)
            return Refuse("dry-run-invalid","invalid_request","CityId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.CityId,
            ["modules/shop/geo/ajax_set_my_city.php (NOT executed)"],
            "StorefrontSetMyCity payload validated; UPDATE blocked.",
            "modules/shop/geo/ajax_set_my_city.php");
    }
    private static StorefrontSetMyCityDryRunResult Refuse(string s,string c,string d,StorefrontSetMyCityRequest r)=>
        new(s,0,true,false,true,c,false,r.CityId,[],d,"modules/shop/geo/ajax_set_my_city.php");
}
public sealed record StorefrontSetMyCityRequest(long CityId, bool ConfirmWrites = false);
public sealed record StorefrontSetMyCityDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long CityId,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{cityId=CityId},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
