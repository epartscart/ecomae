using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>A settlement row for PHP <c>epc_pay_accounts_create_settlement</c>.</summary>
public sealed record StorefrontPaymentSettlement(
    long OperationId,
    long OrderId,
    long AccountId,
    string OwnerType,
    long OwnerId,
    string Handler,
    decimal GrossAmount,
    decimal PlatformFeePct,
    string Currency,
    string Status,
    string Note);

/// <summary>One line group of PHP <c>epc_pay_accounts_order_splits</c>.</summary>
public sealed record StorefrontPaymentSplit(long StorageId, long VendorId, decimal Amount, IReadOnlyDictionary<string, string?>? Account);

/// <summary>
/// The storefront runtime of PHP <c>content/shop/payments/epc_payment_accounts.php</c> (get, find for owner, resolve for
/// order, credentials, order splits, settlements) and <c>content/shop/finance/get_pay_system_parameters.php</c>.
/// Account rows are PDO-style string maps.
/// </summary>
public static class StorefrontPaymentAccounts
{
    public static Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => CpPaymentsWriteService.EnsureAccountSchemaAsync(connection, cancellationToken);

    /// <summary>PHP <c>epc_pay_accounts_get</c>.</summary>
    public static async Task<IReadOnlyDictionary<string, string?>?> GetAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await RowAsync(connection, ErpDb.Positional("SELECT * FROM `epc_payment_accounts` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_pay_accounts_find_for_owner</c>: the default (else newest) active account of the owner.</summary>
    public static async Task<IReadOnlyDictionary<string, string?>?> FindForOwnerAsync(DbConnection connection, string ownerType, long ownerId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await RowAsync(
            connection,
            ErpDb.Positional(
                "SELECT * FROM `epc_payment_accounts` WHERE `owner_type` = ? AND `owner_id` = ? AND `status` = 'active' ORDER BY `is_default` DESC, `id` DESC LIMIT 1"),
            cancellationToken,
            ownerType,
            ownerId).ConfigureAwait(false);
    }

    /// <summary>
    /// PHP <c>epc_pay_accounts_resolve_for_order</c>: the vendor of the order's largest storage, then the office account,
    /// then the office's legacy <c>pay_system_*</c> as a virtual account (id 0), then the platform default.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string?>?> ResolveForOrderAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (orderId <= 0)
        {
            return await FindForOwnerAsync(connection, "platform", 0, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var line = await RowAsync(
                connection,
                ErpDb.Positional(
                    "SELECT `t2_storage_id` AS sid, SUM(`price` * `count_need`) AS amt FROM `shop_orders_items` WHERE `order_id` = ? AND `t2_storage_id` > 0 GROUP BY `t2_storage_id` ORDER BY amt DESC LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            var storageId = Int(line?["sid"]);
            if (storageId > 0)
            {
                var vendorId = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `epc_vendor_accounts` WHERE `storage_id` = ? AND `status` IN ('approved','active') LIMIT 1"),
                    cancellationToken,
                    storageId).ConfigureAwait(false);
                if (vendorId > 0)
                {
                    var account = await FindForOwnerAsync(connection, "vendor", vendorId, cancellationToken).ConfigureAwait(false);
                    if (account is not null)
                    {
                        return account;
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        try
        {
            var officeId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"), cancellationToken, orderId)
                .ConfigureAwait(false);
            if (officeId > 0)
            {
                var account = await FindForOwnerAsync(connection, "office", officeId, cancellationToken).ConfigureAwait(false);
                if (account is not null)
                {
                    return account;
                }

                var office = await RowAsync(
                    connection,
                    ErpDb.Positional("SELECT `id`, `caption`, `pay_system_id`, `pay_system_parameters` FROM `shop_offices` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    officeId).ConfigureAwait(false);
                var paySystemId = Int(office?["pay_system_id"]);
                if (office is not null && paySystemId > 0)
                {
                    var handler = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `handler` FROM `shop_payment_systems` WHERE `id` = ? LIMIT 1"), cancellationToken, paySystemId)
                        .ConfigureAwait(false) ?? string.Empty;
                    return new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["id"] = "0",
                        ["owner_type"] = "office",
                        ["owner_id"] = officeId.ToString(CultureInfo.InvariantCulture),
                        ["title"] = office["caption"] ?? string.Empty,
                        ["handler"] = handler,
                        ["pay_system_id"] = paySystemId.ToString(CultureInfo.InvariantCulture),
                        ["mode"] = "direct",
                        ["credentials"] = office["pay_system_parameters"] ?? string.Empty,
                        ["platform_fee_pct"] = "0",
                        ["demo_mode"] = "1",
                        ["status"] = "active",
                        ["_virtual"] = "1",
                    };
                }
            }
        }
        catch (DbException)
        {
        }

        return await FindForOwnerAsync(connection, "platform", 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_pay_accounts_credentials_array</c>: the credentials JSON plus the account demo flag and connected account.</summary>
    public static Dictionary<string, string> CredentialsArray(IReadOnlyDictionary<string, string?> account)
    {
        var credentials = JsonStrings(account.TryGetValue("credentials", out var raw) ? raw : null);
        if (!credentials.ContainsKey("demo_mode") && account.TryGetValue("demo_mode", out var demo) && demo is not null)
        {
            credentials["demo_mode"] = ShopPayForOrderService.PhpIntCast(demo).ToString(CultureInfo.InvariantCulture);
        }

        if (account.TryGetValue("connected_account_id", out var connected) && ShopPayForOrderService.PhpTruthy(connected))
        {
            credentials["connected_account_id"] = connected!;
            credentials["stripe_account"] = connected!;
        }

        return credentials;
    }

    /// <summary>The pay-system parameters, the individual account used (if any) and the handler after PHP's fallback.</summary>
    public sealed record PaySystemParameters(IReadOnlyDictionary<string, string> Values, IReadOnlyDictionary<string, string?>? Account, string Handler);

    /// <summary>
    /// PHP <c>get_pay_system_parameters.php</c>: a wholesaler reads its office's <c>pay_system_parameters</c>; otherwise the
    /// operation's individual account (linked, else resolved for its order) when active, else the enabled pay system of
    /// the handler, else the active pay system.
    /// </summary>
    public static async Task<PaySystemParameters> ParametersAsync(DbConnection connection, long operationId, string? handler, bool wholesaler, CancellationToken cancellationToken)
    {
        var hint = StorefrontPaymentWriteService.SanitizeHandler(handler);
        if (wholesaler)
        {
            var raw = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `pay_system_parameters` FROM `shop_offices` WHERE `id` = (SELECT `office_id` FROM `shop_users_accounting` WHERE `id` = ?);"),
                cancellationToken,
                operationId).ConfigureAwait(false);
            return new PaySystemParameters(JsonStrings(raw), null, hint);
        }

        IReadOnlyDictionary<string, string>? values = null;
        IReadOnlyDictionary<string, string?>? used = null;
        try
        {
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            long accountId = 0;
            try
            {
                var op = await RowAsync(
                    connection,
                    ErpDb.Positional("SELECT `epc_payment_account_id`, `pay_orders`, `office_id` FROM `shop_users_accounting` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    operationId).ConfigureAwait(false);
                if (op is not null)
                {
                    accountId = Int(op["epc_payment_account_id"]);
                    if (accountId <= 0)
                    {
                        var resolved = await ResolveForOrderAsync(connection, Int(op["pay_orders"]), cancellationToken).ConfigureAwait(false);
                        if (resolved is not null && Int(resolved["id"]) > 0)
                        {
                            accountId = Int(resolved["id"]);
                        }
                        else if (resolved is not null)
                        {
                            var credentials = CredentialsArray(resolved);
                            if (credentials.Count > 0)
                            {
                                values = credentials;
                            }

                            used = resolved;
                        }
                    }
                }
            }
            catch (DbException)
            {
                accountId = 0;
            }

            if (accountId > 0)
            {
                var account = await GetAsync(connection, accountId, cancellationToken).ConfigureAwait(false);
                if (account is not null && account.TryGetValue("status", out var status) && status == "active")
                {
                    values = CredentialsArray(account);
                    used = account;
                    if (hint.Length == 0)
                    {
                        hint = account.TryGetValue("handler", out var accountHandler) ? accountHandler ?? string.Empty : string.Empty;
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        if (values is null)
        {
            IReadOnlyDictionary<string, string?>? system = null;
            if (hint.Length > 0)
            {
                system = await RowAsync(connection, ErpDb.Positional("SELECT * FROM `shop_payment_systems` WHERE `handler` = ? AND `anable` = 1 LIMIT 1;"), cancellationToken, hint)
                    .ConfigureAwait(false);
            }

            system ??= await RowAsync(connection, ErpDb.Positional("SELECT * FROM `shop_payment_systems` WHERE `active`= ?;"), cancellationToken, 1).ConfigureAwait(false);
            values = JsonStrings(system is not null && system.TryGetValue("parameters_values", out var parameters) ? parameters ?? "{}" : "{}");
        }

        return new PaySystemParameters(values, used, hint);
    }

    /// <summary>PHP <c>epc_pay_accounts_order_splits</c>: order lines grouped by storage with the storage's vendor and its account.</summary>
    public static async Task<IReadOnlyList<StorefrontPaymentSplit>> OrderSplitsAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var splits = new List<StorefrontPaymentSplit>();
        try
        {
            var groups = new List<(long StorageId, decimal Amount)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `t2_storage_id` AS sid, SUM(`price` * `count_need`) AS amt FROM `shop_orders_items` WHERE `order_id` = ? GROUP BY `t2_storage_id`");
                ErpDb.AddParameters(command, orderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add((
                        reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.IsDBNull(1) ? 0m : Round2(Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture))));
                }
            }

            foreach (var (storageId, amount) in groups)
            {
                long vendorId = 0;
                if (storageId > 0)
                {
                    vendorId = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `id` FROM `epc_vendor_accounts` WHERE `storage_id` = ? LIMIT 1"),
                        cancellationToken,
                        storageId).ConfigureAwait(false);
                }

                var account = vendorId > 0 ? await FindForOwnerAsync(connection, "vendor", vendorId, cancellationToken).ConfigureAwait(false) : null;
                splits.Add(new StorefrontPaymentSplit(storageId, vendorId, amount, account));
            }
        }
        catch (DbException)
        {
        }

        return splits;
    }

    /// <summary>PHP <c>epc_pay_accounts_create_settlement</c>: fee = gross × platform fee %, net = gross − fee.</summary>
    public static async Task<long> CreateSettlementAsync(DbConnection connection, StorefrontPaymentSettlement settlement, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var gross = Round2(settlement.GrossAmount);
        var feePct = Round2(settlement.PlatformFeePct);
        var fee = Round2(gross * (feePct / 100m));
        var net = Round2(gross - fee);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_payment_settlements` (`operation_id`,`order_id`,`account_id`,`owner_type`,`owner_id`,`handler`,`gross_amount`,`fee_amount`,`net_amount`,`currency`,`status`,`note`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            settlement.OperationId,
            settlement.OrderId,
            settlement.AccountId,
            settlement.OwnerType,
            settlement.OwnerId,
            settlement.Handler,
            gross,
            fee,
            net,
            settlement.Currency,
            settlement.Status,
            settlement.Note,
            now,
            now).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A JSON object as PHP-style scalar strings (<c>true</c> → "1", <c>false</c>/<c>null</c> → ""); anything else is empty.</summary>
    public static Dictionary<string, string> JsonStrings(string? raw)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return values;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.True => "1",
                    JsonValueKind.False or JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
        }

        return values;
    }

    internal static long Int(string? raw) => ShopPayForOrderService.PhpIntCast(raw);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static async Task<IReadOnlyDictionary<string, string?>?> RowAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : ErpDocumentControlRender.PdoString(reader, i);
        }

        return row;
    }
}
