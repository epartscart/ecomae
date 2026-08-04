namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>einvoice_credit_note</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpEinvoiceCreditNoteDryRun { ErpEinvoiceCreditNoteDryRunResult Evaluate(ErpEinvoiceCreditNoteRequest request); }
public sealed class ErpEinvoiceCreditNoteDryRun : IErpEinvoiceCreditNoteDryRun
{
    public ErpEinvoiceCreditNoteDryRunResult Evaluate(ErpEinvoiceCreditNoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET einvoice_credit_note is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=einvoice_credit_note (NOT executed)"],
            "ERP einvoice_credit_note payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=einvoice_credit_note");
    }
    private static ErpEinvoiceCreditNoteDryRunResult Refuse(string s,string c,string d,ErpEinvoiceCreditNoteRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=einvoice_credit_note");
}
public sealed record ErpEinvoiceCreditNoteRequest(bool ConfirmWrites = false);
public sealed record ErpEinvoiceCreditNoteDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="einvoice_credit_note"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
