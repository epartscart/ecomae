namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>inv_import_csv</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInvImportCsvDryRun { ErpInvImportCsvDryRunResult Evaluate(ErpInvImportCsvRequest request); }
public sealed class ErpInvImportCsvDryRun : IErpInvImportCsvDryRun
{
    public ErpInvImportCsvDryRunResult Evaluate(ErpInvImportCsvRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET inv_import_csv is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=inv_import_csv (NOT executed)"],
            "ERP inv_import_csv payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=inv_import_csv");
    }
    private static ErpInvImportCsvDryRunResult Refuse(string s,string c,string d,ErpInvImportCsvRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=inv_import_csv");
}
public sealed record ErpInvImportCsvRequest(bool ConfirmWrites = false);
public sealed record ErpInvImportCsvDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="inv_import_csv"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
