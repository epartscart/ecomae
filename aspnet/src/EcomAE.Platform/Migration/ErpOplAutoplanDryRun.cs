namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>opl_autoplan</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpOplAutoplanDryRun { ErpOplAutoplanDryRunResult Evaluate(ErpOplAutoplanRequest request); }
public sealed class ErpOplAutoplanDryRun : IErpOplAutoplanDryRun
{
    public ErpOplAutoplanDryRunResult Evaluate(ErpOplAutoplanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET opl_autoplan is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=opl_autoplan (NOT executed)"],
            "ERP opl_autoplan payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=opl_autoplan");
    }
    private static ErpOplAutoplanDryRunResult Refuse(string s,string c,string d,ErpOplAutoplanRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=opl_autoplan");
}
public sealed record ErpOplAutoplanRequest(bool ConfirmWrites = false);
public sealed record ErpOplAutoplanDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="opl_autoplan"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
