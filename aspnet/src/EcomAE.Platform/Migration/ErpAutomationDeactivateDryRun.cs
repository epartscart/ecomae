namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>automation_deactivate</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpAutomationDeactivateDryRun { ErpAutomationDeactivateDryRunResult Evaluate(ErpAutomationDeactivateRequest request); }
public sealed class ErpAutomationDeactivateDryRun : IErpAutomationDeactivateDryRun
{
    public ErpAutomationDeactivateDryRunResult Evaluate(ErpAutomationDeactivateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET automation_deactivate is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=automation_deactivate (NOT executed)"],
            "ERP automation_deactivate payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=automation_deactivate");
    }
    private static ErpAutomationDeactivateDryRunResult Refuse(string s,string c,string d,ErpAutomationDeactivateRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=automation_deactivate");
}
public sealed record ErpAutomationDeactivateRequest(bool ConfirmWrites = false);
public sealed record ErpAutomationDeactivateDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="automation_deactivate"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
