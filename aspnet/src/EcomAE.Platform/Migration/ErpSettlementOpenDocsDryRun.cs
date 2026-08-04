namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>settlement_open_docs</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpSettlementOpenDocsDryRun { ErpSettlementOpenDocsDryRunResult Evaluate(ErpSettlementOpenDocsRequest request); }
public sealed class ErpSettlementOpenDocsDryRun : IErpSettlementOpenDocsDryRun
{
    public ErpSettlementOpenDocsDryRunResult Evaluate(ErpSettlementOpenDocsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET settlement_open_docs is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=settlement_open_docs (NOT executed)"],
            "ERP settlement_open_docs payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=settlement_open_docs");
    }
    private static ErpSettlementOpenDocsDryRunResult Refuse(string s,string c,string d,ErpSettlementOpenDocsRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=settlement_open_docs");
}
public sealed record ErpSettlementOpenDocsRequest(bool ConfirmWrites = false);
public sealed record ErpSettlementOpenDocsDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="settlement_open_docs"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
