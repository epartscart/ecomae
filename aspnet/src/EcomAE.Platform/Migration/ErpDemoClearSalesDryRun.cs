namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>demo_clear_sales</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpDemoClearSalesDryRun { ErpDemoClearSalesDryRunResult Evaluate(ErpDemoClearSalesRequest request); }
public sealed class ErpDemoClearSalesDryRun : IErpDemoClearSalesDryRun
{
    public ErpDemoClearSalesDryRunResult Evaluate(ErpDemoClearSalesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET demo_clear_sales is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=demo_clear_sales (NOT executed)"],
            "ERP demo_clear_sales payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=demo_clear_sales");
    }
    private static ErpDemoClearSalesDryRunResult Refuse(string s,string c,string d,ErpDemoClearSalesRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=demo_clear_sales");
}
public sealed record ErpDemoClearSalesRequest(bool ConfirmWrites = false);
public sealed record ErpDemoClearSalesDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="demo_clear_sales"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
