using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

public interface IErpTrialBalanceDryRun
{
    ErpTrialBalanceDryRunResult Evaluate(ErpTrialBalanceSnapshotRequest request, bool confirmWrites);
}

public sealed class ErpTrialBalanceDryRun : IErpTrialBalanceDryRun
{
    public ErpTrialBalanceDryRunResult Evaluate(ErpTrialBalanceSnapshotRequest request, bool confirmWrites)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (confirmWrites)
        {
            return new(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write.",
                0,
                false);
        }

        var check = ErpTrialBalanceWriteService.Validate(request);
        if (check is not null)
        {
            return new("dry-run-invalid", check.Code, check.Message, 0, false);
        }

        return new(
            "dry-run-validated",
            "ok",
            "Trial balance snapshot validated; write blocked until confirmWrites=true.",
            0,
            true);
    }
}

public sealed record ErpTrialBalanceDryRunResult(
    string Status,
    string ValidationCode,
    string Detail,
    int Writes,
    bool WouldWrite)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = true,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        session,
        note = Detail,
    };
}
