namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET supplier-payment writer.</summary>
public interface IErpSupplierPaymentDryRun { ErpSupplierPaymentDryRunResult Evaluate(ErpSupplierPaymentRequest request); }
public sealed class ErpSupplierPaymentDryRun : IErpSupplierPaymentDryRun
{
    public ErpSupplierPaymentDryRunResult Evaluate(ErpSupplierPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","Use confirm_writes=true to execute the live ASP.NET supplier-payment writer.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,false,"ok",true,request.Id,
            ["ajax_erp.php?action=supplier_payment id=@id (NOT executed)"],
            "ERP supplier_payment payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_payment");
    }
    private static ErpSupplierPaymentDryRunResult Refuse(string s,string c,string d,ErpSupplierPaymentRequest r)=>
        new(s,0,true,false,false,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_payment");
}
public sealed record ErpSupplierPaymentRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpSupplierPaymentDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
