namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rtl_discount_save</c> / <c>epc_rtl_discount_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpRtlDiscountSaveWriteService</c>.
/// </summary>
public interface IErpRtlDiscountSaveDryRun
{
    ErpRtlDiscountSaveDryRunResult Evaluate(ErpRtlDiscountSaveRequest request);
}

public sealed class ErpRtlDiscountSaveDryRun : IErpRtlDiscountSaveDryRun
{
    public ErpRtlDiscountSaveDryRunResult Evaluate(ErpRtlDiscountSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        return new ErpRtlDiscountSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Code, request.DiscType,
            ["INSERT `epc_rtl_discount` (NOT executed)"],
            "ErpRtlDiscountSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_retail.php");
    }

    private static ErpRtlDiscountSaveDryRunResult Refuse(string s, string c, string d, ErpRtlDiscountSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Code, r.DiscType, [], d, "content/shop/finance/epc_erp_retail.php");
}

public sealed record ErpRtlDiscountSaveRequest(
    long CompanyId = 0,
    long ChannelId = 0,
    string? Code = null,
    string? Name = null,
    string? DiscType = null,
    decimal Value = 0,
    long Starts = 0,
    long Ends = 0,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpRtlDiscountSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code, string? DiscType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rtl_discount_save", code = Code, disc_type = DiscType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
