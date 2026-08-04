namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>modules/login/code/frontAjax/ajax_sendCode.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontLoginSendCodeDryRun { StorefrontLoginSendCodeDryRunResult Evaluate(StorefrontLoginSendCodeRequest request); }
public sealed class StorefrontLoginSendCodeDryRun : IStorefrontLoginSendCodeDryRun
{
    public StorefrontLoginSendCodeDryRunResult Evaluate(StorefrontLoginSendCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP modules/login/code/frontAjax/ajax_sendCode.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.Phone))
            return Refuse("dry-run-invalid","invalid_request","Phone is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Phone,
            ["modules/login/code/frontAjax/ajax_sendCode.php (NOT executed)"],
            "StorefrontLoginSendCode payload validated; UPDATE blocked.",
            "modules/login/code/frontAjax/ajax_sendCode.php");
    }
    private static StorefrontLoginSendCodeDryRunResult Refuse(string s,string c,string d,StorefrontLoginSendCodeRequest r)=>
        new(s,0,true,false,true,c,false,r.Phone,[],d,"modules/login/code/frontAjax/ajax_sendCode.php");
}
public sealed record StorefrontLoginSendCodeRequest(string? Phone, bool ConfirmWrites = false);
public sealed record StorefrontLoginSendCodeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Phone,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{phone=Phone},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
