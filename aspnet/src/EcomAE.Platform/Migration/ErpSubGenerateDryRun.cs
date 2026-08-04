namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>sub_generate</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpSubGenerateDryRun { ErpSubGenerateDryRunResult Evaluate(ErpSubGenerateRequest request); }
public sealed class ErpSubGenerateDryRun : IErpSubGenerateDryRun
{
    public ErpSubGenerateDryRunResult Evaluate(ErpSubGenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET sub_generate is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=sub_generate (NOT executed)"],
            "ERP sub_generate payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sub_generate");
    }
    private static ErpSubGenerateDryRunResult Refuse(string s,string c,string d,ErpSubGenerateRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=sub_generate");
}
public sealed record ErpSubGenerateRequest(bool ConfirmWrites = false);
public sealed record ErpSubGenerateDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="sub_generate"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
