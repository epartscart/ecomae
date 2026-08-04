namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/users/ajax_set_user_option.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontSetUserOptionDryRun { StorefrontSetUserOptionDryRunResult Evaluate(StorefrontSetUserOptionRequest request); }
public sealed class StorefrontSetUserOptionDryRun : IStorefrontSetUserOptionDryRun
{
    public StorefrontSetUserOptionDryRunResult Evaluate(StorefrontSetUserOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/users/ajax_set_user_option.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.OptionKey))
            return Refuse("dry-run-invalid","invalid_request","OptionKey is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.OptionKey, request.OptionValue,
            ["content/users/ajax_set_user_option.php (NOT executed)"],
            "StorefrontSetUserOption payload validated; UPDATE blocked.",
            "content/users/ajax_set_user_option.php");
    }
    private static StorefrontSetUserOptionDryRunResult Refuse(string s,string c,string d,StorefrontSetUserOptionRequest r)=>
        new(s,0,true,false,true,c,false,r.OptionKey, r.OptionValue,[],d,"content/users/ajax_set_user_option.php");
}
public sealed record StorefrontSetUserOptionRequest(string? OptionKey, string? OptionValue, bool ConfirmWrites = false);
public sealed record StorefrontSetUserOptionDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? OptionKey, string? OptionValue,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{optionKey=OptionKey,optionValue=OptionValue},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
