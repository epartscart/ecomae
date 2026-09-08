using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ccy_set_rate</c> / ajax <c>ccy_set_rate</c> twin.
/// UPSERT <c>epc_ccy_rates</c> on (from_ccy, to_ccy, as_of). Source is always manual.
/// Schema ensure and FX revaluation stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpCcySetRateWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpCcySetRateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCcySetRateWriteRequest(
    string? From = null,
    string? To = null,
    decimal Rate = 0,
    long AsOfUnix = 0);

public sealed class ErpCcySetRateWriteService : IErpCcySetRateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCcySetRateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpCcySetRateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var from = (request.From ?? "").Trim().ToUpperInvariant();
        var to = (request.To ?? "").Trim().ToUpperInvariant();
        if (from.Length == 0 || to.Length == 0 || request.Rate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Provide from, to and a positive rate");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var asOf = request.AsOfUnix > 0 ? request.AsOfUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_ccy_rates", "from_ccy", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Currency rates table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_ccy_rates` (`from_ccy`,`to_ccy`,`rate`,`as_of`,`source`,`time_created`) VALUES (?,?,?,?, 'manual', ?) ON DUPLICATE KEY UPDATE `rate`=VALUES(`rate`), `source`=VALUES(`source`)"),
            cancellationToken,
            from,
            to,
            request.Rate,
            asOf,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(SuccessMessage(from, to, request.Rate, asOf), id);
    }

    public static string SuccessMessage(string from, string to, decimal rate, long asOfUnix)
        => "Rate saved: 1 " + from + " = " + FormatRate(rate) + " " + to + " as of " + FormatYmd(asOfUnix);

    public static string FormatRate(decimal rate)
        => rate.ToString("0.########", CultureInfo.InvariantCulture);

    public static string FormatYmd(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// PHP <c>!empty(as_of) ? strtotime(as_of + ' 12:00:00') : time()</c>.
    /// </summary>
    public static long ResolveAsOfUnix(string? asOf, long asOfUnix)
    {
        var raw = asOf ?? "";
        if (raw.Length > 0 && raw != "0")
        {
            var trimmed = raw.Trim();
            if (DateTime.TryParseExact(
                    trimmed,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var day))
            {
                return new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero)
                    .ToUnixTimeSeconds();
            }

            return 0;
        }

        return asOfUnix > 0 ? asOfUnix : 0;
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
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return 0;
    }

    public static long JsonLong(JsonElement root, params string[] names)
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

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
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
