namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>content/shop/catalogue/evaluations/ajax_add_evaluation.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface IStorefrontAddEvaluationDryRun { StorefrontAddEvaluationDryRunResult Evaluate(StorefrontAddEvaluationRequest request); }
public sealed class StorefrontAddEvaluationDryRun : IStorefrontAddEvaluationDryRun
{
    public StorefrontAddEvaluationDryRunResult Evaluate(StorefrontAddEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP content/shop/catalogue/evaluations/ajax_add_evaluation.php remains authoritative.", request);
        if (request.ProductId <= 0)
            return Refuse("dry-run-invalid","invalid_request","ProductId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.ProductId, request.Rating,
            ["content/shop/catalogue/evaluations/ajax_add_evaluation.php (NOT executed)"],
            "StorefrontAddEvaluation payload validated; UPDATE blocked.",
            "content/shop/catalogue/evaluations/ajax_add_evaluation.php");
    }
    private static StorefrontAddEvaluationDryRunResult Refuse(string s,string c,string d,StorefrontAddEvaluationRequest r)=>
        new(s,0,true,false,true,c,false,r.ProductId, r.Rating,[],d,"content/shop/catalogue/evaluations/ajax_add_evaluation.php");
}
public sealed record StorefrontAddEvaluationRequest(long ProductId, int Rating = 5, bool ConfirmWrites = false);
public sealed record StorefrontAddEvaluationDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long ProductId, int Rating,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="storefront",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{productId=ProductId,rating=Rating},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
