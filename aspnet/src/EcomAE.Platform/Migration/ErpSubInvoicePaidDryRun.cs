namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>sub_invoice_paid</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpSubInvoicePaidDryRun { ErpSubInvoicePaidDryRunResult Evaluate(ErpSubInvoicePaidRequest request); }
public sealed class ErpSubInvoicePaidDryRun : IErpSubInvoicePaidDryRun
{
    public ErpSubInvoicePaidDryRunResult Evaluate(ErpSubInvoicePaidRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET sub_invoice_paid is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_erp.php?action=sub_invoice_paid id=@id (NOT executed)"],
            "ERP sub_invoice_paid payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sub_invoice_paid");
    }
    private static ErpSubInvoicePaidDryRunResult Refuse(string s,string c,string d,ErpSubInvoicePaidRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=sub_invoice_paid");
}
public sealed record ErpSubInvoicePaidRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpSubInvoicePaidDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
