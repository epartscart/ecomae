namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>customer_master_save</c> (credit profile upsert).
/// Never executes INSERT/UPDATE. PHP remains authoritative.
/// </summary>
public interface IErpCustomerMasterSaveDryRun
{
    ErpCustomerMasterSaveDryRunResult Evaluate(ErpCustomerMasterSaveRequest request);
}

public sealed class ErpCustomerMasterSaveDryRun : IErpCustomerMasterSaveDryRun
{
    public ErpCustomerMasterSaveDryRunResult Evaluate(ErpCustomerMasterSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET customer_master_save is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.CustomerId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "A customer ID is required (PHP).", request);
        }

        var name = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim();
        var creditLimit = request.CreditLimit ?? 0m;
        var termsDays = request.TermsDays is null or < 0 ? 30 : request.TermsDays.Value;

        return new ErpCustomerMasterSaveDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            CustomerId: request.CustomerId,
            CustomerName: name,
            CreditLimit: creditLimit,
            TermsDays: termsDays,
            OnHold: request.OnHold,
            SimulatedSql:
            [
                "INSERT INTO `epc_credit_profiles` (…) ON DUPLICATE KEY UPDATE … (NOT executed)",
                "epc_erp_dim_save_from_post customer dimensions (NOT executed)"
            ],
            Detail: "Customer master payload shape validated; credit profile upsert blocked. Dimension merge stays PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=customer_master_save");
    }

    private static ErpCustomerMasterSaveDryRunResult Refuse(
        string status, string code, string detail, ErpCustomerMasterSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.CustomerId,
            request.CustomerName, request.CreditLimit, request.TermsDays, request.OnHold,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=customer_master_save");
}

public sealed record ErpCustomerMasterSaveRequest(
    long CustomerId,
    string? CustomerName = null,
    decimal? CreditLimit = null,
    int? TermsDays = null,
    bool OnHold = false,
    bool ConfirmWrites = false);

public sealed record ErpCustomerMasterSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CustomerId, string? CustomerName,
    decimal? CreditLimit, int? TermsDays, bool OnHold,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new
        {
            customer_id = CustomerId,
            customer_name = CustomerName,
            credit_limit = CreditLimit,
            terms_days = TermsDays,
            on_hold = OnHold
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
