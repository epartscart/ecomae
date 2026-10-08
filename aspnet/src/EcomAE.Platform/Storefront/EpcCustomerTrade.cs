using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>A row of PHP <c>epc_trade_pending_customers()</c>.</summary>
public sealed record EpcTradePendingCustomer(
    long UserId,
    string? Email,
    string? Phone,
    string? TimeRegistered,
    string? EmailConfirmed,
    string? CustomerType,
    string? Name,
    string? Surname,
    string? Company);

/// <summary>
/// PHP <c>content/shop/pricing/epc_customer_trade.php</c>: retail or wholesale registration, manager approval, the
/// price profile group and the fixed dealing currency, all kept in <c>users_profiles</c>. Storage errors are ignored,
/// as in PHP.
/// </summary>
public static class EpcCustomerTrade
{
    public const string PendingCheckoutMessage = "Your account is registered. You can browse and add items to the cart, but checkout is available only after a manager approves your retail/wholesale profile and dealing currency.";
    public const string RejectedCheckoutMessage = "Your trade account registration was not approved. Please contact us if you need assistance.";

    public static IReadOnlyList<string> CustomerTypes { get; } = ["retail", "wholesale"];

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string PhpTrim(string value) => value.Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    private static string Digits(string value) => Regex.Replace(value, "[^0-9]", string.Empty);

