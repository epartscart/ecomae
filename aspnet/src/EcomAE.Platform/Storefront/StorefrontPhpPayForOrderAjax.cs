using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string PayForOrderPath = "/content/shop/protocol/pay_for_order.php";

    private const string PayForOrderJsonType = "application/json;charset=utf-8;";

    /// <summary>The query, posted CSRF key (null when not sent), cookies and referer PHP <c>protocol/pay_for_order.php</c> reads.</summary>
    public sealed record PayForOrderProtocolRequest(
        string? Initiator,
        string? OrderId,
        string? PaySum,
        string? DirectPay,
        string? Code,
        string? CsrfKey,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer);

    /// <summary>
    /// PHP <c>content/shop/protocol/pay_for_order.php</c>: an empty body unless <c>initiator</c>, <c>order_id</c>,
    /// <c>pay_sum</c> and <c>direct_pay</c> are all sent with initiator 1/2/3 and direct_pay 0/1; then <c>stop_csrf.php</c>
    /// for the manager and the customer (the admin session when the referer is the control panel), then the engine.
    /// </summary>
    public static async Task<object> PayForOrderProtocolAsync(
        DbConnection connection,
        IShopPayForOrderService engine,
        PayForOrderProtocolRequest request,
        IReadOnlyDictionary<string, string> config,
        string techKey,
        CancellationToken cancellationToken)
    {
        var empty = new RawHttp(string.Empty, PayForOrderJsonType);
        if (request.Initiator is null || request.OrderId is null || request.PaySum is null || request.DirectPay is null)
        {
            return empty;
        }

        var initiator = PhpLooseEquals(request.Initiator, 1) ? 1 : PhpLooseEquals(request.Initiator, 2) ? 2 : PhpLooseEquals(request.Initiator, 3) ? 3 : 0;
        if (initiator == 0 || (!PhpLooseEquals(request.DirectPay, 1) && !PhpLooseEquals(request.DirectPay, 0)))
        {
            return empty;
        }

        if (initiator is 1 or 2)
        {
            var failure = await PayForOrderCsrfAsync(connection, request, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        var adminId = await IsAdminSessionAsync(connection, request.AdminSession, request.AdminUser, cancellationToken).ConfigureAwait(false)
            ? ParseId(request.AdminUser)
            : 0;
        var customerId = await CookieUserIdAsync(connection, request.Session, request.UserCookie, cancellationToken).ConfigureAwait(false);
        var result = await engine.PayAsync(
            connection,
            new ShopPayForOrderRequest(
                initiator,
                request.OrderId,
                request.PaySum,
                request.DirectPay,
                adminId,
                customerId,
                TechKeyMatches(request.Code, techKey),
                ShopPayForOrderConfig.From(config)),
            cancellationToken).ConfigureAwait(false);
        return result.ToJson();
    }

    /// <summary>PHP <c>stop_csrf.php</c> as pay_for_order.php runs it; null when the key matches.</summary>
    private static Task<RawHttp?> PayForOrderCsrfAsync(DbConnection connection, PayForOrderProtocolRequest request, CancellationToken cancellationToken)
        => StopCsrfAsync(
            connection,
            request.CsrfKey,
            request.Session,
            request.UserCookie,
            request.AdminSession,
            request.AdminUser,
            request.Referer,
            PayForOrderJsonType,
            cancellationToken);
}
