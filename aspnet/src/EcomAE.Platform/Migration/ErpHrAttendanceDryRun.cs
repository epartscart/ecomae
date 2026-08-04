namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>hr_attendance</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpHrAttendanceDryRun { ErpHrAttendanceDryRunResult Evaluate(ErpHrAttendanceRequest request); }
public sealed class ErpHrAttendanceDryRun : IErpHrAttendanceDryRun
{
    public ErpHrAttendanceDryRunResult Evaluate(ErpHrAttendanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET hr_attendance is not implemented; PHP ajax_erp.php remains authoritative.", request);
        if (request.Id < 0)
            return Refuse("dry-run-invalid","invalid_request","id must be >= 0.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Id, request.Code,
            ["ajax_erp.php?action=hr_attendance (NOT executed)"],
            "ERP hr_attendance payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=hr_attendance");
    }
    private static ErpHrAttendanceDryRunResult Refuse(string s,string c,string d,ErpHrAttendanceRequest r)=>
        new(s,0,true,false,true,c,false,r.Id, r.Code,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=hr_attendance");
}
public sealed record ErpHrAttendanceRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);
public sealed record ErpHrAttendanceDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,long Id, string? Code,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{id=Id,code=Code},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
