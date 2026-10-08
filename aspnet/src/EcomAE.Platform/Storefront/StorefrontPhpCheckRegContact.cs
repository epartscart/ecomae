using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/users/check_reg_contact.php</c>: the CSRF check, then whether an e-mail or phone may be used for a
/// new or changed account (developer domains refused, the <c>reg_fields</c> regexp, and no other user with it).
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string CheckRegContactPath = "/content/users/check_reg_contact.php";
    public const string CheckRegContactJsonType = "application/json;charset=utf-8;";

    public sealed record CheckRegContactRequest(
        string? Contact,
        string? ContactType,
        string? CsrfKey,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer,
        string Lang);

    public static async Task<RawHttp> CheckRegContactAsync(DbConnection connection, CheckRegContactRequest request, CancellationToken cancellationToken)
    {
        var csrf = await StopCsrfAsync(
            connection,
            request.CsrfKey,
            request.Session,
            request.UserCookie,
            request.AdminSession,
            request.AdminUser,
            request.Referer,
            CheckRegContactJsonType,
            cancellationToken).ConfigureAwait(false);
        if (csrf is not null)
        {
            return csrf;
        }

        var translator = new StorefrontPhpTranslator(connection, request.Lang);
        var contact = request.Contact ?? string.Empty;
        string column;
        string caption;
        if (request.ContactType == "phone")
        {
            column = "phone";
            caption = await translator.TextAsync(1312, cancellationToken).ConfigureAwait(false);
        }
        else if (request.ContactType == "email")
        {
            column = "email";
            caption = "E-mail";
        }
        else
        {
            return new RawHttp(string.Empty, CheckRegContactJsonType);
        }

        RawHttp Refuse(string? message)
            => new("{\"status\":false,\"message\":" + PhpJsonOrNull(message) + "}", CheckRegContactJsonType);

        if (column == "email" && (contact.Contains("@intask.pro", StringComparison.Ordinal) || contact.Contains("@docpart.ru", StringComparison.Ordinal) || contact.Contains("@docpart.net", StringComparison.Ordinal)))
        {
            return Refuse(await translator.RawAsync("5641", cancellationToken).ConfigureAwait(false));
        }

        var regexp = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?"), cancellationToken, column).ConfigureAwait(false) ?? string.Empty;
        if (regexp.Length > 0 && !PregWholeMatch(regexp, contact))
        {
            return Refuse(await translator.TextAsync(4699, cancellationToken).ConfigureAwait(false) + " " + caption);
        }

        var taken = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `" + column + "` = ?"), cancellationToken, HtmlEntitiesQuotes(contact)).ConfigureAwait(false);
        if (taken != 0)
        {
            return Refuse(await translator.TextAsync(4700, cancellationToken).ConfigureAwait(false) + " " + caption + " " + await translator.TextAsync(4701, cancellationToken).ConfigureAwait(false));
        }

        return new RawHttp("{\"status\":true,\"message\":\"Ok\"}", CheckRegContactJsonType);
    }
}
