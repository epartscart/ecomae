namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cs_import_declaration_pdf</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCsImportDeclarationPdfDryRun { ErpCsImportDeclarationPdfDryRunResult Evaluate(ErpCsImportDeclarationPdfRequest request); }
public sealed class ErpCsImportDeclarationPdfDryRun : IErpCsImportDeclarationPdfDryRun
{
    public ErpCsImportDeclarationPdfDryRunResult Evaluate(ErpCsImportDeclarationPdfRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET cs_import_declaration_pdf is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=cs_import_declaration_pdf (NOT executed)"],
            "ERP cs_import_declaration_pdf payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cs_import_declaration_pdf");
    }
    private static ErpCsImportDeclarationPdfDryRunResult Refuse(string s,string c,string d,ErpCsImportDeclarationPdfRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=cs_import_declaration_pdf");
}
public sealed record ErpCsImportDeclarationPdfRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpCsImportDeclarationPdfDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
