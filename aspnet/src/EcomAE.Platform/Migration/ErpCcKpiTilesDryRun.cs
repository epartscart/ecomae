namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cc_kpi_tiles</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpCcKpiTilesDryRun { ErpCcKpiTilesDryRunResult Evaluate(ErpCcKpiTilesRequest request); }
public sealed class ErpCcKpiTilesDryRun : IErpCcKpiTilesDryRun
{
    public ErpCcKpiTilesDryRunResult Evaluate(ErpCcKpiTilesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused","confirm_writes_refused","confirm_writes requested but live ASP.NET cc_kpi_tiles is not implemented; PHP ajax_erp.php remains authoritative.", request);
        
        return new("dry-run-validated",0,true,false,true,"ok",true,
            ["ajax_erp.php?action=cc_kpi_tiles (NOT executed)"],
            "ERP cc_kpi_tiles payload validated; UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cc_kpi_tiles");
    }
    private static ErpCcKpiTilesDryRunResult Refuse(string s,string c,string d,ErpCcKpiTilesRequest r)=>
        new(s,0,true,false,true,c,false,[],d,"/CP/content/shop/finance/erp/ajax_erp.php?action=cc_kpi_tiles");
}
public sealed record ErpCcKpiTilesRequest(bool ConfirmWrites = false);
public sealed record ErpCcKpiTilesDryRunResult(string Status,int Writes,bool WritesBlocked,bool CutoverAllowed,bool PhpAuthoritative,string ValidationCode,bool WouldWrite,IReadOnlyList<string> SimulatedSql,string Detail,string PhpAjax)
{
    public object ToPayload(object session)=>new{ok=true,surface="erp",status=Status,writes=Writes,writesBlocked=WritesBlocked,cutoverAllowed=CutoverAllowed,phpAuthoritative=PhpAuthoritative,validation_code=ValidationCode,would_write=WouldWrite,intended=new{action="cc_kpi_tiles"},simulated=SimulatedSql,php_ajax=PhpAjax,session,note=Detail};
}
