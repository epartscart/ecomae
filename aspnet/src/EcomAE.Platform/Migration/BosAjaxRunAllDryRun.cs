namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>run_all</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxRunAllDryRun { BosAjaxRunAllDryRunResult Evaluate(BosAjaxRunAllRequest request); }
public sealed class BosAjaxRunAllDryRun : IBosAjaxRunAllDryRun
{
    public BosAjaxRunAllDryRunResult Evaluate(BosAjaxRunAllRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id,
            ["ajax_epc_bos.php?action=run_all (NOT executed)"],
            "BOS run_all payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=run_all");
    }
    private static BosAjaxRunAllDryRunResult Refuse(string s,string c,string d,BosAjaxRunAllRequest r)=>
        new(s,0,true,false,true,c,false,r.Id,[],d,"/BOS/ajax_epc_bos.php?action=run_all");
}
public sealed record BosAjaxRunAllRequest(long Id=0, bool ConfirmWrites=false);
public sealed record BosAjaxRunAllDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
