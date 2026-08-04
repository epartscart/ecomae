namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>modules/login/code/frontAjax/ajax_checkCode.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontLoginCheckCodeDryRun { StorefrontLoginCheckCodeDryRunResult Evaluate(StorefrontLoginCheckCodeRequest request); }
public sealed class StorefrontLoginCheckCodeDryRun : IStorefrontLoginCheckCodeDryRun
{
    public StorefrontLoginCheckCodeDryRunResult Evaluate(StorefrontLoginCheckCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP modules/login/code/frontAjax/ajax_checkCode.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.Code))
            return Refuse("dry-run-invalid","invalid_request","Code is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Code,
            ["modules/login/code/frontAjax/ajax_checkCode.php (NOT executed)"],
            "StorefrontLoginCheckCode payload validated; UPDATE blocked.",
            "modules/login/code/frontAjax/ajax_checkCode.php");
    }
    private static StorefrontLoginCheckCodeDryRunResult Refuse(string s,string c,string d,StorefrontLoginCheckCodeRequest r)=>
        new(s,0,true,false,true,c,false,r.Code,[],d,"modules/login/code/frontAjax/ajax_checkCode.php");
}
public sealed record StorefrontLoginCheckCodeRequest(string? Code, bool ConfirmWrites = false);
public sealed record StorefrontLoginCheckCodeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
