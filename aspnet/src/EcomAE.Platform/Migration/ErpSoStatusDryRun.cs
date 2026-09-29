namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET sales-order status writer.</summary>
public interface IErpSoStatusDryRun { ErpSoStatusDryRunResult Evaluate(ErpSoStatusRequest request); }
public sealed class ErpSoStatusDryRun : IErpSoStatusDryRun
{
    public ErpSoStatusDryRunResult Evaluate(ErpSoStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET sales-order status writer.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.Id, request.TargetStatus,
            ["ajax_erp.php?action=so_status (NOT executed)"],
            "ERP so_status payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=so_status");
    }
    private static ErpSoStatusDryRunResult Refuse(string s,string c,string d,ErpSoStatusRequest r)=>
        new(s,0,true,false,false,c,false,r.Id, r.TargetStatus,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=so_status");
}
public sealed record ErpSoStatusRequest(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
public sealed record ErpSoStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
