namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>einvoice_poll_asp</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpEinvoicePollAspDryRun { ErpEinvoicePollAspDryRunResult Evaluate(ErpEinvoicePollAspRequest request); }
public sealed class ErpEinvoicePollAspDryRun : IErpEinvoicePollAspDryRun
{
    public ErpEinvoicePollAspDryRunResult Evaluate(ErpEinvoicePollAspRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET einvoice_poll_asp is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=einvoice_poll_asp (NOT executed)"],
            "ERP einvoice_poll_asp payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=einvoice_poll_asp");
    }
    private static ErpEinvoicePollAspDryRunResult Refuse(string s,string c,string d,ErpEinvoicePollAspRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=einvoice_poll_asp");
}
public sealed record ErpEinvoicePollAspRequest(bool ConfirmWrites = false);
public sealed record ErpEinvoicePollAspDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="einvoice_poll_asp"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
