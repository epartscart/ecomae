namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>ajax_newsletter_subscribe.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontNewsletterSubscribeDryRun { StorefrontNewsletterSubscribeDryRunResult Evaluate(StorefrontNewsletterSubscribeRequest request); }
public sealed class StorefrontNewsletterSubscribeDryRun : IStorefrontNewsletterSubscribeDryRun
{
    public StorefrontNewsletterSubscribeDryRunResult Evaluate(StorefrontNewsletterSubscribeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_newsletter_subscribe.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.Email))
            return Refuse("dry-run-invalid","invalid_request","Email is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Email,
            ["ajax_newsletter_subscribe.php (NOT executed)"],
            "StorefrontNewsletterSubscribe payload validated; UPDATE blocked.",
            "ajax_newsletter_subscribe.php");
    }
    private static StorefrontNewsletterSubscribeDryRunResult Refuse(string s,string c,string d,StorefrontNewsletterSubscribeRequest r)=>
        new(s,0,true,false,true,c,false,r.Email,[],d,"ajax_newsletter_subscribe.php");
}
public sealed record StorefrontNewsletterSubscribeRequest(string? Email, bool ConfirmWrites = false);
public sealed record StorefrontNewsletterSubscribeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Email,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{email=Email},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
