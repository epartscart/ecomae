namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>hr_expense_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpHrExpenseStatusDryRun { ErpHrExpenseStatusDryRunResult Evaluate(ErpHrExpenseStatusRequest request); }
public sealed class ErpHrExpenseStatusDryRun : IErpHrExpenseStatusDryRun
{
    public ErpHrExpenseStatusDryRunResult Evaluate(ErpHrExpenseStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET hr_expense_status is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_erp.php?action=hr_expense_status id=@id (NOT executed)"],
            "ERP hr_expense_status payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=hr_expense_status");
    }
    private static ErpHrExpenseStatusDryRunResult Refuse(string s,string c,string d,ErpHrExpenseStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=hr_expense_status");
}
public sealed record ErpHrExpenseStatusRequest(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
public sealed record ErpHrExpenseStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
