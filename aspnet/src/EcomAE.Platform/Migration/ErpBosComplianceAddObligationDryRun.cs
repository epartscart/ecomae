namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>bos_compliance_add_obligation</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpBosComplianceAddObligationDryRun { ErpBosComplianceAddObligationDryRunResult Evaluate(ErpBosComplianceAddObligationRequest request); }
public sealed class ErpBosComplianceAddObligationDryRun : IErpBosComplianceAddObligationDryRun
{
    public ErpBosComplianceAddObligationDryRunResult Evaluate(ErpBosComplianceAddObligationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET bos_compliance_add_obligation is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=bos_compliance_add_obligation (NOT executed)"],
            "ERP bos_compliance_add_obligation payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_compliance_add_obligation");
    }
    private static ErpBosComplianceAddObligationDryRunResult Refuse(string s,string c,string d,ErpBosComplianceAddObligationRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=bos_compliance_add_obligation");
}
public sealed record ErpBosComplianceAddObligationRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpBosComplianceAddObligationDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
