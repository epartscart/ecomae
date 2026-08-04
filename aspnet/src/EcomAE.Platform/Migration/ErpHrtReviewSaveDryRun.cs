namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>hrt_review_save</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpHrtReviewSaveDryRun { ErpHrtReviewSaveDryRunResult Evaluate(ErpHrtReviewSaveRequest request); }
public sealed class ErpHrtReviewSaveDryRun : IErpHrtReviewSaveDryRun
{
    public ErpHrtReviewSaveDryRunResult Evaluate(ErpHrtReviewSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET hrt_review_save is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=hrt_review_save (NOT executed)"],
            "ERP hrt_review_save payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=hrt_review_save");
    }
    private static ErpHrtReviewSaveDryRunResult Refuse(string s,string c,string d,ErpHrtReviewSaveRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=hrt_review_save");
}
public sealed record ErpHrtReviewSaveRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpHrtReviewSaveDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
