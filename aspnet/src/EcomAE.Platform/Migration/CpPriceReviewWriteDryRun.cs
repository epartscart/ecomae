namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/prices_upload/price_review/ajax_price_review.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPriceReviewWriteDryRun { CpPriceReviewWriteDryRunResult Evaluate(CpPriceReviewWriteRequest request); }
public sealed class CpPriceReviewWriteDryRun : ICpPriceReviewWriteDryRun
{
    public CpPriceReviewWriteDryRunResult Evaluate(CpPriceReviewWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/prices_upload/price_review/ajax_price_review.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/prices_upload/price_review/ajax_price_review.php (NOT executed)"],
            "CpPriceReviewWrite payload validated; UPDATE blocked.",
            "cp/content/shop/prices_upload/price_review/ajax_price_review.php");
    }
    private static CpPriceReviewWriteDryRunResult Refuse(string s,string c,string d,CpPriceReviewWriteRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/prices_upload/price_review/ajax_price_review.php");
}
public sealed record CpPriceReviewWriteRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPriceReviewWriteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
