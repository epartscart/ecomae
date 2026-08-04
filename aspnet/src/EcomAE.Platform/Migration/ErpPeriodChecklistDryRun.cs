namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>period_checklist</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPeriodChecklistDryRun { ErpPeriodChecklistDryRunResult Evaluate(ErpPeriodChecklistRequest request); }
public sealed class ErpPeriodChecklistDryRun : IErpPeriodChecklistDryRun
{
    public ErpPeriodChecklistDryRunResult Evaluate(ErpPeriodChecklistRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET period_checklist is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=period_checklist (NOT executed)"],
            "ERP period_checklist payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_checklist");
    }
    private static ErpPeriodChecklistDryRunResult Refuse(string s,string c,string d,ErpPeriodChecklistRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=period_checklist");
}
public sealed record ErpPeriodChecklistRequest(bool ConfirmWrites = false);
public sealed record ErpPeriodChecklistDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="period_checklist"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
