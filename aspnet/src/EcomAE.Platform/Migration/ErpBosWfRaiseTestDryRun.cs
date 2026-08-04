namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bos_wf_raise_test</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBosWfRaiseTestDryRun { ErpBosWfRaiseTestDryRunResult Evaluate(ErpBosWfRaiseTestRequest request); }
public sealed class ErpBosWfRaiseTestDryRun : IErpBosWfRaiseTestDryRun
{
    public ErpBosWfRaiseTestDryRunResult Evaluate(ErpBosWfRaiseTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET bos_wf_raise_test is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=bos_wf_raise_test (NOT executed)"],
            "ERP bos_wf_raise_test payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_wf_raise_test");
    }
    private static ErpBosWfRaiseTestDryRunResult Refuse(string s,string c,string d,ErpBosWfRaiseTestRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bos_wf_raise_test");
}
public sealed record ErpBosWfRaiseTestRequest(bool ConfirmWrites = false);
public sealed record ErpBosWfRaiseTestDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="bos_wf_raise_test"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
