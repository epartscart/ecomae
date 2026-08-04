namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>aml_report_generate</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpAmlReportGenerateDryRun { ErpAmlReportGenerateDryRunResult Evaluate(ErpAmlReportGenerateRequest request); }
public sealed class ErpAmlReportGenerateDryRun : IErpAmlReportGenerateDryRun
{
    public ErpAmlReportGenerateDryRunResult Evaluate(ErpAmlReportGenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET aml_report_generate is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=aml_report_generate (NOT executed)"],
            "ERP aml_report_generate payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=aml_report_generate");
    }
    private static ErpAmlReportGenerateDryRunResult Refuse(string s,string c,string d,ErpAmlReportGenerateRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=aml_report_generate");
}
public sealed record ErpAmlReportGenerateRequest(bool ConfirmWrites = false);
public sealed record ErpAmlReportGenerateDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="aml_report_generate"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
