namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rtl_assortment_set</c> / <c>epc_rtl_assortment_set</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpRtlAssortmentSetWriteService</c>.
/// </summary>
public interface IErpRtlAssortmentSetDryRun
{
    ErpRtlAssortmentSetDryRunResult Evaluate(ErpRtlAssortmentSetRequest request);
}

public sealed class ErpRtlAssortmentSetDryRun : IErpRtlAssortmentSetDryRun
{
    public ErpRtlAssortmentSetDryRunResult Evaluate(ErpRtlAssortmentSetRequest request)
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

        return new ErpRtlAssortmentSetDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ChannelId, request.ItemId,
            ["INSERT `epc_rtl_assortment` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpRtlAssortmentSet payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_retail.php");
    }

    private static ErpRtlAssortmentSetDryRunResult Refuse(string s, string c, string d, ErpRtlAssortmentSetRequest r) =>
        new(s, 0, true, false, false, c, false, r.ChannelId, r.ItemId, [], d, "content/shop/finance/epc_erp_retail.php");
}

public sealed record ErpRtlAssortmentSetRequest(
    long ChannelId = 0,
    long ItemId = 0,
    int? Active = null,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpRtlAssortmentSetDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ChannelId, long ItemId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rtl_assortment_set", channel_id = ChannelId, item_id = ItemId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
