namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rtl_channel_save</c> / <c>epc_rtl_channel_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpRtlChannelSaveWriteService</c>.
/// </summary>
public interface IErpRtlChannelSaveDryRun
{
    ErpRtlChannelSaveDryRunResult Evaluate(ErpRtlChannelSaveRequest request);
}

public sealed class ErpRtlChannelSaveDryRun : IErpRtlChannelSaveDryRun
{
    public ErpRtlChannelSaveDryRunResult Evaluate(ErpRtlChannelSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        var type = request.ChannelType ?? "store";
        var invalid = EcomAE.Platform.Erp.ErpRtlChannelSaveWriteService.Validate(code, type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpRtlChannelSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["INSERT/UPDATE `epc_rtl_channel` (NOT executed)"],
            "ErpRtlChannelSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_retail.php");
    }

    private static ErpRtlChannelSaveDryRunResult Refuse(string s, string c, string d, ErpRtlChannelSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_retail.php");
}

public sealed record ErpRtlChannelSaveRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    string? ChannelType = null,
    string? Currency = null,
    int? Active = null,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpRtlChannelSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rtl_channel_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
