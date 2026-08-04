using System.Text.RegularExpressions;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>api/v1/licenses/activate.php</c>.
/// Never executes UPDATE on epc_onprem_licenses or returns a signed cert. PHP remains authoritative.
/// </summary>
public interface IOnPremisesLicenseActivateDryRun
{
    OnPremisesLicenseActivateDryRunResult Evaluate(OnPremisesLicenseActivateRequest request);
}

public sealed class OnPremisesLicenseActivateDryRun : IOnPremisesLicenseActivateDryRun
{
    private static readonly Regex LicenseKeyPattern = new(
        @"^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public OnPremisesLicenseActivateDryRunResult Evaluate(OnPremisesLicenseActivateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET on-premises license activate is not implemented; PHP api/v1/licenses/activate.php remains authoritative.",
                request);
        }

        var key = (request.LicenseKey ?? string.Empty).Trim();
        if (!LicenseKeyPattern.IsMatch(key))
        {
            return Refuse("dry-run-invalid", "invalid_key_format",
                "License key format is invalid (PHP invalid_key_format).", request);
        }

        var fingerprint = (request.Fingerprint ?? string.Empty).Trim();
        if (fingerprint.Length == 0)
        {
            return Refuse("dry-run-invalid", "missing_fingerprint",
                "Server fingerprint is required (PHP missing_fingerprint).", request);
        }

        var hostname = Truncate(request.Hostname, 190);
        var ip = Truncate(request.Ip, 45);

        return new OnPremisesLicenseActivateDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            LicenseKeyPreview: Mask(key),
            FingerprintPreview: Mask(fingerprint),
            Hostname: hostname,
            Ip: ip,
            SimulatedSql:
            [
                "SELECT * FROM `epc_onprem_licenses` WHERE `license_key`=@key (lookup NOT executed — registry remains PHP-authoritative)",
                "UPDATE `epc_onprem_licenses` SET `status`='active', `fingerprint`=@fp, `hostname`=@host, `ip`=@ip, `activated_at`=COALESCE(...), `last_seen_at`=@now WHERE `id`=@id (NOT executed)",
                "RSA-SHA256 sign activation_cert + core_bundle (NOT executed — signing key stays PHP)"
            ],
            Detail: "Payload shape validated; activate UPDATE + cert signing blocked. not_found/revoked/expired/already_activated edge cases stay PHP until dual-sample.",
            PhpAjax: "/api/v1/licenses/activate.php");
    }

    private static OnPremisesLicenseActivateDryRunResult Refuse(
        string status, string code, string detail, OnPremisesLicenseActivateRequest request) =>
        new(status, 0, true, false, true, code, false,
            Mask(request.LicenseKey), Mask(request.Fingerprint),
            Truncate(request.Hostname, 190), Truncate(request.Ip, 45),
            [], detail, "/api/v1/licenses/activate.php");

    private static string? Truncate(string? value, int max)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            return null;
        }

        return v.Length <= max ? v : v[..max];
    }

    private static string? Mask(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            return null;
        }

        return v.Length <= 8 ? v : v[..4] + "…" + v[^4..];
    }
}

public sealed record OnPremisesLicenseActivateRequest(
    string? LicenseKey,
    string? Fingerprint,
    string? Hostname = null,
    string? Ip = null,
    string? PhpVersion = null,
    string? Os = null,
    bool ConfirmWrites = false);

public sealed record OnPremisesLicenseActivateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? LicenseKeyPreview, string? FingerprintPreview,
    string? Hostname, string? Ip, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new
        {
            license_key = LicenseKeyPreview,
            fingerprint = FingerprintPreview,
            hostname = Hostname,
            ip = Ip
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        note = Detail
    };
}
