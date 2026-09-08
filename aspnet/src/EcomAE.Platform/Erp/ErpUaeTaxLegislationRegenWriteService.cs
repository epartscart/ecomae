using System.Text.Json;
using System.Text.Json.Nodes;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_uae_tax_legislation_backfill_summaries</c> /
/// ajax <c>uae_tax_legislation_regen_summaries</c> twin.
/// Re-enriches stored FTA items and refreshes cache. Ignores <c>fetch_pdf</c> —
/// PDF excerpts stay on the Classic twin. Does not CREATE tables.
/// </summary>
public interface IErpUaeTaxLegislationRegenWriteService
{
    Task<ErpUaeTaxLegislationRegenWriteResult> RegenAsync(
        ErpUaeTaxLegislationRegenWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUaeTaxLegislationRegenWriteRequest(bool FetchPdf = false);

public sealed record ErpUaeTaxLegislationRegenWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    int Writes,
    int Updated,
    int Synced,
    int PdfExcerpts,
    bool FetchPdfs,
    IReadOnlyList<Dictionary<string, object?>> Samples)
{
    public static ErpUaeTaxLegislationRegenWriteResult Fail(string code, string message) =>
        new(false, code, message, 0, 0, 0, 0, false, []);

    public static ErpUaeTaxLegislationRegenWriteResult Ok(
        string message,
        int writes,
        int updated,
        int synced,
        IReadOnlyList<Dictionary<string, object?>> samples) =>
        new(true, "ok", message, writes, updated, synced, 0, false, samples);
}

public sealed class ErpUaeTaxLegislationRegenWriteService : IErpUaeTaxLegislationRegenWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpUaeTaxLegislationRegenWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpUaeTaxLegislationRegenWriteResult> RegenAsync(
        ErpUaeTaxLegislationRegenWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpUaeTaxLegislationRegenWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpUaeTaxLegislationLibrary.ColumnExistsAsync(connection, "epc_uae_tax_compliance_cache", "cache_key", cancellationToken).ConfigureAwait(false)
            || !await ErpUaeTaxLegislationLibrary.ColumnExistsAsync(connection, "epc_uae_tax_legislation_items", "item_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpUaeTaxLegislationRegenWriteResult.Fail("invalid", "UAE tax legislation tables are not provisioned");
        }

        var loaded = await ErpUaeTaxLegislationLibrary.LoadSearchItemsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (loaded.Count == 0)
        {
            return ErpUaeTaxLegislationRegenWriteResult.Fail(
                "empty",
                "Legislation library is empty — fetch updates from tax.gov.ae/legislation.aspx first.");
        }

        var enriched = loaded.Select(ErpUaeTaxFtaLegislation.Enrich).ToList();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var synced = await ErpUaeTaxLegislationLibrary.SyncItemsAsync(connection, enriched, now, cancellationToken).ConfigureAwait(false);
        var overall = ErpUaeTaxFtaLegislation.OverallSummaries(enriched);
        var rows = enriched.Select(ErpUaeTaxLegislationLibrary.ItemToDict).ToList();

        var cachedJson = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxFtaLegislation.CacheKey).ConfigureAwait(false);
        JsonObject payload;
        try
        {
            payload = !string.IsNullOrWhiteSpace(cachedJson)
                ? JsonNode.Parse(cachedJson) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException)
        {
            payload = new JsonObject();
        }

        payload["legislation"] = JsonSerializer.SerializeToNode(rows, ErpUaeTaxLegislationLibrary.JsonOpts);
        payload["items"] = JsonSerializer.SerializeToNode(rows, ErpUaeTaxLegislationLibrary.JsonOpts);
        payload["overall_summaries"] = JsonSerializer.SerializeToNode(overall, ErpUaeTaxLegislationLibrary.JsonOpts);
        payload["items_synced"] = synced;
        payload["summaries_backfilled_at"] = now;
        payload["summaries_need_regen"] = false;
        var url = ErpUaeTaxFtaLegislation.LegislationUrl;
        var payloadJson = payload.ToJsonString();
        var overallJson = JsonSerializer.Serialize(overall, ErpUaeTaxLegislationLibrary.JsonOpts);
        await ErpUaeTaxLegislationLibrary.UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKey, payloadJson, url, now, cancellationToken).ConfigureAwait(false);
        await ErpUaeTaxLegislationLibrary.UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKeySite, payloadJson, url, now, cancellationToken).ConfigureAwait(false);
        await ErpUaeTaxLegislationLibrary.UpsertCacheAsync(connection, ErpUaeTaxFtaLegislation.CacheKeyOverall, overallJson, url, now, cancellationToken).ConfigureAwait(false);

        var samples = enriched.Take(3).Select(s => new Dictionary<string, object?>
        {
            ["title"] = s.Title,
            ["erp_summary"] = s.ErpSummary,
            ["compliance_actions"] = (s.ComplianceActions ?? []).Take(3).ToList(),
        }).ToList();
        var writes = synced + 3;
        var message = "Regenerated summaries for " + enriched.Count + " legislation item(s). PDF excerpts stay on the Classic twin.";
        _ = request.FetchPdf;
        return ErpUaeTaxLegislationRegenWriteResult.Ok(message, writes, enriched.Count, synced, samples);
    }
}
