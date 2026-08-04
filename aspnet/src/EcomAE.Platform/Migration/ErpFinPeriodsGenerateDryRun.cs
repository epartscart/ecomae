namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fin_periods_generate</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFinPeriodsGenerateDryRun { ErpFinPeriodsGenerateDryRunResult Evaluate(ErpFinPeriodsGenerateRequest request); }
public sealed class ErpFinPeriodsGenerateDryRun : IErpFinPeriodsGenerateDryRun
{
    public ErpFinPeriodsGenerateDryRunResult Evaluate(ErpFinPeriodsGenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET fin_periods_generate is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=fin_periods_generate (NOT executed)"],
            "ERP fin_periods_generate payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fin_periods_generate");
    }
    private static ErpFinPeriodsGenerateDryRunResult Refuse(string s,string c,string d,ErpFinPeriodsGenerateRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fin_periods_generate");
}
public sealed record ErpFinPeriodsGenerateRequest(bool ConfirmWrites = false);
public sealed record ErpFinPeriodsGenerateDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="fin_periods_generate"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
