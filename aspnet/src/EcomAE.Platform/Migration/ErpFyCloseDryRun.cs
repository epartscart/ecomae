namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fy_close</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFyCloseDryRun { ErpFyCloseDryRunResult Evaluate(ErpFyCloseRequest request); }
public sealed class ErpFyCloseDryRun : IErpFyCloseDryRun
{
    public ErpFyCloseDryRunResult Evaluate(ErpFyCloseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET fy_close is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=fy_close (NOT executed)"],
            "ERP fy_close payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fy_close");
    }
    private static ErpFyCloseDryRunResult Refuse(string s,string c,string d,ErpFyCloseRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fy_close");
}
public sealed record ErpFyCloseRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpFyCloseDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
