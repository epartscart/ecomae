namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>invoice_list</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInvoiceListDryRun { ErpInvoiceListDryRunResult Evaluate(ErpInvoiceListRequest request); }
public sealed class ErpInvoiceListDryRun : IErpInvoiceListDryRun
{
    public ErpInvoiceListDryRunResult Evaluate(ErpInvoiceListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET invoice_list is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=invoice_list (NOT executed)"],
            "ERP invoice_list payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_list");
    }
    private static ErpInvoiceListDryRunResult Refuse(string s,string c,string d,ErpInvoiceListRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=invoice_list");
}
public sealed record ErpInvoiceListRequest(bool ConfirmWrites = false);
public sealed record ErpInvoiceListDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="invoice_list"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
