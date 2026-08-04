namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fx_post_revaluation</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFxPostRevaluationDryRun { ErpFxPostRevaluationDryRunResult Evaluate(ErpFxPostRevaluationRequest request); }
public sealed class ErpFxPostRevaluationDryRun : IErpFxPostRevaluationDryRun
{
    public ErpFxPostRevaluationDryRunResult Evaluate(ErpFxPostRevaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET fx_post_revaluation is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=fx_post_revaluation (NOT executed)"],
            "ERP fx_post_revaluation payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fx_post_revaluation");
    }
    private static ErpFxPostRevaluationDryRunResult Refuse(string s,string c,string d,ErpFxPostRevaluationRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fx_post_revaluation");
}
public sealed record ErpFxPostRevaluationRequest(bool ConfirmWrites = false);
public sealed record ErpFxPostRevaluationDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="fx_post_revaluation"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
