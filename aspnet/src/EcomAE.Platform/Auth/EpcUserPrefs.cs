using System.Data.Common;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Per-login display preferences (display currency + CP language) persisted per user so a choice the
/// user made once follows that login everywhere — storefront, CP and ERP shells — and is never
/// re-seeded by host/country defaults. Stored in the tenant DB; failures never break a login.
/// </summary>
public static class EpcUserPrefs
{
    /// <summary>
    /// On a successful login: restore the stored currency/language onto the response cookies; when the
    /// request already carries a (possibly newer) choice, save it so the latest pick wins.
    /// </summary>
    public static async Task ApplyOnLoginAsync(
        ITenantDbConnectionFactory connections,
        HttpRequest request,
        HttpResponse response,
        long userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await ApplyOnLoginAsync(connection, request, response, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Preference storage must never break a login.
        }
    }

    /// <summary>Same as above on an already-open login connection (OTP / OAuth flows).</summary>
    public static async Task ApplyOnLoginAsync(
        DbConnection connection,
        HttpRequest request,
        HttpResponse response,
        long userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return;
        }

        try
        {
            await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

            var storedCurrency = string.Empty;
            var storedLang = string.Empty;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT IFNULL(`currency_iso`,''), IFNULL(`lang_code`,'') FROM `epc_user_prefs` WHERE `user_id` = @userId LIMIT 1";
                AddParameter(command, "@userId", userId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    storedCurrency = reader.GetString(0);
                    storedLang = reader.GetString(1);
                }
            }

            // Only plausible values are persisted: numeric ISO for currencies, a short
            // language token — never arbitrary cookie text.
            var cookieCurrency = request.Cookies["epc_currency"] ?? string.Empty;
            if (cookieCurrency.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(cookieCurrency, "^[0-9]{1,4}$"))
            {
                cookieCurrency = string.Empty;
            }

            var cookieLang = request.Cookies["lang_cp"] ?? string.Empty;
            if (cookieLang.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(cookieLang, "^[a-zA-Z0-9_-]{1,12}$"))
            {
                cookieLang = string.Empty;
            }

            var currency = cookieCurrency.Length > 0 ? cookieCurrency : storedCurrency;
            var lang = cookieLang.Length > 0 ? cookieLang : storedLang;

            if (currency != storedCurrency || lang != storedLang)
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "INSERT INTO `epc_user_prefs` (`user_id`,`currency_iso`,`lang_code`,`updated_at`) VALUES (@userId,@currency,@lang,@now) "
                    + "ON DUPLICATE KEY UPDATE `currency_iso`=VALUES(`currency_iso`),`lang_code`=VALUES(`lang_code`),`updated_at`=VALUES(`updated_at`)";
                AddParameter(command, "@userId", userId);
                AddParameter(command, "@currency", currency);
                AddParameter(command, "@lang", lang);
                AddParameter(command, "@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Pin the resolved values onto this login's cookies: the country middleware treats
            // epc_currency != epc_currency_host as a user pick and never re-seeds it.
            if (currency.Length > 0 && !string.Equals(cookieCurrency, currency, StringComparison.Ordinal))
            {
                var options = new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax, Secure = request.IsHttps };
                response.Cookies.Append("epc_currency", currency, options);
                response.Cookies.Delete("epc_currency_host");
            }

            if (lang.Length > 0 && !string.Equals(cookieLang, lang, StringComparison.Ordinal))
            {
                response.Cookies.Append("lang_cp", lang, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax, Secure = request.IsHttps });
            }
        }
        catch (Exception)
        {
            // Preference storage must never break a login.
        }
    }

    private static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS `epc_user_prefs` ("
            + "`user_id` BIGINT UNSIGNED NOT NULL,"
            + "`currency_iso` VARCHAR(16) NOT NULL DEFAULT '',"
            + "`lang_code` VARCHAR(16) NOT NULL DEFAULT '',"
            + "`updated_at` BIGINT NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`user_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
