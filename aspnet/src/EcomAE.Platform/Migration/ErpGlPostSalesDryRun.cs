namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>gl_post_sales</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpGlPostSalesDryRun
{
    ErpGlPostSalesDryRunResult Evaluate(ErpGlPostSalesRequest request);
}

public sealed class ErpGlPostSalesDryRun : IErpGlPostSalesDryRun
{
    public ErpGlPostSalesDryRunResult Evaluate(ErpGlPostSalesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET gl_post_sales is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.DateFromUnix is < 0 || request.DateToUnix is < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "date range unix must be >= 0.", request);
        }

        if (request.DateFromUnix is > 0 && request.DateToUnix is > 0 && request.DateFromUnix > request.DateToUnix)
        {
            return Refuse("dry-run-invalid", "invalid_range", "dateFrom must be <= dateTo.", request);
        }

        return new ErpGlPostSalesDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.DateFromUnix, request.DateToUnix,
            ["epc_erp_gl_post_sales_orders(@from, @to) journal INSERTs (NOT executed)"],
            "GL post-sales date window validated; journal posting blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_post_sales");
    }

    private static ErpGlPostSalesDryRunResult Refuse(
        string status, string code, string detail, ErpGlPostSalesRequest request) =>
        new(status, 0, true, false, true, code, false, request.DateFromUnix, request.DateToUnix, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_post_sales");
}

public sealed record ErpGlPostSalesRequest(long? DateFromUnix = null, long? DateToUnix = null, bool ConfirmWrites = false);

public sealed record ErpGlPostSalesDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long? DateFromUnix, long? DateToUnix,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { date_from_unix = DateFromUnix, date_to_unix = DateToUnix },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
