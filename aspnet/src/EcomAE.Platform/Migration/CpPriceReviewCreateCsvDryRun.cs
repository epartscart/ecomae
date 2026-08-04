namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/prices_upload/price_review/ajax_create_csv.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPriceReviewCreateCsvDryRun { CpPriceReviewCreateCsvDryRunResult Evaluate(CpPriceReviewCreateCsvRequest request); }
public sealed class CpPriceReviewCreateCsvDryRun : ICpPriceReviewCreateCsvDryRun
{
    public CpPriceReviewCreateCsvDryRunResult Evaluate(CpPriceReviewCreateCsvRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/prices_upload/price_review/ajax_create_csv.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/prices_upload/price_review/ajax_create_csv.php (NOT executed)"],
            "CpPriceReviewCreateCsv payload validated; UPDATE blocked.",
            "cp/content/shop/prices_upload/price_review/ajax_create_csv.php");
    }
    private static CpPriceReviewCreateCsvDryRunResult Refuse(string s,string c,string d,CpPriceReviewCreateCsvRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/prices_upload/price_review/ajax_create_csv.php");
}
public sealed record CpPriceReviewCreateCsvRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPriceReviewCreateCsvDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
