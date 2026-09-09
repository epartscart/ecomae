using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>currencies_turning.php</c> rate / available-flag UPDATE twins
/// and <c>epc_currency_live_schedule_save</c> UPSERT of nightly FX schedule keys.
/// Live FX pull / apply / run-now stay Classic. This service does not invent a send.
/// </summary>
public interface ICpCurrencyWriteService
{
    Task<ErpSimpleWriteResult> SetRateAsync(string? isoCode, decimal rate, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetAvailableAsync(
        string? isoCodes,
        int available,
        string? shopCurrency,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveScheduleAsync(
        int enabled,
        string? timezone,
        int hour,
        CancellationToken cancellationToken = default);
}

public sealed class CpCurrencyWriteService : ICpCurrencyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCurrencyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetRateAsync(string? isoCode, decimal rate, CancellationToken cancellationToken = default)
    {
        var iso = NormalizeIso(isoCode);
        if (iso.Length != 3)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A 3-letter ISO currency code is required.");
        }

        if (rate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rate greater than zero is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var money = decimal.Round(rate, 6, MidpointRounding.AwayFromZero);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_currencies` SET `rate` = ? WHERE `iso_code` = ?"),
            cancellationToken,
            money.ToString(CultureInfo.InvariantCulture), iso);
        return ErpSimpleWriteResult.Ok("Currency rate saved.", 0);
    }

    public async Task<ErpSimpleWriteResult> SetAvailableAsync(
        string? isoCodes,
        int available,
        string? shopCurrency,
        CancellationToken cancellationToken = default)
    {
        if (available is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Available must be 0 or 1.");
        }

        var codes = ParseIsoList(isoCodes);
        if (codes.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one ISO currency code is required.");
        }

        var shop = NormalizeCurrencyCode(shopCurrency);
        if (shop.Length > 0 && codes.Contains(shop, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("shop", "The shop currency cannot be marked unavailable.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (shop.Length == 0)
        {
            shop = await ReadShopCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
            if (shop.Length > 0 && codes.Contains(shop, StringComparer.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("shop", "The shop currency cannot be marked unavailable.");
            }
        }

        var placeholders = string.Join(",", codes.Select(_ => "?"));
        var args = new object?[codes.Count + 1];
        args[0] = available;
        for (var i = 0; i < codes.Count; i++)
        {
            args[i + 1] = codes[i];
        }

        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_currencies` SET `available` = ? WHERE `iso_code` IN (" + placeholders + ")"),
            cancellationToken,
            args).ConfigureAwait(false);
        return new ErpSimpleWriteResult(
            true,
            "ok",
            available == 1 ? "Currencies marked available." : "Currencies marked unavailable.",
            writes,
            writes);
    }

    public async Task<ErpSimpleWriteResult> SaveScheduleAsync(
        int enabled,
        string? timezone,
        int hour,
        CancellationToken cancellationToken = default)
    {
        var tz = NormalizeTimezone(timezone);
        if (!IsValidTimezone(tz))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid timezone");
        }

        if (hour < 0 || hour > 23)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Hour must be 0–23");
        }

        var flag = enabled == 1 ? 1 : 0;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await UpsertSettingAsync(connection, "fx_live_auto_enabled", flag.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            await UpsertSettingAsync(connection, "fx_live_auto_timezone", tz, cancellationToken).ConfigureAwait(false);
            await UpsertSettingAsync(connection, "fx_live_auto_hour", hour.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "FX schedule saved.", 0, 3);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Price settings table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP empty timezone falls back to <c>Asia/Dubai</c>.</summary>
    public static string NormalizeTimezone(string? raw)
    {
        var tz = (raw ?? string.Empty).Trim();
        return tz.Length == 0 ? "Asia/Dubai" : tz.Length > 64 ? tz[..64] : tz;
    }

    /// <summary>PHP <c>date_default_timezone_set</c> accept/reject for IANA ids.</summary>
    public static bool IsValidTimezone(string? timezone)
    {
        var tz = (timezone ?? string.Empty).Trim();
        if (tz.Length == 0)
        {
            return false;
        }

        try
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _);
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static Task<int> UpsertSettingAsync(
        DbConnection connection,
        string key,
        string value,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            key, value);

    public static IReadOnlyList<string> ParseIsoList(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        if (text.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var fromJson = new List<string>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var code = item.ValueKind switch
                        {
                            JsonValueKind.Number => item.GetRawText(),
                            JsonValueKind.String => item.GetString(),
                            _ => null
                        };
                        var iso = NormalizeCurrencyCode(code);
                        if (iso.Length > 0 && !fromJson.Contains(iso, StringComparer.Ordinal))
                        {
                            fromJson.Add(iso);
                        }
                    }

                    return fromJson.Count > 64 ? fromJson.Take(64).ToArray() : fromJson;
                }
            }
            catch (JsonException)
            {
            }
        }

        var parsed = new List<string>();
        foreach (var part in text.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var iso = NormalizeCurrencyCode(part);
            if (iso.Length > 0 && !parsed.Contains(iso, StringComparer.Ordinal))
            {
                parsed.Add(iso);
            }

            if (parsed.Count >= 64)
            {
                break;
            }
        }

        return parsed;
    }

    public static string NormalizeCurrencyCode(string? isoCode)
    {
        var letter = NormalizeIso(isoCode);
        if (letter.Length == 3)
        {
            return letter;
        }

        var raw = (isoCode ?? string.Empty).Trim();
        if (raw.Length == 3 && raw.All(char.IsDigit))
        {
            return raw;
        }

        return string.Empty;
    }

    private static async Task<string> ReadShopCurrencyAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"),
                cancellationToken,
                "shop_currency");
            return NormalizeCurrencyCode(value);
        }
        catch (System.Data.Common.DbException)
        {
            return string.Empty;
        }
    }

    internal static string NormalizeIso(string? isoCode)
    {
        var raw = (isoCode ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length != 3)
        {
            return string.Empty;
        }

        foreach (var ch in raw)
        {
            if (ch is < 'A' or > 'Z')
            {
                return string.Empty;
            }
        }

        return raw;
    }
}
