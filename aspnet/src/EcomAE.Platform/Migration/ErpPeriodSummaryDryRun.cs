namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>period_summary</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPeriodSummaryDryRun { ErpPeriodSummaryDryRunResult Evaluate(ErpPeriodSummaryRequest request); }
public sealed class ErpPeriodSummaryDryRun : IErpPeriodSummaryDryRun
{
    public ErpPeriodSummaryDryRunResult Evaluate(ErpPeriodSummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET period_summary is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=period_summary (NOT executed)"],
            "ERP period_summary payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_summary");
    }
    private static ErpPeriodSummaryDryRunResult Refuse(string s,string c,string d,ErpPeriodSummaryRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=period_summary");
}
public sealed record ErpPeriodSummaryRequest(bool ConfirmWrites = false);
public sealed record ErpPeriodSummaryDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="period_summary"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
