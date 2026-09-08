using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_uae_ct_save_adjustments</c> / ajax <c>uae_tax_save_ct_adjustments</c> twin.
/// UPSERT <c>epc_uae_ct_adjustments</c>. Schema ensure, FTA fetch, ask, and regen stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpUaeTaxSaveCtAdjustmentsWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpUaeTaxSaveCtAdjustmentsWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUaeTaxSaveCtAdjustmentsWriteRequest(
    string? DateFrom = null,
    string? DateTo = null,
    IReadOnlyDictionary<string, decimal>? Amounts = null,
    IReadOnlyDictionary<string, string>? Notes = null);

public sealed class ErpUaeTaxSaveCtAdjustmentsWriteService : IErpUaeTaxSaveCtAdjustmentsWriteService
{
    public static readonly (string Key, string Direction)[] Fields =
    [
        ("non_deductible_entertainment", "add"),
        ("fines_penalties", "add"),
        ("book_depreciation_excess", "add"),
        ("related_party_adjustments", "add"),
        ("other_add_backs", "add"),
        ("exempt_income", "deduct"),
        ("foreign_branch_exemption", "deduct"),
        ("loss_carryforward", "deduct"),
        ("qualifying_donations", "deduct"),
        ("other_deductions", "deduct"),
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpUaeTaxSaveCtAdjustmentsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpUaeTaxSaveCtAdjustmentsWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryPeriodKey(request.DateFrom, request.DateTo, out var periodKey))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid period dates");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_uae_ct_adjustments", "period_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "CT adjustments table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var amounts = request.Amounts ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
        var notes = request.Notes ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, direction) in Fields)
        {
            var amt = RoundAmount(amounts.TryGetValue(key, out var raw) ? raw : 0);
            var note = notes.TryGetValue(key, out var n) ? n.Trim() : "";
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_uae_ct_adjustments` (`period_key`, `adjustment_key`, `direction`, `amount`, `notes`, `time_updated`) VALUES (?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `amount` = VALUES(`amount`), `notes` = VALUES(`notes`), `time_updated` = VALUES(`time_updated`)"),
                cancellationToken,
                periodKey,
                key,
                direction,
                amt,
                note,
                now).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Corporate Tax adjustments saved for this period", 0);
    }

    public static bool TryPeriodKey(string? dateFrom, string? dateTo, out string periodKey)
    {
        periodKey = "";
        if (!TryPhpDateYmd(dateFrom, out var from) || !TryPhpDateYmd(dateTo, out var to))
        {
            return false;
        }

        periodKey = from + "_" + to;
        return true;
    }

    public static bool TryPhpDateYmd(string? raw, out string ymd)
    {
        ymd = "";
        var text = (raw ?? "").Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            ymd = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix != 0)
        {
            ymd = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            ymd = parsed.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    public static decimal RoundAmount(decimal value)
        => decimal.Round(Math.Max(0, value), 2, MidpointRounding.AwayFromZero);

    public static Dictionary<string, decimal> CollectAmounts(IFormCollection form)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (key, _) in Fields)
        {
            amounts[key] = LiveWriteFormBinder.Dec(form, "ct_" + key, key);
        }

        return amounts;
    }

    public static Dictionary<string, string> CollectNotes(IFormCollection form)
    {
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, _) in Fields)
        {
            notes[key] = LiveWriteFormBinder.Text(form, "note_" + key, "notes_" + key);
        }

        return notes;
    }

    public static Dictionary<string, decimal> CollectAmounts(JsonElement root)
    {
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        JsonElement bag = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("amounts", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            bag = nested;
        }

        foreach (var (key, _) in Fields)
        {
            amounts[key] = JsonDec(bag, "ct_" + key, key);
        }

        return amounts;
    }

    public static Dictionary<string, string> CollectNotes(JsonElement root)
    {
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        JsonElement bag = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("notes", out var nested) && nested.ValueKind == JsonValueKind.Object)
        {
            bag = nested;
        }

        foreach (var (key, _) in Fields)
        {
            notes[key] = JsonText(bag, "note_" + key, key);
        }

        return notes;
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static decimal JsonDec(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
            {
                return d;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
