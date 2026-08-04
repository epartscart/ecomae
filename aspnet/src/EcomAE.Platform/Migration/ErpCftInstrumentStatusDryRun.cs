namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cft_instrument_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCftInstrumentStatusDryRun { ErpCftInstrumentStatusDryRunResult Evaluate(ErpCftInstrumentStatusRequest request); }
public sealed class ErpCftInstrumentStatusDryRun : IErpCftInstrumentStatusDryRun
{
    public ErpCftInstrumentStatusDryRunResult Evaluate(ErpCftInstrumentStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET cft_instrument_status is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id <= 0)
            return Refuse("dry-run-invalid","invalid_request","id must be positive.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.TargetStatus,
            ["ajax_erp.php?action=cft_instrument_status (NOT executed)"],
            "ERP cft_instrument_status payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cft_instrument_status");
    }
    private static ErpCftInstrumentStatusDryRunResult Refuse(string s,string c,string d,ErpCftInstrumentStatusRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.TargetStatus,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=cft_instrument_status");
}
public sealed record ErpCftInstrumentStatusRequest(long Id, string? TargetStatus = null, bool ConfirmWrites = false);
public sealed record ErpCftInstrumentStatusDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? TargetStatus,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,status=TargetStatus},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
