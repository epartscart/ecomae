using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Cp;

/// <summary>
/// ASP.NET owner of the PHP <c>epc_currency_live_rates.php</c> module: live FX preview/apply
/// against the main shop currency and the nightly auto-update schedule (get / tick).
/// Shop rate model: <c>foreign_price * rate = amount in main currency</c>.
/// </summary>
public interface ICpCurrencyLiveRatesService
{
    /// <summary>Main shop currency as (iso_code from config_items.shop_currency, ISO alpha).</summary>
    Task<(string IsoCode, string Alpha)> GetMainCurrencyAsync(CancellationToken cancellationToken = default);

    Task<CpCurrencyLivePreview> PreviewAsync(CancellationToken cancellationToken = default);

    Task<CpCurrencyLiveApplyResult> ApplyAsync(IReadOnlyCollection<string>? onlyIsoCodes, CancellationToken cancellationToken = default);

    Task<CpCurrencyFxSchedule> GetScheduleAsync(CancellationToken cancellationToken = default);

    Task<CpCurrencyFxTickResult> TickAsync(bool force, CancellationToken cancellationToken = default);
}

public sealed record CpCurrencyLivePreviewRow(
    int Id,
    string IsoCode,
    string IsoName,
    string Caption,
    bool Available,
    bool IsMain,
    decimal CurrentRate,
    decimal? LiveRate,
    decimal? DiffPct);

public sealed record CpCurrencyLivePreview(
    bool Ok,
    string Error,
    string BaseIsoCode,
    string BaseAlpha,
    string Provider,
    string AsOf,
    long FetchedAt,
    IReadOnlyList<CpCurrencyLivePreviewRow> Rows);

public sealed record CpCurrencyLiveApplyResult(
    bool Ok,
    string Error,
    int Updated,
    int Skipped,
    string Provider,
    string AsOf);

public sealed record CpCurrencyFxSchedule(
    bool Enabled,
    string Timezone,
    int Hour,
    long LastRunAt,
    string LastStatus,
    string LastProvider,
    string LastMessage,
    string LocalNow,
    string LocalDate,
    bool Due,
    string NextWindow);

public sealed record CpCurrencyFxTickResult(
    bool Ok,
    bool Ran,
    bool Skipped,
    string Reason,
    int Updated,
    string Provider,
    string AsOf,
    string Error,
    CpCurrencyFxSchedule Schedule);

public sealed record CpCurrencyFxBundle(bool Ok, string Base, string Date, string Provider, IReadOnlyDictionary<string, decimal> Rates, string Error);

public sealed class CpCurrencyLiveRatesService : ICpCurrencyLiveRatesService
{
    public const string HttpClientName = nameof(CpCurrencyLiveRatesService);
    private const string DefaultTimezone = "Asia/Dubai";

    private static readonly (string Name, string UrlFormat, string Parser)[] Providers =
    [
        ("ExchangeRate-API (open.er-api.com)", "https://open.er-api.com/v6/latest/{0}", "erapi_v6"),
        ("ExchangeRate-API v4", "https://api.exchangerate-api.com/v4/latest/{0}", "erapi_v4"),
        ("FloatRates", "https://www.floatrates.com/daily/{1}.json", "floatrates"),
    ];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CpCurrencyLiveRatesService> _logger;

    public CpCurrencyLiveRatesService(
        IErpWriteConnectionFactory connections,
        IHttpClientFactory http,
        ILogger<CpCurrencyLiveRatesService> logger)
    {
        _connections = connections;
        _http = http;
        _logger = logger;
    }

