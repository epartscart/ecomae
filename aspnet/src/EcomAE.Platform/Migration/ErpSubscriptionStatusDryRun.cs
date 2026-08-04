namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>sub_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpSubscriptionStatusDryRun { ErpSubscriptionStatusDryRunResult Evaluate(ErpSubscriptionStatusRequest request); }
public sealed class ErpSubscriptionStatusDryRun : IErpSubscriptionStatusDryRun
{
    public ErpSubscriptionStatusDryRunResult Evaluate(ErpSubscriptionStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET sub_status is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        var status = (request.Status ?? "active").Trim().ToLowerInvariant();
        if (status.Length == 0)
            return Refuse("dry-run-invalid","status_required","status is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,status,
            ["epc_sub_set_status(@id, @status) (NOT executed)"],
            "Subscription status payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sub_status");
    }
    private static ErpSubscriptionStatusDryRunResult Refuse(string s,string c,string d,ErpSubscriptionStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,r.Status,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=sub_status");
}
public sealed record ErpSubscriptionStatusRequest(long Id, string? Status="active", bool ConfirmWrites=false);
public sealed record ErpSubscriptionStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,string? SubStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=SubStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
