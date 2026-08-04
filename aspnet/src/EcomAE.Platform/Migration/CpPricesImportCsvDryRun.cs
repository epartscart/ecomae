namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPricesImportCsvDryRun { CpPricesImportCsvDryRunResult Evaluate(CpPricesImportCsvRequest request); }
public sealed class CpPricesImportCsvDryRun : ICpPricesImportCsvDryRun
{
    public CpPricesImportCsvDryRunResult Evaluate(CpPricesImportCsvRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php remains authoritative.", request);
        if (request.SessionId <= 0)
            return Refuse("dry-run-invalid","invalid_request","SessionId must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.SessionId,
            ["cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php (NOT executed)"],
            "CpPricesImportCsv payload validated; UPDATE blocked.",
            "cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php");
    }
    private static CpPricesImportCsvDryRunResult Refuse(string s,string c,string d,CpPricesImportCsvRequest r)=>
        new(s,0,true,false,true,c,false,r.SessionId,[],d,"cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php");
}
public sealed record CpPricesImportCsvRequest(long SessionId, bool ConfirmWrites = false);
public sealed record CpPricesImportCsvDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long SessionId,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{sessionId=SessionId},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
