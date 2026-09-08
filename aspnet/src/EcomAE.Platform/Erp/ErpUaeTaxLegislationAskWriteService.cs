using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_uae_tax_legislation_ask</c> / ajax <c>uae_tax_legislation_ask</c> twin.
/// Search + extract from stored FTA items; append last-10 Q&amp;A to cache.
/// Does not CREATE tables. PDF excerpt / KB seed stay PHP.
/// </summary>
public interface IErpUaeTaxLegislationAskWriteService
{
    Task<ErpUaeTaxLegislationAskWriteResult> AskAsync(
        ErpUaeTaxLegislationAskWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUaeTaxLegislationAskWriteRequest(string? Question = null);

public sealed record ErpUaeTaxLegislationAskWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    int Writes,
    IReadOnlyList<string> Answer,
    IReadOnlyList<ErpUaeTaxLegislationCitation> Citations,
    float Confidence,
    string Disclaimer,
    int MatchCount,
    IReadOnlyList<string> Tokens)
{
    public static ErpUaeTaxLegislationAskWriteResult FromAnswer(ErpUaeTaxLegislationAskAnswer answer, int writes) =>
        new(
            answer.Ok,
            answer.Ok ? "ok" : "invalid",
            answer.Message,
            writes,
            answer.Answer,
            answer.Citations,
            answer.Confidence,
            answer.Disclaimer,
            answer.MatchCount,
            answer.Tokens);

    public static ErpUaeTaxLegislationAskWriteResult Fail(string code, string message) =>
        new(false, code, message, 0, [], [], 0, ErpUaeTaxLegislationAsk.Disclaimer, 0, []);
}

public sealed class ErpUaeTaxLegislationAskWriteService : IErpUaeTaxLegislationAskWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpUaeTaxLegislationAskWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpUaeTaxLegislationAskWriteResult> AskAsync(
        ErpUaeTaxLegislationAskWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var question = (request.Question ?? "").Trim();
        var empty = ErpUaeTaxLegislationAsk.Answer(question, []);
        if (question.Length == 0)
        {
            return ErpUaeTaxLegislationAskWriteResult.FromAnswer(empty, 0);
        }

        if (!_connections.IsConfigured)
        {
            return ErpUaeTaxLegislationAskWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var items = await ErpUaeTaxLegislationLibrary.LoadSearchItemsAsync(connection, cancellationToken).ConfigureAwait(false);
        var answered = ErpUaeTaxLegislationAsk.Answer(question, items);
        if (!answered.Ok)
        {
            return ErpUaeTaxLegislationAskWriteResult.FromAnswer(answered, 0);
        }

        var writes = 0;
        if (await ErpUaeTaxLegislationLibrary.ColumnExistsAsync(
                connection, "epc_uae_tax_compliance_cache", "cache_key", cancellationToken).ConfigureAwait(false))
        {
            await AppendHistoryAsync(connection, question, answered, cancellationToken).ConfigureAwait(false);
            writes = 1;
        }

        return ErpUaeTaxLegislationAskWriteResult.FromAnswer(answered, writes);
    }

    private static async Task AppendHistoryAsync(
        System.Data.Common.DbConnection connection,
        string question,
        ErpUaeTaxLegislationAskAnswer answered,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1"),
            cancellationToken,
            ErpUaeTaxLegislationAsk.HistoryCacheKey).ConfigureAwait(false);
        var history = new List<object>();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                using var doc = JsonDocument.Parse(existing);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        history.Add(JsonSerializer.Deserialize<object>(el.GetRawText()) ?? new { });
                    }
                }
            }
            catch (JsonException)
            {
                history.Clear();
            }
        }

        history.Insert(0, new
        {
            question,
            answer = answered.Answer,
            citations = answered.Citations,
            confidence = answered.Confidence,
            time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        if (history.Count > 10)
        {
            history = history.Take(10).ToList();
        }

        var payload = JsonSerializer.Serialize(history, ErpUaeTaxLegislationLibrary.JsonOpts);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `time_fetched`)
                VALUES (?, ?, ?)
                ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `time_fetched` = VALUES(`time_fetched`)
                """),
            cancellationToken,
            ErpUaeTaxLegislationAsk.HistoryCacheKey,
            payload,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
    }
}
