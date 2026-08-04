namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>supplier_payment</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpSupplierPaymentDryRun { ErpSupplierPaymentDryRunResult Evaluate(ErpSupplierPaymentRequest request); }
public sealed class ErpSupplierPaymentDryRun : IErpSupplierPaymentDryRun
{
    public ErpSupplierPaymentDryRunResult Evaluate(ErpSupplierPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET supplier_payment is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_erp.php?action=supplier_payment id=@id (NOT executed)"],
            "ERP supplier_payment payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_payment");
    }
    private static ErpSupplierPaymentDryRunResult Refuse(string s,string c,string d,ErpSupplierPaymentRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_payment");
}
public sealed record ErpSupplierPaymentRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpSupplierPaymentDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