    /// <summary>PHP 8 <c>strtolower()</c> changes ASCII letters only.</summary>
    private static string AsciiLower(string value) => string.Create(value.Length, value, (span, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            span[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + 32) : source[i];
        }
    });

    /// <summary>PHP <c>epc_trade_profile_get()</c>: the trimmed value, or <paramref name="fallback"/> when there is no row.</summary>
    public static async Task<string> ProfileGetAsync(DbConnection connection, DbTransaction? transaction, long userId, string key, CancellationToken cancellationToken, string fallback = "")
    {
        if (userId <= 0)
        {
            return fallback;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1");
            ErpDb.AddParameters(command, userId, key);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return fallback;
            }

            return reader.IsDBNull(0) ? string.Empty : PhpTrim(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    /// <summary>PHP <c>epc_trade_profile_set()</c>: delete then insert the key.</summary>
    public static async Task ProfileSetAsync(DbConnection connection, DbTransaction? transaction, long userId, string key, string value, CancellationToken cancellationToken)
    {
        if (userId <= 0 || key.Length == 0)
        {
            return;
        }

        try
        {
            await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"), cancellationToken, userId, key)
                .ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
                cancellationToken,
                userId,
                key,
                value).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP <c>epc_trade_profile_delete()</c>.</summary>
    public static async Task ProfileDeleteAsync(DbConnection connection, DbTransaction? transaction, long userId, string key, CancellationToken cancellationToken)
    {
        if (userId <= 0 || key.Length == 0)
        {
            return;
        }

        try
        {
            await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"), cancellationToken, userId, key)
                .ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP <c>epc_trade_approval_status()</c>: no status counts as approved.</summary>
    public static async Task<string> ApprovalStatusAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        var status = await ProfileGetAsync(connection, transaction, userId, "epc_trade_approval_status", cancellationToken).ConfigureAwait(false);
        return status.Length == 0 ? "approved" : status;
    }

    public static async Task<bool> IsPendingAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
        => await ApprovalStatusAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false) == "pending";

    public static async Task<bool> IsApprovedAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
        => await ApprovalStatusAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false) == "approved";

    /// <summary>PHP <c>epc_trade_can_place_order()</c>: guests always, customers once approved.</summary>
    public static async Task<bool> CanPlaceOrderAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
        => userId <= 0 || await IsApprovedAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);

    /// <summary>PHP <c>epc_trade_customer_type_label()</c>.</summary>
    public static string CustomerTypeLabel(string type)
    {
        type = AsciiLower(PhpTrim(type));
        return type switch
        {
            "wholesale" => "Wholesale",
            "retail" => "Retail",
            "" => string.Empty,
            _ => (type[0] is >= 'a' and <= 'z' ? (char)(type[0] - 32) : type[0]) + type[1..],
        };
    }

    /// <summary>PHP <c>epc_trade_normalize_customer_type()</c>: retail, wholesale, or empty.</summary>
    public static string NormalizeCustomerType(string type)
    {
        type = AsciiLower(PhpTrim(type));
        return CustomerTypes.Contains(type) ? type : string.Empty;
    }

    /// <summary>PHP <c>epc_trade_default_retail_currency_iso()</c>: AED (784) when it is available after the supported currencies are ensured.</summary>
    public static async Task<string> DefaultRetailCurrencyIsoAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        const string iso = "784";
        try
        {
            await EpcCurrency.EnsureSupportedAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var count = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `shop_currencies` WHERE `iso_code` = ? AND `available` = 1"), cancellationToken, iso)
                .ConfigureAwait(false);
            if (count > 0)
            {
                return iso;
            }
        }
        catch (DbException)
        {
        }

        return string.Empty;
    }

    /// <summary>
    /// PHP <c>epc_trade_save_registration()</c>: a retail customer is approved at once (AED and the retail price
    /// profile when they exist); a wholesale customer waits for a manager.
    /// </summary>
    public static async Task SaveRegistrationAsync(DbConnection connection, DbTransaction? transaction, long userId, string customerType, CancellationToken cancellationToken)
    {
        customerType = NormalizeCustomerType(customerType);
        if (customerType.Length == 0)
        {
            customerType = "retail";
        }

        await ProfileSetAsync(connection, transaction, userId, "epc_customer_type", customerType, cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_registered_at", Now().ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_currency_change_requested", cancellationToken).ConfigureAwait(false);

        if (customerType == "retail")
        {
            var currency = await DefaultRetailCurrencyIsoAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (currency.Length > 0 && await ApproveCustomerAsync(connection, transaction, userId, currency, "retail", 0, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await ProfileSetAsync(connection, transaction, userId, "epc_trade_approval_status", "approved", cancellationToken).ConfigureAwait(false);
            await AssignPriceProfileAsync(connection, transaction, userId, "retail", cancellationToken).ConfigureAwait(false);
            if (currency.Length > 0)
            {
                await ProfileSetAsync(connection, transaction, userId, "epc_dealing_currency", currency, cancellationToken).ConfigureAwait(false);
            }

            await ProfileSetAsync(connection, transaction, userId, "epc_trade_approved_at", Now().ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            await ProfileDeleteAsync(connection, transaction, userId, "epc_trade_rejection_note", cancellationToken).ConfigureAwait(false);
            return;
        }

        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approval_status", "pending", cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_dealing_currency", cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_trade_approved_at", cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_trade_approved_by", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_trade_user_currency_iso()</c>: the digits of an approved customer's dealing currency.</summary>
    public static async Task<string> UserCurrencyIsoAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !await IsApprovedAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false))
        {
            return string.Empty;
        }

        return Digits(await ProfileGetAsync(connection, transaction, userId, "epc_dealing_currency", cancellationToken).ConfigureAwait(false));
    }

    public static async Task<bool> CurrencyLockedAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
        => (await UserCurrencyIsoAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false)).Length > 0;

    /// <summary>PHP <c>epc_trade_price_profile_group_id()</c>.</summary>
    public static async Task<long> PriceProfileGroupIdAsync(DbConnection connection, DbTransaction? transaction, string profileCode, CancellationToken cancellationToken)
    {
        profileCode = AsciiLower(PhpTrim(profileCode));
        if (profileCode.Length == 0)
        {
            return 0;
        }

        try
        {
            var raw = await ErpDb.StringAsync(connection, transaction, ErpDb.Positional("SELECT `group_id` FROM `epc_price_profiles` WHERE `code` = ? LIMIT 1"), cancellationToken, profileCode)
                .ConfigureAwait(false);
            return (long)StorefrontPhpAjax.PhpFloatCast(raw);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    /// <summary>PHP <c>epc_trade_assign_price_profile()</c>: the customer leaves every price profile group and joins this one.</summary>
    public static async Task<bool> AssignPriceProfileAsync(DbConnection connection, DbTransaction? transaction, long userId, string profileCode, CancellationToken cancellationToken)
    {
        var groupId = await PriceProfileGroupIdAsync(connection, transaction, profileCode, cancellationToken).ConfigureAwait(false);
        if (userId <= 0 || groupId <= 0)
        {
            return false;
        }

        try
        {
            var profileGroups = new List<object?>();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT `group_id` FROM `epc_price_profiles`";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    profileGroups.Add(reader.IsDBNull(0) ? null : reader.GetValue(0));
                }
            }

            if (profileGroups.Count > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (" + string.Join(",", profileGroups.Select(_ => "?")) + ")"),
                    cancellationToken,
                    new object?[] { userId }.Concat(profileGroups).ToArray()).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"), cancellationToken, userId, groupId)
                .ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>PHP <c>epc_trade_approve_customer()</c>.</summary>
    public static async Task<bool> ApproveCustomerAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long userId,
        string currencyIso,
        string profileCode,
        long adminId,
        CancellationToken cancellationToken)
    {
        currencyIso = Digits(currencyIso);
        var normalized = NormalizeCustomerType(profileCode);
        profileCode = normalized.Length > 0 ? normalized : AsciiLower(PhpTrim(profileCode));
        if (userId <= 0 || currencyIso.Length == 0 || profileCode.Length == 0)
        {
            return false;
        }

        if (!await AssignPriceProfileAsync(connection, transaction, userId, profileCode, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approval_status", "approved", cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_dealing_currency", currencyIso, cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approved_at", Now().ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approved_by", adminId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_currency_change_requested", cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_currency_change_requested_iso", cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_trade_rejection_note", cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>PHP <c>epc_trade_reject_customer()</c>.</summary>
    public static async Task RejectCustomerAsync(DbConnection connection, DbTransaction? transaction, long userId, string note, long adminId, CancellationToken cancellationToken)
    {
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approval_status", "rejected", cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_rejection_note", PhpTrim(note), cancellationToken).ConfigureAwait(false);
        await ProfileSetAsync(connection, transaction, userId, "epc_trade_approved_by", adminId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await ProfileDeleteAsync(connection, transaction, userId, "epc_dealing_currency", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_trade_request_currency_change()</c>: only for an approved customer.</summary>
    public static async Task RequestCurrencyChangeAsync(DbConnection connection, DbTransaction? transaction, long userId, string requestedIso, string note, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !await IsApprovedAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        requestedIso = Digits(requestedIso);
        await ProfileSetAsync(connection, transaction, userId, "epc_currency_change_requested", "1", cancellationToken).ConfigureAwait(false);
        if (requestedIso.Length > 0)
        {
            await ProfileSetAsync(connection, transaction, userId, "epc_currency_change_requested_iso", requestedIso, cancellationToken).ConfigureAwait(false);
        }

        if (note.Length > 0)
        {
            await ProfileSetAsync(connection, transaction, userId, "epc_currency_change_note", PhpTrim(note), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>epc_trade_pending_customers()</c>: customers whose approval is pending, newest first.</summary>
    public static async Task<IReadOnlyList<EpcTradePendingCustomer>> PendingCustomersAsync(DbConnection connection, CancellationToken cancellationToken, int limit = 200)
    {
        var rows = new List<EpcTradePendingCustomer>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_registered`, u.`email_confirmed`,
                	MAX(CASE WHEN p.`data_key` = 'epc_customer_type' THEN p.`data_value` END) AS `customer_type`,
                	MAX(CASE WHEN p.`data_key` = 'name' THEN p.`data_value` END) AS `name`,
                	MAX(CASE WHEN p.`data_key` = 'surname' THEN p.`data_value` END) AS `surname`,
                	MAX(CASE WHEN p.`data_key` = 'company' THEN p.`data_value` END) AS `company`
                	FROM `users` u
                	INNER JOIN `users_profiles` ps ON ps.`user_id` = u.`user_id` AND ps.`data_key` = 'epc_trade_approval_status' AND ps.`data_value` = 'pending'
                	LEFT JOIN `users_profiles` p ON p.`user_id` = u.`user_id`
                	GROUP BY u.`user_id`
                	ORDER BY u.`time_registered` DESC
                	LIMIT
                """ + " " + limit.ToString(CultureInfo.InvariantCulture);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string? Text(int i) => reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                rows.Add(new EpcTradePendingCustomer(
                    (long)StorefrontPhpAjax.PhpFloatCast(Text(0)),
                    Text(1),
                    Text(2),
                    Text(3),
                    Text(4),
                    Text(5),
                    Text(6),
                    Text(7),
                    Text(8)));
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }

    /// <summary>PHP <c>epc_trade_currency_options()</c>.</summary>
    public static Task<IReadOnlyList<EpcCurrencyRecord>> CurrencyOptionsAsync(DbConnection connection, string? shopCurrency, CancellationToken cancellationToken)
        => EpcCurrency.RecordsAsync(connection, shopCurrency, cancellationToken);

    /// <summary>PHP <c>epc_trade_notify_customer()</c>: the notification to the customer, with empty order variables unless given.</summary>
    public static async Task NotifyCustomerAsync(
        IStorefrontNotifyDispatcher dispatcher,
        DbConnection connection,
        string notifyName,
        long userId,
        IReadOnlyDictionary<string, string>? extraVars,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return;
        }

        var vars = new Dictionary<string, string>(StringComparer.Ordinal) { ["order_id"] = "0", ["order_text"] = string.Empty };
        foreach (var (key, value) in extraVars ?? new Dictionary<string, string>())
        {
            vars[key] = value;
        }

        await dispatcher.SendAsync(connection, notifyName, vars, [StorefrontNotifyPerson.User((int)userId)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_trade_checkout_block_message()</c>: empty when the customer may order.</summary>
    public static async Task<string> CheckoutBlockMessageAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || await CanPlaceOrderAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false))
        {
            return string.Empty;
        }

        var status = await ApprovalStatusAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        if (status == "pending")
        {
            return PendingCheckoutMessage;
        }

        if (status == "rejected")
        {
            var note = await ProfileGetAsync(connection, transaction, userId, "epc_trade_rejection_note", cancellationToken).ConfigureAwait(false);
            return note.Length > 0 ? RejectedCheckoutMessage + " Note: " + note : RejectedCheckoutMessage;
        }

        return string.Empty;
    }
}
