namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>concurrency_status</c> when <c>confirmWrites</c> is omitted.
/// The live path (presence upsert + lock / row_version reads) is <c>IErpPresenceWriteService</c>.
/// </summary>
public interface IErpConcurrencyStatusDryRun { ErpConcurrencyStatusDryRunResult Evaluate(ErpConcurrencyStatusRequest request); }
public sealed class ErpConcurrencyStatusDryRun : IErpConcurrencyStatusDryRun
{
    public ErpConcurrencyStatusDryRunResult Evaluate(ErpConcurrencyStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused on the dry-run path; POST confirmWrites=true to run on ASP.NET.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.EntityType,request.EntityId,
            ["INSERT ... ON DUPLICATE KEY UPDATE `epc_erp_presence` (NOT executed)", "SELECT `epc_erp_edit_locks` / `row_version` (NOT executed)"],
            "ERP concurrency_status payload validated; presence upsert blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=concurrency_status");
    }
    private static ErpConcurrencyStatusDryRunResult Refuse(string s,string c,string d,ErpConcurrencyStatusRequest r)=>
        new(s,0,true,false,false,c,false,r.EntityType,r.EntityId,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=concurrency_status");
}
public sealed record ErpConcurrencyStatusRequest(string? EntityType = null, string? EntityId = null, string? Tab = null, string? Area = null, bool ConfirmWrites = false);
public sealed record ErpConcurrencyStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? EntityType,string? EntityId,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{entity_type=EntityType,entity_id=EntityId},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
