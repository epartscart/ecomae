using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/general/get_currency_indicator.php</c>: the shop-currency sign or short caption used next
/// to prices. <c>currency_show_mode</c> <c>no</c> is empty; <c>sign_before</c> / <c>sign_after</c> is the sign;
/// every other mode is <c>caption_short</c>. A missing row leaves the indicator unset, like PHP's warning.
/// Verified against PHP 8.3 by <c>Fixtures/StorefrontFragments/golden.json</c>.
/// The manufacturer and article subqueries live in <c>content/shop/catalogue/cat_lang_general.php</c>
/// (<see cref="StorefrontCatalogueLang"/>).
/// </summary>
public static class StorefrontCurrencyIndicator
{
    public sealed record Result(string? Sign, string? Indicator);

    public static Result From(string? showMode, string? sign, string? captionShort, bool row)
    {
        if (!row)
        {
            return new Result(null, null);
        }

        if (string.Equals(showMode, "no", StringComparison.Ordinal))
        {
            return new Result(sign, string.Empty);
        }

        if (string.Equals(showMode, "sign_before", StringComparison.Ordinal)
            || string.Equals(showMode, "sign_after", StringComparison.Ordinal))
        {
            return new Result(sign, sign);
        }

        return new Result(sign, captionShort);
    }

    public static async Task<Result> LoadAsync(
        DbConnection connection,
        string? shopCurrency,
        string? showMode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `sign`, `caption_short` FROM `shop_currencies` WHERE `iso_code` = @iso LIMIT 1";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@iso";
        parameter.Value = shopCurrency ?? (object)DBNull.Value;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return From(showMode, null, null, false);
        }

        var sign = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
        var caption = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture);
        return From(showMode, sign, caption, true);
    }
}
