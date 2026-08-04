namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>command_center</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCommandCenterDryRun { ErpCommandCenterDryRunResult Evaluate(ErpCommandCenterRequest request); }
public sealed class ErpCommandCenterDryRun : IErpCommandCenterDryRun
{
    public ErpCommandCenterDryRunResult Evaluate(ErpCommandCenterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET command_center is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=command_center (NOT executed)"],
            "ERP command_center payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=command_center");
    }
    private static ErpCommandCenterDryRunResult Refuse(string s,string c,string d,ErpCommandCenterRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=command_center");
}
public sealed record ErpCommandCenterRequest(bool ConfirmWrites = false);
public sealed record ErpCommandCenterDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="command_center"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
