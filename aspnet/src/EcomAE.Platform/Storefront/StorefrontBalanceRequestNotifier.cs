using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Sends the customer the "order confirmed — pay your balance" e-mail once an order has a
/// confirmed total and an outstanding balance (the second half of the 30%-advance flow).
/// Fired by staff confirm and by vendor-side confirmation; safe to call repeatedly — it
/// no-ops while the order has nothing due.
/// </summary>
public interface IStorefrontBalanceRequestNotifier
{
    Task<bool> SendAsync(long orderId, CancellationToken cancellationToken = default);
}

public sealed class StorefrontBalanceRequestNotifier : IStorefrontBalanceRequestNotifier
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IStorefrontSplitPaymentService _split;
    private readonly ICpPlatformMailer _mailer;

    public StorefrontBalanceRequestNotifier(
        IErpWriteConnectionFactory connections,
        IStorefrontSplitPaymentService split,
        ICpPlatformMailer mailer)
    {
        _connections = connections;
        _split = split;
        _mailer = mailer;
    }

    public async Task<bool> SendAsync(long orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || !_connections.IsConfigured)
        {
            return false;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var state = await _split.ReadStateAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        if (state is null
            || !state.ConfirmedTotal.HasValue
            || state.BalancePaid
            || state.BalanceDue <= 0m
            || string.IsNullOrEmpty(state.BalanceToken))
        {
            return false;
        }

        var email = state.UserId > 0
            ? await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken, state.UserId).ConfigureAwait(false)
            : await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `email_not_auth` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken, orderId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var host = await ErpDb.StringAsync(
            connection, null,
            "SELECT `host` FROM `epc_portal_site_settings` WHERE `host` <> '' ORDER BY `id` LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        var link = IStorefrontSplitPaymentService.BalancePayPath(orderId, state.BalanceToken);
        var url = string.IsNullOrWhiteSpace(host) ? link : "https://" + host.Trim().TrimEnd('/') + link;

        var amount = state.BalanceDue.ToString("0.00", CultureInfo.InvariantCulture);
        var currency = WebUtility.HtmlEncode(state.CurrencyCode ?? string.Empty);
        var subject = "Order #" + orderId.ToString(CultureInfo.InvariantCulture) + " confirmed — balance payment";
        var body = new StringBuilder(512)
            .Append("<div style=\"font-family:sans-serif;max-width:560px\">")
            .Append("<h2 style=\"margin:0 0 12px\">Your order has been confirmed</h2>")
            .Append("<p>Order <b>#").Append(orderId.ToString(CultureInfo.InvariantCulture)).Append("</b> is confirmed by our vendor.")
            .Append(" Please pay the remaining balance to proceed with fulfilment.</p>")
            .Append("<p style=\"font-size:18px\">Balance due: <b>").Append(amount).Append(' ').Append(currency).Append("</b></p>")
            .Append("<p><a href=\"").Append(url).Append("\" style=\"display:inline-block;background:#1a56db;color:#fff;padding:10px 22px;border-radius:6px;text-decoration:none\">Pay balance now</a></p>")
            .Append("<p style=\"color:#666;font-size:12px\">If the button does not work, open this link: ").Append(WebUtility.HtmlEncode(url)).Append("</p>")
            .Append("</div>")
            .ToString();

        var outcome = await _mailer.SendHtmlAsync(email.Trim(), subject, body, cancellationToken).ConfigureAwait(false);
        return outcome.Ok;
    }
}
