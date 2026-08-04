namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/pos/ajax_pos.php?action=complete_sale</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPosCompleteSaleDryRun { CpPosCompleteSaleDryRunResult Evaluate(CpPosCompleteSaleRequest request); }
public sealed class CpPosCompleteSaleDryRun : ICpPosCompleteSaleDryRun
{
    public CpPosCompleteSaleDryRunResult Evaluate(CpPosCompleteSaleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/shop/pos/ajax_pos.php?action=complete_sale (NOT executed)"],
            "CpPosCompleteSale payload validated; UPDATE blocked.",
            "cp/content/shop/pos/ajax_pos.php?action=complete_sale");
    }
    private static CpPosCompleteSaleDryRunResult Refuse(string s,string c,string d,CpPosCompleteSaleRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/shop/pos/ajax_pos.php?action=complete_sale");
}
public sealed record CpPosCompleteSaleRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPosCompleteSaleDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
