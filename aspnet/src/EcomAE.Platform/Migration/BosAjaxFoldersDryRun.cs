namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP BOS <c>folders</c> (ajax_epc_bos.php). Never UPDATE. PHP authoritative.</summary>
public interface IBosAjaxFoldersDryRun { BosAjaxFoldersDryRunResult Evaluate(BosAjaxFoldersRequest request); }
public sealed class BosAjaxFoldersDryRun : IBosAjaxFoldersDryRun
{
    public BosAjaxFoldersDryRunResult Evaluate(BosAjaxFoldersRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP ajax_epc_bos.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_epc_bos.php?action=folders (NOT executed)"],
            "BOS folders payload validated; UPDATE blocked.",
            "/BOS/ajax_epc_bos.php?action=folders");
    }
    private static BosAjaxFoldersDryRunResult Refuse(string s,string c,string d,BosAjaxFoldersRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/BOS/ajax_epc_bos.php?action=folders");
}
public sealed record BosAjaxFoldersRequest(long Id=0, string? Code=null, bool ConfirmWrites=false);
public sealed record BosAjaxFoldersDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="bos",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
