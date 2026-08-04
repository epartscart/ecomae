namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>coll_activity_log</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCollActivityLogDryRun { ErpCollActivityLogDryRunResult Evaluate(ErpCollActivityLogRequest request); }
public sealed class ErpCollActivityLogDryRun : IErpCollActivityLogDryRun
{
    public ErpCollActivityLogDryRunResult Evaluate(ErpCollActivityLogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET coll_activity_log is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_erp.php?action=coll_activity_log id=@id (NOT executed)"],
            "ERP coll_activity_log payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=coll_activity_log");
    }
    private static ErpCollActivityLogDryRunResult Refuse(string s,string c,string d,ErpCollActivityLogRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=coll_activity_log");
}
public sealed record ErpCollActivityLogRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpCollActivityLogDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
