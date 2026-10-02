namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bank_import</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBankImportDryRun { ErpBankImportDryRunResult Evaluate(ErpBankImportRequest request); }
public sealed class ErpBankImportDryRun : IErpBankImportDryRun
{
    public ErpBankImportDryRunResult Evaluate(ErpBankImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AccountId <= 0)
            return Refuse("dry-run-invalid", "invalid_account", "account_id must be greater than zero.", request);

        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=bank_import (NOT executed)"],
            "ERP bank_import payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bank_import");
    }
    private static ErpBankImportDryRunResult Refuse(string s,string c,string d,ErpBankImportRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bank_import");
}
public sealed record ErpBankImportRequest(long AccountId = 0, string? CsvText = null, bool ConfirmWrites = false);
public sealed record ErpBankImportDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="bank_import"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
