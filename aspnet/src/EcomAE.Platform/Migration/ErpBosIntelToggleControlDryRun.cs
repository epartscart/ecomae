namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bos_intel_toggle_control</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBosIntelToggleControlDryRun { ErpBosIntelToggleControlDryRunResult Evaluate(ErpBosIntelToggleControlRequest request); }
public sealed class ErpBosIntelToggleControlDryRun : IErpBosIntelToggleControlDryRun
{
    public ErpBosIntelToggleControlDryRunResult Evaluate(ErpBosIntelToggleControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET bos_intel_toggle_control is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (string.IsNullOrWhiteSpace(request.ControlKey))
            return Refuse("dry-run-invalid","invalid_request","controlKey is required.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.ControlKey, request.Enabled,
            ["ajax_erp.php?action=bos_intel_toggle_control control=@controlKey (NOT executed)"],
            "ERP bos_intel_toggle_control payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_intel_toggle_control");
    }
    private static ErpBosIntelToggleControlDryRunResult Refuse(string s,string c,string d,ErpBosIntelToggleControlRequest r)=>
        new(s,0,true,false,true,c,false,r.ControlKey, r.Enabled,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bos_intel_toggle_control");
}
public sealed record ErpBosIntelToggleControlRequest(string? ControlKey = null, bool Enabled = true, bool ConfirmWrites = false);
public sealed record ErpBosIntelToggleControlDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? ControlKey, bool Enabled,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{controlKey=ControlKey,enabled=Enabled},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
