namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>prj_log_time</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPrjLogTimeDryRun { ErpPrjLogTimeDryRunResult Evaluate(ErpPrjLogTimeRequest request); }
public sealed class ErpPrjLogTimeDryRun : IErpPrjLogTimeDryRun
{
    public ErpPrjLogTimeDryRunResult Evaluate(ErpPrjLogTimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET prj_log_time is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=prj_log_time (NOT executed)"],
            "ERP prj_log_time payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=prj_log_time");
    }
    private static ErpPrjLogTimeDryRunResult Refuse(string s,string c,string d,ErpPrjLogTimeRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=prj_log_time");
}
public sealed record ErpPrjLogTimeRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpPrjLogTimeDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
