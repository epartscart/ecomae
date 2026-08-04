namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>design_tokens</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxDesignTokensDryRun { BosAjaxDesignTokensDryRunResult Evaluate(BosAjaxDesignTokensRequest request); }
public sealed class BosAjaxDesignTokensDryRun : IBosAjaxDesignTokensDryRun
{
    public BosAjaxDesignTokensDryRunResult Evaluate(BosAjaxDesignTokensRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_epc_bos.php?action=design_tokens (NOT executed)"],
            "BOS design_tokens payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=design_tokens");
    }
    private static BosAjaxDesignTokensDryRunResult Refuse(string s,string c,string d,BosAjaxDesignTokensRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/BOS/ajax_epc_bos.php?action=design_tokens");
}
public sealed record BosAjaxDesignTokensRequest(long Id=0, string? Code=null, bool ConfirmWrites=false);
public sealed record BosAjaxDesignTokensDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
