namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bos_wf_decide</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBosWfDecideDryRun { ErpBosWfDecideDryRunResult Evaluate(ErpBosWfDecideRequest request); }
public sealed class ErpBosWfDecideDryRun : IErpBosWfDecideDryRun
{
    public ErpBosWfDecideDryRunResult Evaluate(ErpBosWfDecideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET bos_wf_decide is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Approve, request.Note,
            ["ajax_erp.php?action=bos_wf_decide id=@id (NOT executed)"],
            "ERP bos_wf_decide payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_wf_decide");
    }
    private static ErpBosWfDecideDryRunResult Refuse(string s,string c,string d,ErpBosWfDecideRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Approve, r.Note,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bos_wf_decide");
}
public sealed record ErpBosWfDecideRequest(long Id, bool Approve = true, string? Note = null, bool ConfirmWrites = false);
public sealed record ErpBosWfDecideDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, bool Approve, string? Note,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,approve=Approve,note=Note},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
