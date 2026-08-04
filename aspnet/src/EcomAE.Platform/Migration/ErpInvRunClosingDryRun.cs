namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>inv_run_closing</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpInvRunClosingDryRun { ErpInvRunClosingDryRunResult Evaluate(ErpInvRunClosingRequest request); }
public sealed class ErpInvRunClosingDryRun : IErpInvRunClosingDryRun
{
    public ErpInvRunClosingDryRunResult Evaluate(ErpInvRunClosingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET inv_run_closing is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=inv_run_closing (NOT executed)"],
            "ERP inv_run_closing payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=inv_run_closing");
    }
    private static ErpInvRunClosingDryRunResult Refuse(string s,string c,string d,ErpInvRunClosingRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=inv_run_closing");
}
public sealed record ErpInvRunClosingRequest(bool ConfirmWrites = false);
public sealed record ErpInvRunClosingDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="inv_run_closing"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
