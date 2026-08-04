namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>ins_claim_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInsClaimStatusDryRun { ErpInsClaimStatusDryRunResult Evaluate(ErpInsClaimStatusRequest request); }
public sealed class ErpInsClaimStatusDryRun : IErpInsClaimStatusDryRun
{
    public ErpInsClaimStatusDryRunResult Evaluate(ErpInsClaimStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET ins_claim_status is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_erp.php?action=ins_claim_status (NOT executed)"],
            "ERP ins_claim_status payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ins_claim_status");
    }
    private static ErpInsClaimStatusDryRunResult Refuse(string s,string c,string d,ErpInsClaimStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=ins_claim_status");
}
public sealed record ErpInsClaimStatusRequest(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
public sealed record ErpInsClaimStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
