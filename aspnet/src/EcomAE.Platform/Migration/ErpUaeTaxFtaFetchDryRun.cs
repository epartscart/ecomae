namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>uae_tax_fta_fetch</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpUaeTaxFtaFetchDryRun { ErpUaeTaxFtaFetchDryRunResult Evaluate(ErpUaeTaxFtaFetchRequest request); }
public sealed class ErpUaeTaxFtaFetchDryRun : IErpUaeTaxFtaFetchDryRun
{
    public ErpUaeTaxFtaFetchDryRunResult Evaluate(ErpUaeTaxFtaFetchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET uae_tax_fta_fetch is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=uae_tax_fta_fetch (NOT executed)"],
            "ERP uae_tax_fta_fetch payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_fta_fetch");
    }
    private static ErpUaeTaxFtaFetchDryRunResult Refuse(string s,string c,string d,ErpUaeTaxFtaFetchRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_fta_fetch");
}
public sealed record ErpUaeTaxFtaFetchRequest(bool ConfirmWrites = false);
public sealed record ErpUaeTaxFtaFetchDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="uae_tax_fta_fetch"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
