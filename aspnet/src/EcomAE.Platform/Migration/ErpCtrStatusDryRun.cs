namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>ctr_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCtrStatusDryRun { ErpCtrStatusDryRunResult Evaluate(ErpCtrStatusRequest request); }
public sealed class ErpCtrStatusDryRun : IErpCtrStatusDryRun
{
    public ErpCtrStatusDryRunResult Evaluate(ErpCtrStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET ctr_status is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_erp.php?action=ctr_status id=@id status=@status (NOT executed)"],
            "ERP ctr_status payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_status");
    }
    private static ErpCtrStatusDryRunResult Refuse(string s,string c,string d,ErpCtrStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_status");
}
public sealed record ErpCtrStatusRequest(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
public sealed record ErpCtrStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
