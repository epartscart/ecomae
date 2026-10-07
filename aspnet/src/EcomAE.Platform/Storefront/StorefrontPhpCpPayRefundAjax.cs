using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string RefundAccountingMissing = "Refund accounting is not in this database.";

    public static Task<object> OrderPayRefundAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        bool postedOrder,
        string? orderText,
        bool postedDirect,
        string? directText,
        bool postedPaid,
        string? paidText,
        ICpOmsWriteService orders,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (adminId, token) => PayRefundBodyAsync(
                connection, adminId, postedOrder, orderText, postedDirect, directText, postedPaid, paidText, orders, token),
            cancellationToken);

    private static async Task<object> PayRefundBodyAsync(
        DbConnection connection,
        int adminId,
        bool postedOrder,
        string? orderText,
        bool postedDirect,
        string? directText,
        bool postedPaid,
        string? paidText,
        ICpOmsWriteService orders,
        CancellationToken cancellationToken)
    {
        if (!postedOrder || !postedDirect)
        {
            return new FlagBody(false, "Forbidden");
        }

        var orderId = ParseId(orderText);
        var directRaw = (directText ?? string.Empty).Trim();
        var direct = directRaw.Length > 0 && !string.Equals(directRaw, "0", StringComparison.Ordinal);
        decimal? paidOverride = null;
        if (postedPaid && decimal.TryParse(paidText, NumberStyles.Number, CultureInfo.InvariantCulture, out var paidSum) && paidSum > 0)
        {
            paidOverride = paidSum;
        }

        try
        {
            var officeId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            if (officeId <= 0 && orderId > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    orderId).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return new FlagBody(false, "Forbidden");
                }
            }

            if (orderId > 0)
            {
                var userId = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `user_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    orderId).ConfigureAwait(false);
                if (userId == 0 && !string.Equals(directRaw, "1", StringComparison.Ordinal))
                {
                    return new FlagBody(false, "Forbidden");
                }

                var allowed = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ?"),
                    cancellationToken,
                    officeId,
                    "%\"" + adminId.ToString(CultureInfo.InvariantCulture) + "\"%").ConfigureAwait(false);
                if (allowed == 0)
                {
                    return new FlagBody(false, "Forbidden");
                }
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, ex.Message.Contains("shop_offices", StringComparison.OrdinalIgnoreCase) ? OfficesMissing : OrdersMissing);
        }

        try
        {
            var written = await orders.PayRefundAsync(orderId, direct, paidOverride, adminId, cancellationToken).ConfigureAwait(false);
            return written.Succeeded
                ? new FlagBody(true, string.Empty)
                : new FlagBody(false, written.Message);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, RefundAccountingMissing);
        }
    }
}
