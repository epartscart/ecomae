using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>plugins/authentication/plugin.php</c> guest session + checkout cookies
/// (<c>products_in_cart</c>, <c>created_order</c>).
/// </summary>
public interface IStorefrontGuestSessionService
{
    Task<StorefrontShopper> ResolveAsync(
        HttpContext context,
        bool createIfMissing,
        CancellationToken cancellationToken = default);

    void ApplyGuestCookies(HttpResponse response, StorefrontShopper shopper);

    void ApplyCheckoutCookies(HttpResponse response, long orderId);

    void AppendProductsInCartCookie(HttpRequest request, HttpResponse response, long cartId);
}

public sealed record StorefrontShopper(
    int UserId,
    long SessionRecordId,
    string SessionToken,
    bool IssuedGuestCookies)
{
    public bool IsSignedIn => UserId > 0;

    public bool HasGuestCart => UserId <= 0 && SessionRecordId > 0;

    public bool CanShop => IsSignedIn || HasGuestCart;
}

public sealed class StorefrontGuestSessionService : IStorefrontGuestSessionService
{
    public const string ProductsInCartCookie = "products_in_cart";
    public const string CreatedOrderCookie = "created_order";
    public const int GuestCookieSeconds = 9_999_999;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IOptions<EcomAeOptions> _options;

    public StorefrontGuestSessionService(
        IErpWriteConnectionFactory connections,
        IOptions<EcomAeOptions> options)
    {
        _connections = connections;
        _options = options;
    }

    public static string NormalizeContact(string? value)
        => (value ?? string.Empty).Trim();

    public static bool GuestEmailLooksValid(string? email)
    {
        var value = NormalizeContact(email);
        if (value.Length == 0)
        {
            return true;
        }

        return value.Contains('@', StringComparison.Ordinal)
               && value.IndexOf('@', StringComparison.Ordinal) > 0
               && value.IndexOf('@', StringComparison.Ordinal) < value.Length - 1
               && !value.Contains(' ', StringComparison.Ordinal);
    }

    public static bool GuestPhoneHasDigits(string? phone)
    {
        var digits = 0;
        foreach (var ch in NormalizeContact(phone))
        {
            if (char.IsDigit(ch))
            {
                digits++;
            }
        }

        return digits >= 6;
    }

    public static bool ContactMatchesRegexp(string contact, string? regexp)
    {
        if (string.IsNullOrWhiteSpace(regexp))
        {
            return true;
        }

        try
        {
            var match = Regex.Match(contact, regexp);
            return match.Success && string.Equals(match.Value, contact, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    public static string HtmlEntities(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    public static string GuestSessionToken(string secretSuccession, string remoteIp, long unixTime)
    {
        var nonce = RandomNumberGenerator.GetInt32(1000, 1_000_000_001);
        return LegacyPasswordVerifier.Md5Hex(
            unixTime.ToString(CultureInfo.InvariantCulture)
            + nonce.ToString(CultureInfo.InvariantCulture)
            + (secretSuccession ?? string.Empty)
            + (remoteIp ?? string.Empty));
    }

    public async Task<StorefrontShopper> ResolveAsync(
        HttpContext context,
        bool createIfMissing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var token = context.Request.Cookies["session"] ?? string.Empty;
        var userId = 0;
        if (int.TryParse(context.Request.Cookies["u_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            userId = parsed;
        }

        if (userId > 0 && token.Length > 0)
        {
            return new(userId, 0, token, false);
        }

        if (!_connections.IsConfigured)
        {
            return new(0, 0, token, false);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (token.Length > 0)
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `sessions` WHERE `session` = ? AND `user_id` = 0 LIMIT 1"),
                cancellationToken,
                token);
            if (existing > 0)
            {
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `sessions` SET `last_activiti_time` = ? WHERE `id` = ? AND `user_id` = 0"),
                        cancellationToken,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        existing);
                }
                catch (Exception)
                {
                    // Activity stamp is best-effort.
                }

                return new(0, existing, token, false);
            }
        }

        if (!createIfMissing)
        {
            return new(0, 0, token, false);
        }

        var secret = _options.Value.SecretSuccession ?? string.Empty;
        if (secret.Length == 0)
        {
            return new(0, 0, token, false);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ip = LegacySessionTokenFactory.ResolveClientIp(context.Request) ?? string.Empty;
        var ua = context.Request.Headers.UserAgent.ToString();
        var minted = GuestSessionToken(secret, ip, now);
        var csrf = LegacySessionTokenFactory.CsrfGuardKey(secret, minted, ip, ua);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`)
                VALUES (?, 0, ?, '', ?, ?)
                """),
            cancellationToken,
            minted, now, now, csrf);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return id > 0
            ? new(0, id, minted, true)
            : new(0, 0, token, false);
    }

    public void ApplyGuestCookies(HttpResponse response, StorefrontShopper shopper)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!shopper.IssuedGuestCookies || shopper.SessionToken.Length == 0)
        {
            return;
        }

        var expires = DateTimeOffset.UtcNow.AddSeconds(GuestCookieSeconds);
        response.Cookies.Append("session", shopper.SessionToken, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Expires = expires,
            IsEssential = true
        });
        response.Cookies.Append("u_id", "0", new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Expires = expires,
            IsEssential = true
        });
    }

    public void ApplyCheckoutCookies(HttpResponse response, long orderId)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(ProductsInCartCookie, new CookieOptions { Path = "/" });
        if (orderId <= 0)
        {
            return;
        }

        response.Cookies.Append(CreatedOrderCookie, orderId.ToString(CultureInfo.InvariantCulture), new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
            Expires = DateTimeOffset.UtcNow.AddSeconds(GuestCookieSeconds),
            IsEssential = true
        });
    }

    public void AppendProductsInCartCookie(HttpRequest request, HttpResponse response, long cartId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (cartId <= 0)
        {
            return;
        }

        var ids = new List<long>();
        var raw = request.Cookies[ProductsInCartCookie];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<JsonElement>>(raw);
                if (parsed is not null)
                {
                    foreach (var item in parsed)
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var id) && id > 0)
                        {
                            ids.Add(id);
                        }
                        else if (item.ValueKind == JsonValueKind.String
                                 && long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromText)
                                 && fromText > 0)
                        {
                            ids.Add(fromText);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                ids.Clear();
            }
        }

        if (!ids.Contains(cartId))
        {
            ids.Add(cartId);
        }

        response.Cookies.Append(
            ProductsInCartCookie,
            JsonSerializer.Serialize(ids),
            new CookieOptions
            {
                Path = "/",
                HttpOnly = false,
                Expires = DateTimeOffset.UtcNow.AddSeconds(GuestCookieSeconds),
                IsEssential = true
            });
    }
}
