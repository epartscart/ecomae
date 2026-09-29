namespace EcomAE.Platform.Migration;

/// <summary>Validation envelope for the live ASP.NET customer-settlement writer.</summary>
public interface IErpCustomerSettlementDryRun
{
    ErpCustomerSettlementDryRunResult Evaluate(ErpCustomerSettlementRequest request);
}

public sealed class ErpCustomerSettlementDryRun : IErpCustomerSettlementDryRun
{
    public ErpCustomerSettlementDryRunResult Evaluate(ErpCustomerSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET customer-settlement writer.",
                request);
        }

        if (request.UserId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Customer and positive amount required (PHP).", request);
        }

        var direction = (request.Direction ?? "credit").Trim().ToLowerInvariant();
        if (direction is not ("credit" or "debit"))
        {
            return Refuse("dry-run-invalid", "invalid_direction",
                "Direction must be credit or debit (PHP).", request);
        }

        var kind = (request.EntryKind ?? "adjustment").Trim().ToLowerInvariant();
        if (kind is not ("adjustment" or "settlement" or "write_off"))
        {
            kind = "adjustment";
        }

        if (kind == "write_off" && direction == "credit")
        {
            return Refuse("dry-run-invalid", "write_off_direction",
                "Write-off must reduce customer balance (debit direction) (PHP).", request);
        }

        return new ErpCustomerSettlementDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.UserId, request.Amount, direction, kind, request.OrderId,
            [
                "INSERT INTO `shop_users_accounting` (…) (NOT executed)",
                "Optional GL post_gl path is applied by the confirmed ASP.NET write"
            ],
            "Customer settlement payload validated; no write was performed.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=customer_settlement");
    }

    private static ErpCustomerSettlementDryRunResult Refuse(
        string status, string code, string detail, ErpCustomerSettlementRequest request) =>
        new(status, 0, true, false, false, code, false,
            request.UserId, request.Amount, request.Direction, request.EntryKind, request.OrderId,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=customer_settlement");
}

public sealed record ErpCustomerSettlementRequest(
    long UserId,
    decimal Amount,
    string? Direction = "credit",
    string? EntryKind = "adjustment",
    long OrderId = 0,
    bool ConfirmWrites = false);

public sealed record ErpCustomerSettlementDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long UserId, decimal Amount, string? Direction,
    string? EntryKind, long OrderId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { user_id = UserId, amount = Amount, direction = Direction, entry_kind = EntryKind, order_id = OrderId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
