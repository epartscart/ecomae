namespace EcomAE.Platform.Migration;

/// <summary>Dry-run for PHP <c>fin_fx_revalue</c>. Never writes; confirmed writes run through <c>IErpFinFxRevalueWriteService</c>.</summary>
public interface IErpFinFxRevalueDryRun { ErpFinFxRevalueDryRunResult Evaluate(ErpFinFxRevalueRequest request); }
public sealed class ErpFinFxRevalueDryRun : IErpFinFxRevalueDryRun
{
    public ErpFinFxRevalueDryRunResult Evaluate(ErpFinFxRevalueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes is handled by the live /erp/ajax/fin-fx-revalue route (IErpFinFxRevalueWriteService); the dry-run evaluator never writes.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=fin_fx_revalue (NOT executed)"],
            "ERP fin_fx_revalue payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fin_fx_revalue");
    }
    private static ErpFinFxRevalueDryRunResult Refuse(string s,string c,string d,ErpFinFxRevalueRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=fin_fx_revalue");
}
public sealed record ErpFinFxRevalueRequest(bool ConfirmWrites = false);
public sealed record ErpFinFxRevalueDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="fin_fx_revalue"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
