using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_expense_save</c> twin. Schema ensure, employee save,
/// leave request, and attendance stay PHP.
/// </summary>
public interface IErpHrExpenseSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long employeeId,
        string? title,
        IReadOnlyList<ErpHrExpenseLine> lines,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrExpenseLine(string Label, decimal Amount);

public sealed class ErpHrExpenseSaveWriteService : IErpHrExpenseSaveWriteService
{
    private static readonly Regex FormLineKey = new(
        @"^lines\[(\d+)\]\[(label|amount)\]$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrExpenseSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long employeeId,
        string? title,
        IReadOnlyList<ErpHrExpenseLine> lines,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select an employee");
        }

        var kept = NormalizeLines(lines);
        if (kept.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one expense line");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var claimTitle = Clip(title ?? string.Empty, 160);
        var total = 0m;
        foreach (var line in kept)
        {
            total = decimal.Round(total + line.Amount, 2, MidpointRounding.AwayFromZero);
        }

        var payload = JsonSerializer.Serialize(
            kept.Select(line => new { label = line.Label, amount = line.Amount }));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hr_expenses` (`employee_id`,`title`,`amount`,`status`,`lines`,`time_created`) VALUES (?,?,?,'draft',?,?)"),
            cancellationToken,
            employeeId, claimTitle, total, payload, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(FormatSavedMessage(total), inserted);
    }

    public static string FormatSavedMessage(decimal amount)
        => "Expense claim saved — " + amount.ToString("#,0.00", CultureInfo.InvariantCulture) + " AED";

    public static IReadOnlyList<ErpHrExpenseLine> NormalizeLines(IEnumerable<ErpHrExpenseLine>? raw)
    {
        var kept = new List<ErpHrExpenseLine>();
        if (raw is null)
        {
            return kept;
        }

        foreach (var line in raw)
        {
            var amount = decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount == 0)
            {
                continue;
            }

            kept.Add(new ErpHrExpenseLine(line.Label ?? string.Empty, amount));
        }

        return kept;
    }

    public static IReadOnlyList<ErpHrExpenseLine> ParseLines(
        IReadOnlyList<ErpHrExpenseLine>? jsonLines,
        string? linesJson,
        IFormCollection? form)
    {
        if (jsonLines is { Count: > 0 })
        {
            return NormalizeLines(jsonLines);
        }

        if (!string.IsNullOrWhiteSpace(linesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(linesJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var parsed = new List<ErpHrExpenseLine>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var label = item.TryGetProperty("label", out var lab) ? lab.GetString() ?? string.Empty : string.Empty;
                        var amount = 0m;
                        if (item.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var dec))
                        {
                            amount = dec;
                        }

                        parsed.Add(new ErpHrExpenseLine(label, amount));
                    }

                    var fromJson = NormalizeLines(parsed);
                    if (fromJson.Count > 0)
                    {
                        return fromJson;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to form fields.
            }
        }

        if (form is not null)
        {
            var byIndex = new SortedDictionary<int, (string Label, decimal? Amount)>();
            foreach (var key in form.Keys)
            {
                var match = FormLineKey.Match(key);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    continue;
                }

                byIndex.TryGetValue(idx, out var row);
                if (string.Equals(match.Groups[2].Value, "label", StringComparison.OrdinalIgnoreCase))
                {
                    row.Label = form[key].ToString();
                }
                else if (decimal.TryParse(form[key].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                {
                    row.Amount = amt;
                }

                byIndex[idx] = row;
            }

            if (byIndex.Count > 0)
            {
                return NormalizeLines(byIndex.Values.Select(row => new ErpHrExpenseLine(row.Label ?? string.Empty, row.Amount ?? 0)));
            }

            var label = LiveWriteFormBinder.Text(form, "label", "line_label", "lineLabel");
            var amount = LiveWriteFormBinder.Dec(form, "amount", "line_amount", "lineAmount");
            return NormalizeLines([new ErpHrExpenseLine(label, amount)]);
        }

        return [];
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}
