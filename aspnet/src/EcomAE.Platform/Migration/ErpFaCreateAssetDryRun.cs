namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fa_create_asset</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFaCreateAssetDryRun { ErpFaCreateAssetDryRunResult Evaluate(ErpFaCreateAssetRequest request); }
public sealed class ErpFaCreateAssetDryRun : IErpFaCreateAssetDryRun
{
    public ErpFaCreateAssetDryRunResult Evaluate(ErpFaCreateAssetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET fa_create_asset is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=fa_create_asset (NOT executed)"],
            "ERP fa_create_asset payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fa_create_asset");
    }
    private static ErpFaCreateAssetDryRunResult Refuse(string s,string c,string d,ErpFaCreateAssetRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fa_create_asset");
}
public sealed record ErpFaCreateAssetRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpFaCreateAssetDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
