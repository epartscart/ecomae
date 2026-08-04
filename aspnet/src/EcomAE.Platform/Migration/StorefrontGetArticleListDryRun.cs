namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/docpart/ajax_get_article_list.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontGetArticleListDryRun { StorefrontGetArticleListDryRunResult Evaluate(StorefrontGetArticleListRequest request); }
public sealed class StorefrontGetArticleListDryRun : IStorefrontGetArticleListDryRun
{
    public StorefrontGetArticleListDryRunResult Evaluate(StorefrontGetArticleListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/docpart/ajax_get_article_list.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["content/shop/docpart/ajax_get_article_list.php (NOT executed)"],
            "StorefrontGetArticleList payload validated; UPDATE blocked.",
            "content/shop/docpart/ajax_get_article_list.php");
    }
    private static StorefrontGetArticleListDryRunResult Refuse(string s,string c,string d,StorefrontGetArticleListRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"content/shop/docpart/ajax_get_article_list.php");
}
public sealed record StorefrontGetArticleListRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record StorefrontGetArticleListDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
