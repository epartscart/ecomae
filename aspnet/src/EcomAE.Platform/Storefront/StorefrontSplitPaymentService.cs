using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Split payment on shop_orders: a configurable advance (default 30%) is collected at
/// checkout; once staff confirm the order — possibly at a reduced total when not every
/// line can be fulfilled — the customer pays the remaining balance through a tokenized
/// link (no login needed; the token is the proof).
/// </summary>
public interface IStorefrontSplitPaymentService
{
    Task EnsureSchemaAsync(DbConnection connection, DbTransaction? tx, CancellationToken cancellationToken);

    /// <summary>Stamp country/currency/advance on the just-created order (inside the checkout tx).</summary>
    Task StampOrderAsync(
        DbConnection connection,
        DbTransaction? tx,
        long orderId,
        decimal orderTotal,
        string countryCode,
        string currencyCode,
        decimal advancePercent,
        CancellationToken cancellationToken);

    Task<StorefrontOrderPayState> ReadStateAsync(DbConnection connection, long orderId, CancellationToken cancellationToken);

    /// <summary>Refresh advance_paid / balance_paid from the accounting ledger after a payment notify.</summary>
    Task RefreshPaidFlagsAsync(DbConnection connection, long orderId, CancellationToken cancellationToken);

    /// <summary>Staff order confirmation: freeze the fulfillable total (partial fulfillment allowed).</summary>
    Task<(bool Ok, string Message)> ConfirmOrderAsync(long orderId, decimal confirmedTotal, CancellationToken cancellationToken = default);

    /// <summary>Token-gated balance link validation — returns state when the token matches.</summary>
    Task<StorefrontOrderPayState?> ReadByTokenAsync(long orderId, string token, CancellationToken cancellationToken = default);

    static string BalancePayPath(long orderId, string token)
        => "/storefront/balance-pay?order=" + orderId.ToString(CultureInfo.InvariantCulture)
           + "&token=" + Uri.EscapeDataString(token);
}

public sealed record StorefrontOrderPayState(
    long OrderId,
    long UserId,
    string CountryCode,
    string CurrencyCode,
    decimal OrderTotal,
    decimal AdvancePercent,
    decimal AdvanceAmount,
    bool AdvancePaid,
    decimal? ConfirmedTotal,
    string BalanceToken,
    bool BalancePaid,
    decimal PaidSum,
    bool SplitEnabled)
{
    public decimal EffectiveTotal => ConfirmedTotal ?? OrderTotal;

    public decimal AdvanceDue => AdvancePaid ? 0m : Math.Min(AdvanceAmount, Math.Max(0m, EffectiveTotal - PaidSum));

    public decimal BalanceDue
    {
        get
        {
            if (!AdvancePaid || BalancePaid)
            {
                return 0m;
            }

            var due = EffectiveTotal - PaidSum;
            return due > 0m ? due : 0m;
        }
    }
}

public sealed class StorefrontSplitPaymentService : IStorefrontSplitPaymentService
{
    /// <summary>Tenants change their required advance in one place.</summary>
    public const decimal DefaultAdvancePercent = 30m;

    private static readonly (string Column, string Ddl)[] OrderColumns =
    [
        ("country_code", "ALTER TABLE `shop_orders` ADD COLUMN `country_code` VARCHAR(2) NOT NULL DEFAULT ''"),
        ("currency_code", "ALTER TABLE `shop_orders` ADD COLUMN `currency_code` VARCHAR(3) NOT NULL DEFAULT ''"),
        ("order_total", "ALTER TABLE `shop_orders` ADD COLUMN `order_total` DECIMAL(18,2) NOT NULL DEFAULT 0"),
        ("advance_percent", "ALTER TABLE `shop_orders` ADD COLUMN `advance_percent` DECIMAL(9,3) NOT NULL DEFAULT 0"),
        ("advance_amount", "ALTER TABLE `shop_orders` ADD COLUMN `advance_amount` DECIMAL(18,2) NOT NULL DEFAULT 0"),
        ("advance_paid", "ALTER TABLE `shop_orders` ADD COLUMN `advance_paid` TINYINT(1) NOT NULL DEFAULT 0"),
        ("confirmed_total", "ALTER TABLE `shop_orders` ADD COLUMN `confirmed_total` DECIMAL(18,2) NULL"),
        ("balance_token", "ALTER TABLE `shop_orders` ADD COLUMN `balance_token` VARCHAR(64) NOT NULL DEFAULT ''"),
        ("balance_paid", "ALTER TABLE `shop_orders` ADD COLUMN `balance_paid` TINYINT(1) NOT NULL DEFAULT 0"),
    ];

