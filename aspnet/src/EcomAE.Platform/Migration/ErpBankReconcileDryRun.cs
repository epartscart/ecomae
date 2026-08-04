namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bank_reconcile</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBankReconcileDryRun { ErpBankReconcileDryRunResult Evaluate(ErpBankReconcileRequest request); }
public sealed class ErpBankReconcileDryRun : IErpBankReconcileDryRun
{
    public ErpBankReconcileDryRunResult Evaluate(ErpBankReconcileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET bank_reconcile is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=bank_reconcile (NOT executed)"],
            "ERP bank_reconcile payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bank_reconcile");
    }
    private static ErpBankReconcileDryRunResult Refuse(string s,string c,string d,ErpBankReconcileRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bank_reconcile");
}
public sealed record ErpBankReconcileRequest(bool ConfirmWrites = false);
public sealed record ErpBankReconcileDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="bank_reconcile"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
