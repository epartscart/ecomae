using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Services;

public enum TenantDeploymentKind
{
    Cloud,
    OnPremises
}

public enum TenantInstallationStage
{
    Requested,
    ProvisioningCloudTenant,
    PackageReady,
    AwaitingLocalExecution,
    EnrollingInstallation,
    Synchronizing,
    Ready,
    Failed
}

public sealed record TenantInstallationProgress(
    string TenantKey,
    TenantDeploymentKind DeploymentKind,
    TenantInstallationStage Stage,
    int Percent,
    string Message,
    DateTimeOffset UpdatedAt,
    string? FailureCode = null);

public sealed record OnPremisesInstallManifest(
    string ManifestVersion,
    string TenantKey,
    string CloudBaseUrl,
    string EnrollmentUrl,
    string EnrollmentRequestId,
    string PackageVersion,
    DateTimeOffset ExpiresAt,
    string DeploymentKind = "on-premises");

public sealed record TenantInstallationState(
    string TenantKey,
    TenantDeploymentKind DeploymentKind,
    TenantInstallationStage Stage,
    int Percent,
    string Message,
    DateTimeOffset UpdatedAt,
    string? EnrollmentRequestId = null,
    string? EnrollmentId = null,
    DateTimeOffset? LastHeartbeatAt = null,
    DateTimeOffset? LastCloudSyncAt = null,
    string? FailureCode = null);

public sealed record TenantSyncEnvelope(
    string EnvelopeId,
    string TenantKey,
    string InstallationId,
    string EntityType,
    string EntityId,
    long Version,
    string Direction,
    string PayloadHash,
    DateTimeOffset CreatedAt);

public sealed record TenantSyncValidation(
    bool Accepted,
    string Code,
    string Detail);

