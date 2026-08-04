namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>ccy_set_rate</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpCcySetRateDryRun
{
    ErpCcySetRateDryRunResult Evaluate(ErpCcySetRateRequest request);
}

public sealed class ErpCcySetRateDryRun : IErpCcySetRateDryRun
{
    public ErpCcySetRateDryRunResult Evaluate(ErpCcySetRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET ccy_set_rate is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var from = (request.From ?? string.Empty).Trim().ToUpperInvariant();
        var to = (request.To ?? string.Empty).Trim().ToUpperInvariant();
        if (from.Length == 0 || to.Length == 0 || request.Rate <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Provide from, to and a positive rate (PHP).", request);
        }

        return new ErpCcySetRateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, from, to, request.Rate,
            [
                "INSERT INTO `epc_ccy_rates` (…) ON DUPLICATE KEY UPDATE rate (NOT executed)"
            ],
            "FX rate payload validated; upsert blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ccy_set_rate");
    }

    private static ErpCcySetRateDryRunResult Refuse(
        string status, string code, string detail, ErpCcySetRateRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.From, request.To, request.Rate, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ccy_set_rate");
}

public sealed record ErpCcySetRateRequest(string? From, string? To, decimal Rate, bool ConfirmWrites = false);

public sealed record ErpCcySetRateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? From, string? To, decimal Rate,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { from = From, to = To, rate = Rate },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