    private const string StateSql = """
        SELECT `id`, `user_id`,
               IFNULL(`country_code`, '') AS country_code,
               IFNULL(`currency_code`, '') AS currency_code,
               IFNULL(`order_total`, 0) AS order_total,
               IFNULL(`advance_percent`, 0) AS advance_percent,
               IFNULL(`advance_amount`, 0) AS advance_amount,
               IFNULL(`advance_paid`, 0) AS advance_paid,
               `confirmed_total`,
               IFNULL(`balance_token`, '') AS balance_token,
               IFNULL(`balance_paid`, 0) AS balance_paid
        FROM `shop_orders` WHERE `id` = ? LIMIT 1
        """;

    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontSplitPaymentService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task EnsureSchemaAsync(DbConnection connection, DbTransaction? tx, CancellationToken cancellationToken)
    {
        if (tx is not null)
        {
            // Callers ensure the schema before opening the transaction — an ALTER here would
            // wait on the tx's metadata lock (stall) or implicitly commit it (half order).
            return;
        }

        if (!await ErpDb.TableKnownAsync(connection, "shop_orders", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var (column, ddl) in OrderColumns)
        {
            if (!await ErpDb.ColumnKnownAsync(connection, "shop_orders", column, cancellationToken, tx).ConfigureAwait(false))
            {
                await ErpDb.TryExecuteAsync(connection, ddl, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task StampOrderAsync(
        DbConnection connection,
        DbTransaction? tx,
        long orderId,
        decimal orderTotal,
        string countryCode,
        string currencyCode,
        decimal advancePercent,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var advance = Math.Round(orderTotal * advancePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("""
                UPDATE `shop_orders` SET
                    `country_code` = ?, `currency_code` = ?, `order_total` = ?,
                    `advance_percent` = ?, `advance_amount` = ?, `balance_token` = ?
                WHERE `id` = ?
                """),
            cancellationToken,
            countryCode, currencyCode, orderTotal, advancePercent, advance, token, orderId).ConfigureAwait(false);
    }

    public async Task<StorefrontOrderPayState> ReadStateAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
        StorefrontOrderPayState? state = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(StateSql);
            ErpDb.AddParameters(command, orderId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                state = ReadState(reader);
            }
        }

        if (state is null)
        {
            return new StorefrontOrderPayState(orderId, 0, "", "", 0m, 0m, 0m, false, null, "", false, 0m, false);
        }

        var paidSum = await PaidSumAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        return state with { PaidSum = paidSum };
    }

    public async Task RefreshPaidFlagsAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var paidSum = await PaidSumAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                UPDATE `shop_orders` SET
                    `advance_paid` = IF(`advance_amount` > 0 AND ? >= `advance_amount`, 1, `advance_paid`),
                    `balance_paid` = IF(`advance_paid` = 1 AND ? >= IFNULL(`confirmed_total`, `order_total`) AND `order_total` > 0, 1, `balance_paid`)
                WHERE `id` = ?
                """),
            cancellationToken,
            paidSum, paidSum, orderId).ConfigureAwait(false);
    }

    public async Task<(bool Ok, string Message)> ConfirmOrderAsync(long orderId, decimal confirmedTotal, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return (false, "Order is required.");
        }

        if (confirmedTotal < 0m)
        {
            return (false, "Confirmed total cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return (false, "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
            var rows = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_orders` SET `confirmed_total` = ? WHERE `id` = ?"),
                cancellationToken,
                confirmedTotal, orderId).ConfigureAwait(false);
            return rows > 0 ? (true, "Confirmed total saved — the balance payment link is ready to send.")
                            : (false, "Order not found.");
        }
        catch (DbException ex)
        {
            return (false, "Confirm failed: " + ex.Message);
        }
    }

    public async Task<StorefrontOrderPayState?> ReadByTokenAsync(long orderId, string token, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(token) || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var state = await ReadStateAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            return state.BalanceToken.Length > 0
                   && string.Equals(state.BalanceToken, token.Trim(), StringComparison.Ordinal)
                ? state
                : null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    internal static StorefrontOrderPayState ReadState(DbDataReader reader)
        => new(
            Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["user_id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["country_code"], CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader["currency_code"], CultureInfo.InvariantCulture) ?? "",
            Convert.ToDecimal(reader["order_total"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(reader["advance_percent"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(reader["advance_amount"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["advance_paid"], CultureInfo.InvariantCulture) == 1,
            reader["confirmed_total"] is DBNull ? null : Convert.ToDecimal(reader["confirmed_total"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["balance_token"], CultureInfo.InvariantCulture) ?? "",
            Convert.ToInt32(reader["balance_paid"], CultureInfo.InvariantCulture) == 1,
            0m,
            SplitEnabled: true);

    /// <summary>Order total from items — same formula the payment service applies.</summary>
    internal static async Task<decimal> OrderSumAsync(DbConnection connection, long orderId, CancellationToken cancellationToken, DbTransaction? tx = null)
    {
        try
        {
            return await ErpDb.DecimalAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "SELECT CAST(SUM(`price`*`count_need`) AS DECIMAL(18,2)) FROM `shop_orders_items` WHERE `order_id`=? AND `status` NOT IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag`=0)"),
                cancellationToken,
                orderId).ConfigureAwait(false);
        }
        catch
        {
            return await ErpDb.DecimalAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT CAST(SUM(`price`*`count_need`) AS DECIMAL(18,2)) FROM `shop_orders_items` WHERE `order_id`=?"),
                cancellationToken,
                orderId).ConfigureAwait(false);
        }
    }

    /// <summary>Paid ledger total against the order (income=0 minus income=1, like the payment service).</summary>
    private static Task<decimal> PaidSumAsync(DbConnection connection, long orderId, CancellationToken cancellationToken, DbTransaction? tx = null)
        => ErpDb.DecimalAsync(
            connection,
            tx,
            ErpDb.Positional(
                "SELECT CAST((IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?),0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=1 AND `order_id`=?),0)) AS DECIMAL(18,2))"),
            cancellationToken,
            orderId, orderId);
}