public sealed record TenantInstallManifestRequest(
    string? TenantKey = null,
    string? CloudBaseUrl = null,
    string? PackageVersion = null,
    string? EnrollmentRequestId = null,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Pure control-plane rules shared by cloud provisioning and the on-premises installer.
/// It does not open databases, issue credentials, or execute remote commands.
/// </summary>
public static class TenantInstallationControlPlane
{
    private static readonly IReadOnlyDictionary<TenantInstallationStage, int> PercentByStage =
        new Dictionary<TenantInstallationStage, int>
        {
            [TenantInstallationStage.Requested] = 0,
            [TenantInstallationStage.ProvisioningCloudTenant] = 20,
            [TenantInstallationStage.PackageReady] = 40,
            [TenantInstallationStage.AwaitingLocalExecution] = 50,
            [TenantInstallationStage.EnrollingInstallation] = 70,
            [TenantInstallationStage.Synchronizing] = 85,
            [TenantInstallationStage.Ready] = 100,
            [TenantInstallationStage.Failed] = 0
        };

    public static TenantInstallationState Start(
        string tenantKey,
        TenantDeploymentKind deploymentKind,
        DateTimeOffset now)
    {
        var normalizedTenantKey = NormalizeTenantKey(tenantKey);
        return new TenantInstallationState(
            normalizedTenantKey,
            deploymentKind,
            TenantInstallationStage.Requested,
            0,
            "Installation requested.",
            now);
    }

    public static TenantInstallationState Advance(
        TenantInstallationState state,
        TenantInstallationStage nextStage,
        DateTimeOffset now,
        string? message = null,
        string? enrollmentRequestId = null,
        string? enrollmentId = null)
    {
        if (nextStage == TenantInstallationStage.Failed)
        {
            throw new ArgumentException("Use Fail to record a failed installation.", nameof(nextStage));
        }

        if (!IsAllowedTransition(state.Stage, nextStage))
        {
            throw new InvalidOperationException(
                $"Installation cannot transition from {state.Stage} to {nextStage}.");
        }

        return state with
        {
            Stage = nextStage,
            Percent = PercentByStage[nextStage],
            Message = string.IsNullOrWhiteSpace(message) ? DefaultMessage(nextStage) : message.Trim(),
            UpdatedAt = now,
            EnrollmentRequestId = enrollmentRequestId ?? state.EnrollmentRequestId,
            EnrollmentId = enrollmentId ?? state.EnrollmentId,
            FailureCode = null
        };
    }

    public static TenantInstallationState Fail(
        TenantInstallationState state,
        string failureCode,
        string message,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("A failure code is required.", nameof(failureCode));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A failure message is required.", nameof(message));
        }

        return state with
        {
            Stage = TenantInstallationStage.Failed,
            Percent = 0,
            Message = message.Trim(),
            UpdatedAt = now,
            FailureCode = failureCode.Trim()
        };
    }

    public static OnPremisesInstallManifest CreateManifest(
        string tenantKey,
        string cloudBaseUrl,
        string packageVersion,
        string enrollmentRequestId,
        DateTimeOffset expiresAt)
    {
        var normalizedCloudBaseUrl = NormalizeHttpsUrl(cloudBaseUrl, nameof(cloudBaseUrl));
        var normalizedTenantKey = NormalizeTenantKey(tenantKey);
        var normalizedRequestId = NormalizeOpaqueId(enrollmentRequestId, nameof(enrollmentRequestId));
        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new ArgumentException("A package version is required.", nameof(packageVersion));
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("The installer manifest must expire in the future.", nameof(expiresAt));
        }

        return new OnPremisesInstallManifest(
            "1",
            normalizedTenantKey,
            normalizedCloudBaseUrl,
            normalizedCloudBaseUrl + "/api/v1/tenant-installations/enroll",
            normalizedRequestId,
            packageVersion.Trim(),
            expiresAt);
    }

    public static TenantSyncValidation ValidateSyncEnvelope(TenantSyncEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.EnvelopeId)
            || string.IsNullOrWhiteSpace(envelope.TenantKey)
            || string.IsNullOrWhiteSpace(envelope.InstallationId)
            || string.IsNullOrWhiteSpace(envelope.EntityType)
            || string.IsNullOrWhiteSpace(envelope.EntityId)
            || envelope.Version <= 0
            || string.IsNullOrWhiteSpace(envelope.PayloadHash))
        {
            return new(false, "invalid-envelope", "Required synchronization fields are missing.");
        }

        if (!string.Equals(envelope.Direction, "cloud-to-onpremises", StringComparison.Ordinal)
            && !string.Equals(envelope.Direction, "onpremises-to-cloud", StringComparison.Ordinal))
        {
            return new(false, "invalid-direction", "Synchronization direction is not supported.");
        }

        if (!IsSha256(envelope.PayloadHash))
        {
            return new(false, "invalid-payload-hash", "Payload hash must be a SHA-256 hex digest.");
        }

        return new(true, "accepted", "Envelope shape is valid; authorization, tenant scope, replay, and conflict checks remain mandatory.");
    }

    public static string Sha256Hex(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NormalizeTenantKey(string tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            throw new ArgumentException("A tenant key is required.", nameof(tenantKey));
        }

        var normalized = tenantKey.Trim().ToLowerInvariant();
        if (normalized.Length > 80
            || normalized.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')))
        {
            throw new ArgumentException("Tenant key contains unsupported characters.", nameof(tenantKey));
        }

        return normalized;
    }

    private static bool IsAllowedTransition(TenantInstallationStage current, TenantInstallationStage next)
    {
        return (current, next) switch
        {
            (TenantInstallationStage.Requested, TenantInstallationStage.ProvisioningCloudTenant) => true,
            (TenantInstallationStage.ProvisioningCloudTenant, TenantInstallationStage.PackageReady) => true,
            (TenantInstallationStage.PackageReady, TenantInstallationStage.AwaitingLocalExecution) => true,
            (TenantInstallationStage.AwaitingLocalExecution, TenantInstallationStage.EnrollingInstallation) => true,
            (TenantInstallationStage.EnrollingInstallation, TenantInstallationStage.Synchronizing) => true,
            (TenantInstallationStage.Synchronizing, TenantInstallationStage.Ready) => true,
            _ => false
        };
    }

    private static string DefaultMessage(TenantInstallationStage stage)
    {
        return stage switch
        {
            TenantInstallationStage.ProvisioningCloudTenant => "Cloud tenant resources are being provisioned.",
            TenantInstallationStage.PackageReady => "The on-premises setup package is ready.",
            TenantInstallationStage.AwaitingLocalExecution => "Run the setup package on the customer system.",
            TenantInstallationStage.EnrollingInstallation => "The local installation is enrolling with the cloud control plane.",
            TenantInstallationStage.Synchronizing => "Initial tenant-scoped synchronization is in progress.",
            TenantInstallationStage.Ready => "Installation is enrolled and ready.",
            _ => "Installation status updated."
        };
    }

    private static string NormalizeHttpsUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("A clean HTTPS base URL is required.", parameterName);
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string NormalizeOpaqueId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
        {
            throw new ArgumentException("An opaque enrollment request id is required.", parameterName);
        }

        return value.Trim();
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }
}
