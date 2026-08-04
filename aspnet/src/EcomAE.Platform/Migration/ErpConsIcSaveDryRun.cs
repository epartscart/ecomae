namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cons_ic_save</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpConsIcSaveDryRun { ErpConsIcSaveDryRunResult Evaluate(ErpConsIcSaveRequest request); }
public sealed class ErpConsIcSaveDryRun : IErpConsIcSaveDryRun
{
    public ErpConsIcSaveDryRunResult Evaluate(ErpConsIcSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET cons_ic_save is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=cons_ic_save (NOT executed)"],
            "ERP cons_ic_save payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cons_ic_save");
    }
    private static ErpConsIcSaveDryRunResult Refuse(string s,string c,string d,ErpConsIcSaveRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=cons_ic_save");
}
public sealed record ErpConsIcSaveRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpConsIcSaveDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
