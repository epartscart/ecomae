using System.Text.Json;
using System.Text.Json.Nodes;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Validates digest JSON payloads against <see cref="SurfacePayloadContractCatalog"/> field contracts.
/// Used by tests/harness before any cutover consideration.
/// </summary>
public static class SurfaceDigestContractValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string SerializeEnvelope(object envelope)
        => JsonSerializer.Serialize(envelope, JsonOptions);

    public static IReadOnlyList<string> Validate(SurfacePayloadContract contract, string json)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var failures = new List<string>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return [$"{contract.AspNetRoute}: invalid JSON ({ex.Message})"];
        }

        if (root is not JsonObject obj)
        {
            return [$"{contract.AspNetRoute}: root must be an object"];
        }

        foreach (var field in contract.RequiredEnvelopeFields)
        {
            if (!obj.ContainsKey(field))
            {
                failures.Add($"{contract.AspNetRoute}: missing envelope field '{field}'");
            }
        }

        var itemFields = contract.RequiredSummaryOrItemFields;
        if (itemFields.Count == 0)
        {
            return failures;
        }

        if (obj.TryGetPropertyValue("summary", out var summaryNode) && summaryNode is JsonObject summary)
        {
            foreach (var field in itemFields)
            {
                if (!summary.ContainsKey(field))
                {
                    failures.Add($"{contract.AspNetRoute}: missing summary field '{field}'");
                }
            }

            return failures;
        }

        if (obj.TryGetPropertyValue("readiness", out var readinessNode) && readinessNode is JsonObject readiness)
        {
            foreach (var field in itemFields)
            {
                if (!readiness.ContainsKey(field))
                {
                    failures.Add($"{contract.AspNetRoute}: missing readiness field '{field}'");
                }
            }

            return failures;
        }

        // List digests: find first array property and validate item shape when non-empty.
        foreach (var property in obj)
        {
            if (property.Value is not JsonArray array)
            {
                continue;
            }

            if (array.Count == 0)
            {
                // Empty list still satisfies contract when migration source returns no rows.
                return failures;
            }

            if (array[0] is not JsonObject item)
            {
                failures.Add($"{contract.AspNetRoute}: first item in '{property.Key}' must be an object");
                return failures;
            }

            foreach (var field in itemFields)
            {
                if (!item.ContainsKey(field))
                {
                    failures.Add($"{contract.AspNetRoute}: missing item field '{property.Key}[].{field}'");
                }
            }

            return failures;
        }

        // Offer-style API payloads.
        if (obj.TryGetPropertyValue("offers", out var offersNode) && offersNode is JsonArray offers)
        {
            if (offers.Count == 0)
            {
                return failures;
            }

            if (offers[0] is not JsonObject offer)
            {
                failures.Add($"{contract.AspNetRoute}: offers[0] must be an object");
                return failures;
            }

            foreach (var field in itemFields)
            {
                if (!offer.ContainsKey(field))
                {
                    failures.Add($"{contract.AspNetRoute}: missing offer field '{field}'");
                }
            }
        }

        return failures;
    }
}
