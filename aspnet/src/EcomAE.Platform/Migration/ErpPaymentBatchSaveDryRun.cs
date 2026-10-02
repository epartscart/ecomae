namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET payment-batch writer.</summary>
public interface IErpPaymentBatchSaveDryRun { ErpPaymentBatchSaveDryRunResult Evaluate(ErpPaymentBatchSaveRequest request); }
public sealed class ErpPaymentBatchSaveDryRun : IErpPaymentBatchSaveDryRun
{
    public ErpPaymentBatchSaveDryRunResult Evaluate(ErpPaymentBatchSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET payment-batch writer.", request);
        if (request.AccountId < 0)
            return Refuse("dry-run-invalid","invalid_account","account_id must be >= 0.", request);
        if (request.BatchType is not null && request.BatchType is not ("sepa" or "local" or "cheque"))
            return Refuse("dry-run-invalid","invalid_batch_type","batch_type must be sepa, local, or cheque.", request);
        if (request.LineCount < 0)
            return Refuse("dry-run-invalid","invalid_line_count","line_count must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["INSERT epc_erp_payment_batches (NOT executed)"],
            "Payment-batch payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=payment_batch_save");
    }
    private static ErpPaymentBatchSaveDryRunResult Refuse(string s,string c,string d,ErpPaymentBatchSaveRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=payment_batch_save");
}
public sealed record ErpPaymentBatchSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    long AccountId = 0,
    string? BatchType = null,
    decimal TotalAmount = 0,
    int LineCount = 1);
public sealed record ErpPaymentBatchSaveDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
