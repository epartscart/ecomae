using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_alloc_run</c> / ajax <c>fin_alloc_run</c> twin: loads the rule,
/// splits the amount by the rule's basis weights (PHP <c>epc_fin_alloc_split</c> rounding),
/// and persists one <c>epc_fin_alloc_run</c> row. No GL posting; schema ensure stays PHP.
/// </summary>
public interface IErpFinAllocRunWriteService
{
    Task<ErpFinAllocRunWriteResult> RunAsync(ErpFinAllocRunWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpFinAllocRunWriteRequest(long RuleId, decimal Amount);

public sealed record ErpFinAllocRunWriteResult(ErpSimpleWriteResult Result, IReadOnlyList<KeyValuePair<string, decimal>> Lines);

public sealed class ErpFinAllocRunWriteService : IErpFinAllocRunWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinAllocRunWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFinAllocRunWriteResult> RunAsync(ErpFinAllocRunWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_fin_alloc_run", "lines_json", cancellationToken).ConfigureAwait(false)
            || !await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_fin_alloc_rule", "basis", cancellationToken).ConfigureAwait(false))
        {
            return Fail("invalid", "Allocation tables are not provisioned");
        }

        long companyId;
        string basisJson;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `company_id`, `basis` FROM `epc_fin_alloc_rule` WHERE `id`=?";
            var p = command.CreateParameter();
            p.Value = request.RuleId;
            command.Parameters.Add(p);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Fail("invalid", "Allocation rule not found");
            }

            companyId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            basisJson = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }

        var lines = Split(request.Amount, DecodeBasis(basisJson));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_fin_alloc_run` (`company_id`,`rule_id`,`amount`,`run_date`,`lines_json`,`time_created`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.RuleId,
            request.Amount,
            now,
            EncodeLines(lines),
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok(Message(lines.Count), id), lines);
    }

    public static string Message(int destinations) => $"Allocated across {destinations} destination(s)";

    /// <summary>PHP <c>epc_fin_alloc_split</c>: proportional 2dp split; rounding remainder goes to the heaviest destination.</summary>
    public static IReadOnlyList<KeyValuePair<string, decimal>> Split(decimal amount, IReadOnlyList<KeyValuePair<string, decimal>> weights)
    {
        var total = weights.Sum(w => w.Value);
        if (total <= 0)
        {
            return [];
        }

        var running = 0m;
        var outList = new List<KeyValuePair<string, decimal>>(weights.Count);
        foreach (var (dest, w) in weights)
        {
            var a = Math.Round(amount * w / total, 2, MidpointRounding.AwayFromZero);
            outList.Add(new(dest, a));
            running += a;
        }

        var diff = Math.Round(amount - running, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(diff) >= 0.01m)
        {
            var maxIdx = -1;
            var maxW = decimal.MinValue;
            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i].Value > maxW)
                {
                    maxW = weights[i].Value;
                    maxIdx = i;
                }
            }

            if (maxIdx >= 0)
            {
                outList[maxIdx] = new(outList[maxIdx].Key, Math.Round(outList[maxIdx].Value + diff, 2, MidpointRounding.AwayFromZero));
            }
        }

        return outList;
    }

    /// <summary>PHP <c>json_decode($rule['basis'], true) ?: []</c> — object dest⇒weight; anything else is empty.</summary>
    public static IReadOnlyList<KeyValuePair<string, decimal>> DecodeBasis(string? json)
    {
        var list = new List<KeyValuePair<string, decimal>>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return list;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                decimal w = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number => prop.Value.GetDecimal(),
                    JsonValueKind.String when decimal.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
                    _ => 0m,
                };
                list.Add(new(prop.Name, w));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return list;
    }

    public static string EncodeLines(IReadOnlyList<KeyValuePair<string, decimal>> lines)
        => lines.Count == 0 ? "[]" : JsonSerializer.Serialize(lines.ToDictionary(l => l.Key, l => l.Value));

    private static ErpFinAllocRunWriteResult Fail(string code, string message) => new(ErpSimpleWriteResult.Fail(code, message), []);
}
