namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/content/ajax_create_sitemap.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpCreateSitemapDryRun { CpCreateSitemapDryRunResult Evaluate(CpCreateSitemapRequest request); }
public sealed class CpCreateSitemapDryRun : ICpCreateSitemapDryRun
{
    public CpCreateSitemapDryRunResult Evaluate(CpCreateSitemapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes refused; PHP cp/content/content/ajax_create_sitemap.php remains authoritative.", request);
        return new("dry-run-validated",0,true,false,true,"ok",true,request.Action,
            ["cp/content/content/ajax_create_sitemap.php (NOT executed)"],
            "CpCreateSitemap payload validated; UPDATE blocked.",
            "cp/content/content/ajax_create_sitemap.php");
    }
    private static CpCreateSitemapDryRunResult Refuse(string s,string c,string d,CpCreateSitemapRequest r)=>
        new(s,0,true,false,true,c,false,r.Action,[],d,"cp/content/content/ajax_create_sitemap.php");
}
public sealed record CpCreateSitemapRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpCreateSitemapDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,string? Action,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="cp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action=Action},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
