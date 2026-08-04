namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>coll_dunning_run</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCollDunningRunDryRun { ErpCollDunningRunDryRunResult Evaluate(ErpCollDunningRunRequest request); }
public sealed class ErpCollDunningRunDryRun : IErpCollDunningRunDryRun
{
    public ErpCollDunningRunDryRunResult Evaluate(ErpCollDunningRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET coll_dunning_run is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=coll_dunning_run (NOT executed)"],
            "ERP coll_dunning_run payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=coll_dunning_run");
    }
    private static ErpCollDunningRunDryRunResult Refuse(string s,string c,string d,ErpCollDunningRunRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=coll_dunning_run");
}
public sealed record ErpCollDunningRunRequest(bool ConfirmWrites = false);
public sealed record ErpCollDunningRunDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="coll_dunning_run"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
