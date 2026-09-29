namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET collections-case status writer.</summary>
public interface IErpCollectionsCaseStatusDryRun { ErpCollectionsCaseStatusDryRunResult Evaluate(ErpCollectionsCaseStatusRequest request); }
public sealed class ErpCollectionsCaseStatusDryRun : IErpCollectionsCaseStatusDryRun
{
    public ErpCollectionsCaseStatusDryRunResult Evaluate(ErpCollectionsCaseStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET collections-case status writer.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        var status = (request.Status ?? "new").Trim().ToLowerInvariant();
        if (status.Length == 0)
            return Refuse("dry-run-invalid","status_required","status is required.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.Id,status,
            ["epc_coll_case_set_status(@id, @status) (NOT executed)"],
            "Collections case status payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=coll_case_status");
    }
    private static ErpCollectionsCaseStatusDryRunResult Refuse(string s,string c,string d,ErpCollectionsCaseStatusRequest r)=>
        new(s,0,true,false,false,c,false,r.Id,r.Status,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=coll_case_status");
}
public sealed record ErpCollectionsCaseStatusRequest(long Id, string? Status="new", bool ConfirmWrites=false);
public sealed record ErpCollectionsCaseStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,string? CaseStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=CaseStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
