namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>pf_case_act</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPfCaseActDryRun { ErpPfCaseActDryRunResult Evaluate(ErpPfCaseActRequest request); }
public sealed class ErpPfCaseActDryRun : IErpPfCaseActDryRun
{
    public ErpPfCaseActDryRunResult Evaluate(ErpPfCaseActRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET pf_case_act is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_erp.php?action=pf_case_act id=@id (NOT executed)"],
            "ERP pf_case_act payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pf_case_act");
    }
    private static ErpPfCaseActDryRunResult Refuse(string s,string c,string d,ErpPfCaseActRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=pf_case_act");
}
public sealed record ErpPfCaseActRequest(long Id, bool ConfirmWrites = false);
public sealed record ErpPfCaseActDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
