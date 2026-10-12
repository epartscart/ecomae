using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Services;

public enum HybridSyncOwnership
{
    Cloud,
    Site
}

public enum HybridSyncDecisionKind
{
    Accept,
    Replay,
    Conflict,
    Reject,
    Empty
}

public sealed record HybridSyncDecision(
    HybridSyncDecisionKind Kind,
    string Code,
    string Detail);

/// <summary>
/// Pure hybrid sync rules: outbound HTTPS envelopes, declared ownership,
/// idempotent replay, conflicts queued — never a silent overwrite, never an invented send.
/// </summary>
public static class HybridSyncControlPlane
{
    public static readonly IReadOnlySet<string> MasterEntityTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "customer",
        "supplier",
        "item",
        "chart_of_accounts",
        "tax_code",
        "staff_user",
        "price_list",
    };

    public static readonly IReadOnlySet<string> TransactionEntityTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "sales_order",
        "sales_invoice",
        "purchase_order",
        "purchase_invoice",
        "payment",
        "stock_movement",
        "journal",
    };

    private static readonly HashSet<string> SecretPayloadKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "db_password",
        "secret",
        "token",
        "api_key",
        "enrollment",
        "enrollment_request_id",
    };

    public static HybridSyncOwnership? OwnershipOf(string? entityType)
    {
        var key = NormalizeEntityType(entityType);
        if (MasterEntityTypes.Contains(key))
        {
            return HybridSyncOwnership.Cloud;
        }

        if (TransactionEntityTypes.Contains(key))
        {
            return HybridSyncOwnership.Site;
        }

        return null;
    }

    public static bool IsCloudToSite(string? direction)
        => direction is "cloud-to-hybrid" or "cloud-to-onpremises";

    public static bool IsSiteToCloud(string? direction)
        => direction is "hybrid-to-cloud" or "onpremises-to-cloud";

    public static bool IsEnrollmentRequestId(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length is < 8 or > 120 || !raw.StartsWith("enr_", StringComparison.Ordinal))
        {
            return false;
        }

        return raw.Skip(4).All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    public static string NewEnrollmentRequestId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return "enr_" + Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static string NewInstallationId()
        => "inst_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

    public static string NewEnvelopeId()
        => "env_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

    public static string NormalizeEntityType(string? entityType)
        => (entityType ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');

    public static HybridSyncDecision DecideIngest(
        TenantSyncEnvelope envelope,
        string? existingEnvelopeId,
        long existingVersion,
        string? existingStatus)
    {
        var shape = TenantInstallationControlPlane.ValidateSyncEnvelope(envelope);
        if (!shape.Accepted)
        {
            return new(HybridSyncDecisionKind.Reject, shape.Code, shape.Detail);
        }

        var ownership = OwnershipOf(envelope.EntityType);
        if (ownership is null)
        {
            return new(HybridSyncDecisionKind.Reject, "unknown-entity", "Entity type is not in the hybrid ownership matrix.");
        }

        if (IsCloudToSite(envelope.Direction) && ownership != HybridSyncOwnership.Cloud)
        {
            return new(
                HybridSyncDecisionKind.Conflict,
                "ownership-conflict",
                "Cloud cannot overwrite a site-owned transaction. Conflict queued.");
        }

        if (IsSiteToCloud(envelope.Direction) && ownership != HybridSyncOwnership.Site)
        {
            return new(
                HybridSyncDecisionKind.Conflict,
                "ownership-conflict",
                "Site cannot overwrite a cloud-owned master. Conflict queued.");
        }

        if (!string.IsNullOrWhiteSpace(existingEnvelopeId)
            && string.Equals(existingEnvelopeId, envelope.EnvelopeId, StringComparison.Ordinal)
            && existingStatus is "queued" or "applied" or "replayed")
        {
            return new(HybridSyncDecisionKind.Replay, "replay", "Envelope id already stored; idempotent replay accepted.");
        }

        if (existingVersion > 0
            && existingVersion >= envelope.Version
            && !string.Equals(existingEnvelopeId, envelope.EnvelopeId, StringComparison.Ordinal))
        {
            return new(
                HybridSyncDecisionKind.Conflict,
                "version-conflict",
                "A newer or equal version already exists. Conflict queued — never silently overwritten.");
        }

        return new(HybridSyncDecisionKind.Accept, "accepted", "Envelope accepted for the change-log cursor.");
    }

    public static HybridSyncDecision DecidePull(int pendingCount)
    {
        if (pendingCount <= 0)
        {
            return new(
                HybridSyncDecisionKind.Empty,
                "empty",
                "No pending master envelopes. This path does not invent a send.");
        }

        return new(HybridSyncDecisionKind.Accept, "pending", pendingCount + " master envelope(s) ready to pull.");
    }

    public static string SanitizePayloadJson(string? payloadJson)
    {
        var raw = (payloadJson ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return "{}";
        }

        if (raw.Length > 256 * 1024)
        {
            throw new ArgumentException("Payload exceeds the 256 KiB hybrid envelope limit.", nameof(payloadJson));
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return raw;
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (SecretPayloadKeys.Contains(property.Name))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            throw new ArgumentException("Payload must be a JSON object.", nameof(payloadJson));
        }
    }
}
