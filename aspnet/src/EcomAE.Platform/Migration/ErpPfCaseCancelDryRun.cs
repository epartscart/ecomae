namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>pf_case_cancel</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPfCaseCancelDryRun { ErpPfCaseCancelDryRunResult Evaluate(ErpPfCaseCancelRequest request); }
public sealed class ErpPfCaseCancelDryRun : IErpPfCaseCancelDryRun
{
    public ErpPfCaseCancelDryRunResult Evaluate(ErpPfCaseCancelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET pf_case_cancel is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_erp.php?action=pf_case_cancel (NOT executed)"],
            "ERP pf_case_cancel payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pf_case_cancel");
    }
    private static ErpPfCaseCancelDryRunResult Refuse(string s,string c,string d,ErpPfCaseCancelRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=pf_case_cancel");
}
public sealed record ErpPfCaseCancelRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpPfCaseCancelDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
