namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>api/v1/on-premises/health.php</c> intake.
/// Never executes INSERT into epc_onprem_health_log. PHP remains authoritative.
/// </summary>
public interface IOnPremisesHealthDryRun
{
    OnPremisesHealthDryRunResult Evaluate(OnPremisesHealthRequest request);
}

public sealed class OnPremisesHealthDryRun : IOnPremisesHealthDryRun
{
    public OnPremisesHealthDryRunResult Evaluate(OnPremisesHealthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET on-premises health intake is not implemented; PHP api/v1/on-premises/health.php remains authoritative.",
                request);
        }

        var key = (request.LicenseKey ?? string.Empty).Trim();
        if (key.Length < 8)
        {
            return Refuse("dry-run-invalid", "invalid_payload",
                "license_key required (PHP invalid_payload).", request);
        }

        var status = (request.Status ?? string.Empty).Trim();
        if (status.Length == 0)
        {
            status = "unknown";
        }

        return new OnPremisesHealthDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            LicenseKeyPreview: key.Length <= 8 ? key : key[..4] + "…" + key[^4..],
            ReportedStatus: status,
            SimulatedSql:
            [
                "SELECT * FROM `epc_onprem_licenses` WHERE `license_key`=@key (lookup NOT executed — license table remains PHP-authoritative)",
                "INSERT INTO `epc_onprem_health_log` (…) (NOT executed)",
                "UPDATE `epc_onprem_licenses` SET `last_seen_at`=@now WHERE `license_key`=@key (NOT executed)"
            ],
            Detail: "Payload shape validated; health log INSERT blocked. License lookup + activate stay PHP until dual-sample.",
            PhpAjax: "/api/v1/on-premises/health.php");
    }

    private static OnPremisesHealthDryRunResult Refuse(
        string status, string code, string detail, OnPremisesHealthRequest request) =>
        new(status, 0, true, false, true, code, false,
            Mask(request.LicenseKey), request.Status, [], detail,
            "/api/v1/on-premises/health.php");

    private static string? Mask(string? key)
    {
        var k = (key ?? string.Empty).Trim();
        if (k.Length == 0)
        {
            return null;
        }

        return k.Length <= 8 ? k : k[..4] + "…" + k[^4..];
    }
}

public sealed record OnPremisesHealthRequest(
    string? LicenseKey,
    string? Status = null,
    string? Uptime = null,
    decimal? DiskFreeGb = null,
    decimal? MemoryUsageMb = null,
    string? PhpVersion = null,
    decimal? DbSizeMb = null,
    string? LastBackup = null,
    bool ConfirmWrites = false);

public sealed record OnPremisesHealthDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? LicenseKeyPreview, string? ReportedStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload() => new
    {
        ok = true,
        surface = "on-premises",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { license_key = LicenseKeyPreview, status = ReportedStatus },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        note = Detail
    };
}
