namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fin_alloc_run</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFinAllocRunDryRun { ErpFinAllocRunDryRunResult Evaluate(ErpFinAllocRunRequest request); }
public sealed class ErpFinAllocRunDryRun : IErpFinAllocRunDryRun
{
    public ErpFinAllocRunDryRunResult Evaluate(ErpFinAllocRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET fin_alloc_run is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=fin_alloc_run (NOT executed)"],
            "ERP fin_alloc_run payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fin_alloc_run");
    }
    private static ErpFinAllocRunDryRunResult Refuse(string s,string c,string d,ErpFinAllocRunRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fin_alloc_run");
}
public sealed record ErpFinAllocRunRequest(bool ConfirmWrites = false);
public sealed record ErpFinAllocRunDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="fin_alloc_run"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
