namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>prja_recognize</c>. Zero-write validation; confirm_writes=true is served by the live write service in the route handler.</summary>
public interface IErpPrjaRecognizeDryRun { ErpPrjaRecognizeDryRunResult Evaluate(ErpPrjaRecognizeRequest request); }
public sealed class ErpPrjaRecognizeDryRun : IErpPrjaRecognizeDryRun
{
    public ErpPrjaRecognizeDryRunResult Evaluate(ErpPrjaRecognizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes is handled by the live ASP.NET prja_recognize write service in the route handler; this dry-run evaluator never writes.", request);
        if (request.ProjectId <= 0)
            return Refuse("dry-run-invalid","project_required","project_id must be greater than zero.", request);
        if (request.Method is not null && request.Method is not ("poc" or "completed" or "straight_line"))
            return Refuse("dry-run-invalid","invalid_method","method must be poc, completed, or straight_line.", request);
        if (request.Fraction is < 0 or > 1)
            return Refuse("dry-run-invalid","invalid_fraction","fraction must be between 0 and 1.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.ProjectId, request.Method,
            ["ajax_erp.php?action=prja_recognize (NOT executed)"],
            "ERP prja_recognize payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=prja_recognize");
    }
    private static ErpPrjaRecognizeDryRunResult Refuse(string s,string c,string d,ErpPrjaRecognizeRequest r)=>
        new(s,0,true,false,true,c,false,r.ProjectId, r.Method,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=prja_recognize");
}
public sealed record ErpPrjaRecognizeRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    long ProjectId = 0,
    string? Method = null,
    decimal Fraction = 0);
public sealed record ErpPrjaRecognizeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
