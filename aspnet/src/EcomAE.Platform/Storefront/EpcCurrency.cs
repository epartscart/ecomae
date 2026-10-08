using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>A row of PHP <c>epc_currency_records()</c>.</summary>
public sealed record EpcCurrencyRecord(string IsoCode, string IsoName, string CaptionShort, string Sign, double Rate);

/// <summary>
/// PHP <c>content/shop/pricing/epc_currency.php</c>: the ten storefront currencies, kept available in
/// <c>shop_currencies</c>, the country-to-currency map, the visitor's currency choice and the amount format.
/// </summary>
public static class EpcCurrency
{
    private sealed record Supported(string IsoName, string CaptionShort, string Sign, double Rate, int Order);

    private static readonly (string Iso, Supported Row)[] SupportedDefaults =
    [
        ("784", new("AED", "AED", "AED", 1, 1)),
        ("840", new("USD", "USD", "$", 3.6725, 2)),
        ("978", new("EUR", "EUR", "€", 4.0, 3)),
        ("586", new("PKR", "PKR", "Rs", 0.0131, 4)),
        ("643", new("RUB", "RUB", "₽", 0.040, 5)),
        ("682", new("SAR", "SAR", "SAR", 0.979, 6)),
        ("414", new("KWD", "KWD", "KWD", 12.0, 7)),
        ("512", new("OMR", "OMR", "OMR", 9.54, 8)),
        ("634", new("QAR", "QAR", "QAR", 1.01, 9)),
        ("48", new("BHD", "BHD", "BHD", 9.74, 10)),
    ];

    /// <summary>PHP <c>epc_currency_country_map()</c>.</summary>
    public static IReadOnlyDictionary<string, string> CountryMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AE"] = "784", ["PK"] = "586", ["US"] = "840", ["GB"] = "840",
        ["DE"] = "978", ["FR"] = "978", ["IT"] = "978", ["ES"] = "978", ["NL"] = "978", ["BE"] = "978", ["AT"] = "978", ["IE"] = "978", ["PT"] = "978", ["FI"] = "978", ["GR"] = "978",
        ["RU"] = "643", ["SA"] = "682", ["KW"] = "414", ["OM"] = "512", ["QA"] = "634", ["BH"] = "48",
    };

    /// <summary>The ISO codes of PHP <c>epc_currency_supported_defaults()</c>, in its order.</summary>
    public static IReadOnlyList<string> SupportedIsoCodes { get; } = SupportedDefaults.Select(s => s.Iso).ToArray();

    /// <summary>PHP <c>epc_currency_ensure_supported()</c>: each supported currency is made available with its caption and sign, or inserted. Errors are ignored per currency.</summary>
    public static async Task EnsureSupportedAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        foreach (var (iso, row) in SupportedDefaults)
        {
            try
            {
                var exists = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `shop_currencies` WHERE `iso_code` = ?"), cancellationToken, iso)
                    .ConfigureAwait(false);
                if (exists > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("UPDATE `shop_currencies` SET `available` = 1, `caption_short` = ?, `sign` = ? WHERE `iso_code` = ?"),
                        cancellationToken,
                        row.CaptionShort,
                        row.Sign,
                        iso).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("INSERT INTO `shop_currencies` (`iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available`, `order`) VALUES (?, ?, ?, ?, ?, 1, ?)"),
                        cancellationToken,
                        iso,
                        row.IsoName,
                        row.CaptionShort,
                        row.Sign,
                        row.Rate,
                        row.Order).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
            }
        }
    }

    /// <summary>PHP <c>epc_currency_records()</c>: the supported rows of <c>shop_currencies</c> (rate 1 when not positive), plus the shop currency when it is missing.</summary>
    public static async Task<IReadOnlyList<EpcCurrencyRecord>> RecordsAsync(DbConnection connection, string? shopCurrency, CancellationToken cancellationToken)
    {
        await EnsureSupportedAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var records = new List<EpcCurrencyRecord>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available` FROM `shop_currencies` WHERE `iso_code` IN ("
                + string.Join(",", SupportedDefaults.Select(_ => "?")) + ") ORDER BY `order`, `iso_name`");
            ErpDb.AddParameters(command, SupportedDefaults.Select(s => (object?)s.Iso).ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string Text(int i) => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                var rate = StorefrontPhpAjax.PhpFloatCast(Text(4));
                var record = new EpcCurrencyRecord(Text(0), Text(1), Text(2), Text(3), rate <= 0 ? 1 : rate);
                var at = records.FindIndex(r => r.IsoCode == record.IsoCode);
                if (at >= 0)
                {
                    records[at] = record;
                }
                else
                {
                    records.Add(record);
                }
            }
        }
        catch (DbException)
        {
        }

        var shop = shopCurrency ?? string.Empty;
        if (!records.Any(r => r.IsoCode == shop))
        {
            var fallback = SupportedDefaults.FirstOrDefault(s => s.Iso == shop).Row;
            records.Add(fallback is not null
                ? new EpcCurrencyRecord(shop, fallback.IsoName, fallback.CaptionShort, fallback.Sign, fallback.Rate)
                : new EpcCurrencyRecord(shop, "AED", "AED", "AED", 1));
        }

        return records;
    }

    /// <summary>
    /// PHP <c>epc_currency_selected_iso()</c>: an approved customer's dealing currency, else the <c>epc_currency</c> cookie,
    /// else the currency of the <c>epc_country</c> cookie, else the shop currency.
    /// </summary>
    public static async Task<string> SelectedIsoAsync(
        DbConnection connection,
        IReadOnlyList<EpcCurrencyRecord> records,
        string? shopCurrency,
        long userId,
        string? currencyCookie,
        string? countryCookie,
        CancellationToken cancellationToken)
    {
        bool Has(string iso) => records.Any(r => r.IsoCode == iso);
        if (userId > 0)
        {
            var fixedIso = await EpcCustomerTrade.UserCurrencyIsoAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
            if (fixedIso.Length > 0 && Has(fixedIso))
            {
                return fixedIso;
            }
        }

        var selected = currencyCookie is null ? string.Empty : Regex.Replace(currencyCookie, "[^0-9]", string.Empty);
        if (selected.Length > 0 && Has(selected))
        {
            return selected;
        }

        var country = countryCookie is null ? string.Empty : Regex.Replace(countryCookie, "[^A-Z]", string.Empty).ToUpperInvariant();
        if (country.Length > 0 && CountryMap.TryGetValue(country, out var mapped) && Has(mapped))
        {
            return mapped;
        }

        return shopCurrency ?? string.Empty;
    }

    /// <summary>PHP <c>epc_currency_format_amount()</c>: the amount divided by the rate, <c>number_format(.., 2, '.', ' ')</c>, with the sign or short name.</summary>
    public static string FormatAmount(double amount, IReadOnlyList<EpcCurrencyRecord> records, string? selectedIso, string mode = "sign_before")
    {
        var record = records.FirstOrDefault(r => r.IsoCode == selectedIso) ?? records[0];
        var rate = record.Rate != 0 ? record.Rate : 1;
        var number = ErpDocumentControlRender.PhpNumberFormat((decimal)(amount / rate)).Replace(",", " ", StringComparison.Ordinal);
        var indicator = mode == "short_name_after" ? record.CaptionShort : record.Sign;
        if (mode == "no")
        {
            return number;
        }

        return mode is "sign_after" or "short_name_after" ? number + " " + indicator : indicator + " " + number;
    }
}