    public async Task<(string IsoCode, string Alpha)> GetMainCurrencyAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ("", "AED");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var iso = await ReadShopCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
        return (iso, await ResolveMainAlphaAsync(connection, iso, cancellationToken).ConfigureAwait(false));
    }

    public async Task<CpCurrencyLivePreview> PreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Failed("TenantRegistry DB is not configured.", "", "");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await PreviewAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CpCurrencyLiveApplyResult> ApplyAsync(IReadOnlyCollection<string>? onlyIsoCodes, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new CpCurrencyLiveApplyResult(false, "TenantRegistry DB is not configured.", 0, 0, "", "");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ApplyAsync(connection, onlyIsoCodes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CpCurrencyFxSchedule> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return BuildSchedule(true, DefaultTimezone, 2, 0, "", "", "", DateTimeOffset.UtcNow);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await GetScheduleAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CpCurrencyFxTickResult> TickAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            var empty = BuildSchedule(true, DefaultTimezone, 2, 0, "", "", "", DateTimeOffset.UtcNow);
            return new CpCurrencyFxTickResult(false, false, true, "db", 0, "", "", "TenantRegistry DB is not configured.", empty);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var schedule = await GetScheduleAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!force)
        {
            if (!schedule.Enabled)
            {
                return new CpCurrencyFxTickResult(true, false, true, "disabled", 0, "", "", "", schedule);
            }

            if (!schedule.Due)
            {
                return new CpCurrencyFxTickResult(true, false, true, "not_due", 0, "", "", "", schedule);
            }
        }

        var applied = await ApplyAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        await SetSettingAsync(connection, "fx_live_auto_last_run_at", now, cancellationToken).ConfigureAwait(false);
        if (!applied.Ok)
        {
            await SetSettingAsync(connection, "fx_live_auto_last_status", "failed", cancellationToken).ConfigureAwait(false);
            await SetSettingAsync(connection, "fx_live_auto_last_provider", "", cancellationToken).ConfigureAwait(false);
            await SetSettingAsync(connection, "fx_live_auto_last_message", applied.Error, cancellationToken).ConfigureAwait(false);
            schedule = await GetScheduleAsync(connection, cancellationToken).ConfigureAwait(false);
            return new CpCurrencyFxTickResult(false, true, false, force ? "forced" : "due", 0, "", "", applied.Error, schedule);
        }

        var message = $"Updated {applied.Updated} currency rate(s); skipped {applied.Skipped}.";
        await SetSettingAsync(connection, "fx_live_auto_last_status", "ok", cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(connection, "fx_live_auto_last_provider", applied.Provider, cancellationToken).ConfigureAwait(false);
        await SetSettingAsync(connection, "fx_live_auto_last_message", message, cancellationToken).ConfigureAwait(false);
        schedule = await GetScheduleAsync(connection, cancellationToken).ConfigureAwait(false);
        return new CpCurrencyFxTickResult(true, true, false, force ? "forced" : "due", applied.Updated, applied.Provider, applied.AsOf, "", schedule);
    }

    // ---- preview / apply -------------------------------------------------------------------

    private async Task<CpCurrencyLivePreview> PreviewAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var baseIso = await ReadShopCurrencyAsync(connection, cancellationToken).ConfigureAwait(false);
        var baseAlpha = await ResolveMainAlphaAsync(connection, baseIso, cancellationToken).ConfigureAwait(false);
        var bundle = await FetchBundleAsync(baseAlpha, cancellationToken).ConfigureAwait(false);
        if (!bundle.Ok)
        {
            return Failed(bundle.Error, baseIso, baseAlpha);
        }

        var shopRates = ToShopRates(bundle.Rates, baseAlpha);
        var rows = new List<CpCurrencyLivePreviewRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `iso_code`, `iso_name`, `caption_short`, `rate`, `available` FROM `shop_currencies` ORDER BY `order`, `iso_name`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var iso = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                var alpha = (reader.IsDBNull(2) ? "" : reader.GetString(2)).Trim().ToUpperInvariant();
                var caption = reader.IsDBNull(3) ? alpha : reader.GetString(3);
                var current = reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
                var available = !reader.IsDBNull(5) && Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) == 1;
                var isMain = (baseIso.Length > 0 && iso == baseIso) || (alpha.Length > 0 && alpha == baseAlpha);
                decimal? live = null;
                if (isMain)
                {
                    live = 1m;
                }
                else if (alpha.Length > 0 && shopRates.TryGetValue(alpha, out var shopRate))
                {
                    live = shopRate;
                }

                rows.Add(new CpCurrencyLivePreviewRow(
                    reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    iso,
                    alpha,
                    caption,
                    available,
                    isMain,
                    current,
                    live,
                    DiffPct(current, live)));
            }
        }

        return new CpCurrencyLivePreview(true, "", baseIso, baseAlpha, bundle.Provider, bundle.Date, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), rows);
    }

    private async Task<CpCurrencyLiveApplyResult> ApplyAsync(DbConnection connection, IReadOnlyCollection<string>? onlyIsoCodes, CancellationToken cancellationToken)
    {
        var preview = await PreviewAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!preview.Ok)
        {
            return new CpCurrencyLiveApplyResult(false, preview.Error, 0, 0, "", "");
        }

        HashSet<string>? allow = onlyIsoCodes is { Count: > 0 }
            ? new HashSet<string>(onlyIsoCodes.Select(c => c.Trim()).Where(c => c.Length > 0), StringComparer.Ordinal)
            : null;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var updated = 0;
        var skipped = 0;
        foreach (var row in preview.Rows)
        {
            if (allow is not null && !allow.Contains(row.IsoCode))
            {
                skipped++;
                continue;
            }

            decimal rate;
            if (row.IsMain)
            {
                rate = 1m;
            }
            else if (row.LiveRate is null)
            {
                skipped++;
                continue;
            }
            else
            {
                rate = row.LiveRate.Value;
            }

            if (rate <= 0)
            {
                skipped++;
                continue;
            }

            rate = decimal.Round(rate, 6, MidpointRounding.AwayFromZero);
            var written = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_currencies` SET `rate` = ?, `rate_source` = ?, `rate_updated_at` = ? WHERE `iso_code` = ?"),
                cancellationToken,
                rate.ToString(CultureInfo.InvariantCulture), preview.Provider, now, row.IsoCode).ConfigureAwait(false);
            if (written > 0)
            {
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        return new CpCurrencyLiveApplyResult(true, "", updated, skipped, preview.Provider, preview.AsOf);
    }

    private async Task<CpCurrencyFxBundle> FetchBundleAsync(string baseAlpha, CancellationToken cancellationToken)
    {
        var alpha = NormalizeAlpha(baseAlpha);
        if (alpha.Length != 3)
        {
            return new CpCurrencyFxBundle(false, "", "", "", new Dictionary<string, decimal>(), "Invalid base currency");
        }

        var errors = new List<string>();
        var client = _http.CreateClient(HttpClientName);
        foreach (var (name, urlFormat, parser) in Providers)
        {
            var url = string.Format(CultureInfo.InvariantCulture, urlFormat, Uri.EscapeDataString(alpha), alpha.ToLowerInvariant());
            string body;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("User-Agent", "EPartsCart-CurrencyLive/1.0");
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{name}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Live FX provider {Provider} failed", name);
                errors.Add($"{name}: {ex.Message}");
                continue;
            }

            var parsed = ParseProvider(parser, alpha, body);
            if (!parsed.Ok)
            {
                errors.Add($"{name}: {parsed.Error}");
                continue;
            }

            return parsed with { Provider = name };
        }

        return new CpCurrencyFxBundle(false, alpha, "", "", new Dictionary<string, decimal>(), "All FX providers failed. " + string.Join(" | ", errors));
    }

    /// <summary>Normalise provider JSON into "foreign units per 1 base" keyed by ISO alpha.</summary>
    public static CpCurrencyFxBundle ParseProvider(string kind, string baseAlpha, string json)
    {
        var alpha = NormalizeAlpha(baseAlpha);
        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal) { [alpha] = 1m };
        var date = "";
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new CpCurrencyFxBundle(false, alpha, "", "", rates, "invalid JSON");
        }

        using (doc)
        {
            var root = doc.RootElement;
            switch (kind)
            {
                case "erapi_v6":
                {
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("result", out var result) || result.GetString() != "success"
                        || !root.TryGetProperty("rates", out var v6) || v6.ValueKind != JsonValueKind.Object)
                    {
                        return new CpCurrencyFxBundle(false, alpha, "", "", rates, "unexpected payload");
                    }

                    if (root.TryGetProperty("time_last_update_utc", out var utc))
                    {
                        date = utc.ToString();
                    }
                    else if (root.TryGetProperty("time_last_update_unix", out var unix))
                    {
                        date = unix.ToString();
                    }

                    AddRates(rates, v6);
                    break;
                }

                case "erapi_v4":
                {
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("rates", out var v4) || v4.ValueKind != JsonValueKind.Object)
                    {
                        return new CpCurrencyFxBundle(false, alpha, "", "", rates, "unexpected payload");
                    }

                    if (root.TryGetProperty("date", out var d))
                    {
                        date = d.ToString();
                    }

                    AddRates(rates, v4);
                    break;
                }

                case "floatrates":
                {
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return new CpCurrencyFxBundle(false, alpha, "", "", rates, "unexpected payload");
                    }

                    foreach (var entry in root.EnumerateObject())
                    {
                        var row = entry.Value;
                        if (row.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var code = "";
                        if (row.TryGetProperty("code", out var c))
                        {
                            code = c.GetString() ?? "";
                        }
                        else if (row.TryGetProperty("alphaCode", out var ac))
                        {
                            code = ac.GetString() ?? "";
                        }

                        code = code.ToUpperInvariant();
                        if (code.Length == 0 || !row.TryGetProperty("rate", out var r) || !TryDecimal(r, out var rate) || rate <= 0)
                        {
                            continue;
                        }

                        rates[code] = rate;
                        if (date.Length == 0 && row.TryGetProperty("date", out var rd))
                        {
                            date = rd.ToString();
                        }
                    }

                    rates[alpha] = 1m;
                    break;
                }

                default:
                    return new CpCurrencyFxBundle(false, alpha, "", "", rates, "unknown parser");
            }
        }

        if (rates.Count < 2)
        {
            return new CpCurrencyFxBundle(false, alpha, "", "", rates, "no rates in response");
        }

        return new CpCurrencyFxBundle(true, alpha, date, "", rates, "");
    }

    /// <summary>Convert API "foreign per 1 base" into shop rates "base per 1 foreign" (main = 1).</summary>
    public static IReadOnlyDictionary<string, decimal> ToShopRates(IReadOnlyDictionary<string, decimal> apiRates, string baseAlpha)
    {
        var alpha = NormalizeAlpha(baseAlpha);
        var out_ = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (rawCode, foreignPerBase) in apiRates)
        {
            var code = rawCode.ToUpperInvariant();
            if (code == alpha)
            {
                out_[code] = 1m;
                continue;
            }

            if (foreignPerBase <= 0)
            {
                continue;
            }

            out_[code] = decimal.Round(1m / foreignPerBase, 6, MidpointRounding.AwayFromZero);
        }

        out_[alpha] = 1m;
        return out_;
    }

    public static decimal? DiffPct(decimal current, decimal? live)
        => live is null || current <= 0 ? null : decimal.Round((live.Value - current) / current * 100m, 3, MidpointRounding.AwayFromZero);

    // ---- schedule --------------------------------------------------------------------------

    private async Task<CpCurrencyFxSchedule> GetScheduleAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await EnsureSettingsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        var enabled = await GetSettingAsync(connection, "fx_live_auto_enabled", "1", cancellationToken).ConfigureAwait(false) == "1";
        var tz = CpCurrencyWriteService.NormalizeTimezone(await GetSettingAsync(connection, "fx_live_auto_timezone", DefaultTimezone, cancellationToken).ConfigureAwait(false));
        var hourText = await GetSettingAsync(connection, "fx_live_auto_hour", "2", cancellationToken).ConfigureAwait(false);
        var hour = int.TryParse(hourText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 23 ? h : 2;
        var lastRunText = await GetSettingAsync(connection, "fx_live_auto_last_run_at", "0", cancellationToken).ConfigureAwait(false);
        var lastRun = long.TryParse(lastRunText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lr) ? lr : 0;
        var lastStatus = await GetSettingAsync(connection, "fx_live_auto_last_status", "", cancellationToken).ConfigureAwait(false);
        var lastProvider = await GetSettingAsync(connection, "fx_live_auto_last_provider", "", cancellationToken).ConfigureAwait(false);
        var lastMessage = await GetSettingAsync(connection, "fx_live_auto_last_message", "", cancellationToken).ConfigureAwait(false);
        return BuildSchedule(enabled, tz, hour, lastRun, lastStatus, lastProvider, lastMessage, DateTimeOffset.UtcNow);
    }

    /// <summary>PHP <c>epc_currency_live_schedule_get</c> due/next-window rules: once per local day from the configured hour onward.</summary>
    public static CpCurrencyFxSchedule BuildSchedule(
        bool enabled,
        string timezone,
        int hour,
        long lastRunAt,
        string lastStatus,
        string lastProvider,
        string lastMessage,
        DateTimeOffset utcNow)
    {
        var tz = CpCurrencyWriteService.NormalizeTimezone(timezone);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(tz, out var zone))
        {
            tz = DefaultTimezone;
            zone = TimeZoneInfo.TryFindSystemTimeZoneById(tz, out var dubai) ? dubai : TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTime(utcNow, zone);
        var localDate = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var alreadyToday = lastRunAt > 0
            && TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(lastRunAt), zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) == localDate;
        var due = enabled && !alreadyToday && localNow.Hour >= hour;
        var slot = hour.ToString("00", CultureInfo.InvariantCulture) + ":00 " + tz;
        string nextWindow;
        if (due)
        {
            nextWindow = "due now (" + localDate + " " + slot + ")";
        }
        else if (localNow.Hour < hour)
        {
            nextWindow = localDate + " " + slot;
        }
        else
        {
            nextWindow = localNow.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + slot;
        }

        return new CpCurrencyFxSchedule(
            enabled,
            tz,
            hour,
            lastRunAt,
            lastStatus,
            lastProvider,
            lastMessage,
            localNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            localDate,
            due,
            nextWindow);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static CpCurrencyLivePreview Failed(string error, string baseIso, string baseAlpha)
        => new(false, error, baseIso, baseAlpha, "", "", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), []);

    private static void AddRates(Dictionary<string, decimal> rates, JsonElement obj)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            var code = prop.Name.ToUpperInvariant();
            if (code.Length == 0 || !TryDecimal(prop.Value, out var value) || value <= 0)
            {
                continue;
            }

            rates[code] = value;
        }
    }

    private static bool TryDecimal(JsonElement element, out decimal value)
    {
        value = 0m;
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetDecimal(out value))
            {
                return true;
            }

            if (element.TryGetDouble(out var d) && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                value = (decimal)d;
                return true;
            }

            return false;
        }

        return element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static string NormalizeAlpha(string? raw)
    {
        var text = (raw ?? "").ToUpperInvariant();
        var chars = text.Where(ch => ch is >= 'A' and <= 'Z').ToArray();
        return new string(chars);
    }

    private static async Task<string> ReadShopCurrencyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"), cancellationToken, "shop_currency").ConfigureAwait(false);
            return (value ?? "").Trim();
        }
        catch (DbException)
        {
            return "";
        }
    }

    private static async Task<string> ResolveMainAlphaAsync(DbConnection connection, string shopCurrency, CancellationToken cancellationToken)
    {
        if (shopCurrency.Length == 0)
        {
            return "AED";
        }

        var alpha = NormalizeAlpha(shopCurrency);
        if (alpha.Length == 3 && alpha.Length == shopCurrency.Length)
        {
            return alpha;
        }

        try
        {
            var name = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `iso_name` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1"), cancellationToken, shopCurrency).ConfigureAwait(false);
            var resolved = (name ?? "").Trim().ToUpperInvariant();
            return resolved.Length > 0 ? resolved : "AED";
        }
        catch (DbException)
        {
            return "AED";
        }
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SHOW COLUMNS FROM `shop_currencies`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                columns.Add(reader.GetString(0));
            }
        }
        catch (DbException)
        {
            return;
        }

        if (!columns.Contains("rate_source"))
        {
            await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `shop_currencies` ADD COLUMN `rate_source` VARCHAR(64) NOT NULL DEFAULT '' AFTER `rate`", cancellationToken).ConfigureAwait(false);
        }

        if (!columns.Contains("rate_updated_at"))
        {
            await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `shop_currencies` ADD COLUMN `rate_updated_at` INT UNSIGNED NOT NULL DEFAULT 0 AFTER `rate_source`", cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task EnsureSettingsTableAsync(DbConnection connection, CancellationToken cancellationToken)
        => ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_price_settings` (`setting_key` varchar(128) NOT NULL, `setting_value` text, PRIMARY KEY (`setting_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken);

    private static async Task<string> GetSettingAsync(DbConnection connection, string key, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), cancellationToken, key).ConfigureAwait(false);
            return value ?? fallback;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static async Task SetSettingAsync(DbConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        await EnsureSettingsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            key, value).ConfigureAwait(false);
    }
}
