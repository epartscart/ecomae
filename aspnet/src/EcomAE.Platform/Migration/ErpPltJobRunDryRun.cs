namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>plt_job_run</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPltJobRunDryRun { ErpPltJobRunDryRunResult Evaluate(ErpPltJobRunRequest request); }
public sealed class ErpPltJobRunDryRun : IErpPltJobRunDryRun
{
    public ErpPltJobRunDryRunResult Evaluate(ErpPltJobRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET plt_job_run is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=plt_job_run (NOT executed)"],
            "ERP plt_job_run payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=plt_job_run");
    }
    private static ErpPltJobRunDryRunResult Refuse(string s,string c,string d,ErpPltJobRunRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=plt_job_run");
}
public sealed record ErpPltJobRunRequest(bool ConfirmWrites = false);
public sealed record ErpPltJobRunDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="plt_job_run"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
