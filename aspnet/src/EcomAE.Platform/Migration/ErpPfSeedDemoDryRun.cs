namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>pf_seed_demo</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPfSeedDemoDryRun { ErpPfSeedDemoDryRunResult Evaluate(ErpPfSeedDemoRequest request); }
public sealed class ErpPfSeedDemoDryRun : IErpPfSeedDemoDryRun
{
    public ErpPfSeedDemoDryRunResult Evaluate(ErpPfSeedDemoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET pf_seed_demo is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=pf_seed_demo (NOT executed)"],
            "ERP pf_seed_demo payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pf_seed_demo");
    }
    private static ErpPfSeedDemoDryRunResult Refuse(string s,string c,string d,ErpPfSeedDemoRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=pf_seed_demo");
}
public sealed record ErpPfSeedDemoRequest(bool ConfirmWrites = false);
public sealed record ErpPfSeedDemoDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="pf_seed_demo"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
