using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_mcgl_set_rate</c> twin. Revaluation, journal entry, and seed stay PHP.
/// </summary>
public interface IErpMultiCurrencyGlWriteService
{
    Task<ErpSimpleWriteResult> SetRateAsync(
        string? baseCurrency,
        string? targetCurrency,
        decimal rate,
        string? effectiveDate,
        string? source,
        CancellationToken cancellationToken = default);
}

public sealed class ErpMultiCurrencyGlWriteService : IErpMultiCurrencyGlWriteService
{
    /// <summary>PHP <c>epc_mcgl_currencies</c> keys.</summary>
    public static readonly string[] AllowedCurrencies =
    [
        "AED", "USD", "EUR", "GBP", "SAR", "INR", "CNY", "JPY",
        "KWD", "BHD", "OMR", "QAR", "EGP", "TRY", "PKR"
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMultiCurrencyGlWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetRateAsync(
        string? baseCurrency,
        string? targetCurrency,
        decimal rate,
        string? effectiveDate,
        string? source,
        CancellationToken cancellationToken = default)
    {
        var baseIso = NormalizeIso(baseCurrency);
        var targetIso = NormalizeIso(targetCurrency);
        if (baseIso.Length == 0 || !AllowedCurrencies.Contains(baseIso, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A supported 3-letter base currency is required.");
        }

        if (targetIso.Length == 0 || !AllowedCurrencies.Contains(targetIso, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A supported 3-letter target currency is required.");
        }

        if (rate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rate greater than zero is required.");
        }

        var date = NormalizeDate(effectiveDate);
        if (date.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Effective date must be yyyy-MM-dd.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var money = decimal.Round(rate, 8, MidpointRounding.AwayFromZero);
        var inverse = decimal.Round(1m / money, 8, MidpointRounding.AwayFromZero);
        var src = Clip(string.IsNullOrWhiteSpace(source) ? "manual" : source, 64);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_fx_rates` (`base_currency`, `target_currency`, `rate`, `inverse_rate`, `source`, `effective_date`)
                VALUES (?, ?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE `rate` = VALUES(`rate`), `inverse_rate` = VALUES(`inverse_rate`), `source` = VALUES(`source`)
                """),
            cancellationToken,
            baseIso, targetIso, money, inverse, src, date);

        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("""
                SELECT `id` FROM `epc_fx_rates`
                WHERE `base_currency` = ? AND `target_currency` = ? AND `effective_date` = ?
                """),
            cancellationToken,
            baseIso, targetIso, date);

        return ErpSimpleWriteResult.Ok(
            "FX rate " + baseIso + "/" + targetIso + " set to " + money.ToString("0.########", CultureInfo.InvariantCulture) + " on " + date + ".",
            id);
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

    internal static string NormalizeDate(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "manual";
        }

        return text.Length <= max ? text : text[..max];
    }
}
