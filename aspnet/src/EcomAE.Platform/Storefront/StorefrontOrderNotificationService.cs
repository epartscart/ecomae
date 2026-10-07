using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>epc_checkout_send_order_notifications()</c> (content/shop/usefull/epc_admin_notifications.php) minus
/// its supplier-LPO tail: <c>new_order_to_manager</c> to the admin inbox + CRM manager + office managers with the
/// staff layout (get_order_info_html_epc_staff.php), one admin-only retry, then <c>new_order_to_user</c> to the
/// customer (user id, or the guest e-mail) with the customer layout (get_order_info_html_for_user.php) and one
/// retry. Every outcome lands in <c>shop_orders_logs</c> like PHP.
/// </summary>
public interface IStorefrontOrderNotificationService
{
    Task<StorefrontOrderNotifyResult> SendAsync(long orderId, CancellationToken cancellationToken = default);
}

public sealed record StorefrontOrderNotifyResult(
    string AdminEmail,
    bool AdminSent,
    bool AdminRetried,
    string CustomerLabel,
    bool CustomerSent,
    IReadOnlyList<string> Log);

public sealed class StorefrontOrderNotificationService : IStorefrontOrderNotificationService
{
    public const string ManagerNotification = "new_order_to_manager";
    public const string CustomerNotification = "new_order_to_user";

    private static readonly string[] CrmProfileKeys = ["epc_crm_user_id", "epc_relationship_manager", "relationship_manager_id", "account_manager_id"];
    private static readonly string[] DeliveryAddressKeys = ["address", "delivery_address", "street", "city", "office", "point", "name"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IStorefrontNotifyDispatcher _notify;
    private readonly ICpPlatformMailer _mailer;

    public StorefrontOrderNotificationService(
        IErpWriteConnectionFactory connections,
        IStorefrontNotifyDispatcher notify,
        ICpPlatformMailer mailer)
    {
        _connections = connections;
        _notify = notify;
        _mailer = mailer;
    }

    public async Task<StorefrontOrderNotifyResult> SendAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        if (orderId <= 0)
        {
            return new StorefrontOrderNotifyResult(string.Empty, false, false, string.Empty, false, log);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var config = _mailer.ReadConfig();
        var translator = new StorefrontPhpTranslator(connection);
        var data = await OrderEmailData.LoadAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        var userId = data.Order is null ? 0 : (int)PhpLong(Field(data.Order, "user_id"));
        var officeId = data.Order is null ? 0 : (int)PhpLong(Field(data.Order, "office_id"));
        var guestEmail = data.Order is null ? string.Empty : WebUtility.HtmlDecode(Field(data.Order, "email_not_auth"));
        var guestPhone = data.Order is null ? string.Empty : WebUtility.HtmlDecode(Field(data.Order, "phone_not_auth"));

        var adminEmail = await AdminEmailAsync(connection, config, cancellationToken).ConfigureAwait(false);
        var managerVars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
            ["order_text"] = await BuildStaffHtmlAsync(connection, translator, data, config, cancellationToken).ConfigureAwait(false),
        };

        var staff = await StaffPersonsAsync(connection, adminEmail, userId, officeId, cancellationToken).ConfigureAwait(false);
        var staffAnswer = await _notify.SendAsync(connection, ManagerNotification, managerVars, staff, cancellationToken).ConfigureAwait(false);
        var adminMatch = adminEmail.ToLowerInvariant();
        var adminOk = staffAnswer.EmailStatus(adminMatch) == true;
        var retried = false;
        if (!adminOk)
        {
            retried = true;
            var retry = await _notify.SendAsync(connection, ManagerNotification, managerVars, [StorefrontNotifyPerson.Direct(adminEmail)], cancellationToken)
                .ConfigureAwait(false);
            adminOk = retry.EmailStatus(adminMatch) == true;
            await LogAsync(connection, orderId, "Order email to admin " + adminEmail + ": " + (adminOk ? "sent (retry)" : "FAILED after retry"), log, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await LogAsync(connection, orderId, "Order email to admin " + adminEmail + ": sent", log, cancellationToken).ConfigureAwait(false);
        }

        StorefrontNotifyPerson customer = userId > 0
            ? StorefrontNotifyPerson.User(userId)
            : StorefrontNotifyPerson.Direct(
                StorefrontGuestSessionService.HtmlEntities(guestEmail),
                StorefrontGuestSessionService.HtmlEntities(guestPhone));
        var customerMatch = userId > 0 ? userId.ToString(CultureInfo.InvariantCulture) : guestEmail.Trim().ToLowerInvariant();
        var customerText = await BuildCustomerHtmlAsync(connection, translator, data, config, cancellationToken).ConfigureAwait(false);
        var comment = await FirstCustomerMessageAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        if (comment.Length > 0)
        {
            customerText += "<h4>" + await translator.TextAsync(4509, cancellationToken).ConfigureAwait(false) + "</h4>"
                + "<div style=\"font-family: Calibri; font-size: 14px;\">" + comment.Replace("\n", "<br/>", StringComparison.Ordinal) + "</div>";
        }

        var customerVars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
            ["order_text"] = customerText,
        };
        var customerAnswer = await _notify.SendAsync(connection, CustomerNotification, customerVars, [customer], cancellationToken).ConfigureAwait(false);
        var customerOk = customerAnswer.EmailStatus(customerMatch) == true;
        if (!customerOk)
        {
            var retry = await _notify.SendAsync(connection, CustomerNotification, customerVars, [customer], cancellationToken).ConfigureAwait(false);
            customerOk = retry.EmailStatus(customerMatch) == true;
        }

        var label = userId > 0 ? "user #" + userId.ToString(CultureInfo.InvariantCulture) : guestEmail.Trim();
        await LogAsync(connection, orderId, "Order email to customer (" + label + "): " + (customerOk ? "sent" : "FAILED"), log, cancellationToken)
            .ConfigureAwait(false);
        return new StorefrontOrderNotifyResult(adminEmail, adminOk, retried, label, customerOk, log);
    }

    /// <summary>
    /// PHP <c>epc_admin_notify_email()</c>: the site context <c>admin_email</c> (tenant <c>contact_json</c>
    /// admin_email → from_email → config from_email), else config from_email unless it is a noreply box,
    /// else <c>admin@</c> + the portal host without <c>www.</c>.
    /// </summary>
    public static async Task<string> AdminEmailAsync(DbConnection connection, IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken)
    {
        var configFrom = config.TryGetValue("from_email", out var f) ? f.Trim() : string.Empty;
        var host = HostOf(config.TryGetValue("domain_path", out var d) ? d : string.Empty);
        var contactJson = string.Empty;
        try
        {
            var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
            if (aliases.Count > 0)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT IFNULL(`contact_json`,'') FROM `epc_portal_site_settings` WHERE `host` IN ("
                    + string.Join(", ", aliases.Select((_, i) => "@p" + i.ToString(CultureInfo.InvariantCulture)))
                    + ") ORDER BY `id` ASC LIMIT 1";
                ErpDb.AddParameters(command, aliases.Cast<object?>().ToArray());
                contactJson = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
        catch (DbException)
        {
        }

        return AdminEmail(CpIndustrySettingsService.ParseContact(contactJson, null), configFrom, host);
    }

    public static string AdminEmail(CpIndustrySettingsContact contact, string configFromEmail, string host)
    {
        var from = contact.FromEmail.Length > 0 ? contact.FromEmail : configFromEmail;
        var admin = contact.AdminEmail.Length > 0 ? contact.AdminEmail : from;
        if (admin.Length > 0)
        {
            return admin;
        }

        var email = configFromEmail.Trim();
        if (email.Length == 0 || email.Contains("noreply", StringComparison.OrdinalIgnoreCase))
        {
            email = "admin@" + (host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host);
        }

        return email;
    }

    /// <summary>PHP <c>epc_staff_notify_persons()</c> → <c>epc_notify_merge_persons()</c>: admin first, then CRM and office managers, deduplicated.</summary>
    public static async Task<IReadOnlyList<StorefrontNotifyPerson>> StaffPersonsAsync(
        DbConnection connection,
        string adminEmail,
        int customerId,
        int officeId,
        CancellationToken cancellationToken)
    {
        var persons = new List<StorefrontNotifyPerson> { StorefrontNotifyPerson.Direct(adminEmail) };
        var seen = new HashSet<int>();
        var crm = await CrmUserIdAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        var candidates = new List<int>();
        if (crm > 0)
        {
            candidates.Add(crm);
        }

        candidates.AddRange(await OfficeManagerIdsAsync(connection, officeId, cancellationToken).ConfigureAwait(false));
        foreach (var uid in candidates)
        {
            if (uid > 0 && seen.Add(uid))
            {
                persons.Add(StorefrontNotifyPerson.User(uid));
            }
        }

        return persons;
    }

    /// <summary>PHP <c>epc_crm_user_id_for_customer()</c>: first relationship-manager profile key in priority order.</summary>
    public static async Task<int> CrmUserIdAsync(DbConnection connection, int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            return 0;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` IN (?, ?, ?, ?) "
                + "ORDER BY FIELD(`data_key`, 'epc_crm_user_id','epc_relationship_manager','relationship_manager_id','account_manager_id') LIMIT 1");
            ErpDb.AddParameters(command, [customerId, .. CrmProfileKeys]);
            var value = PhpLong(Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty);
            return value > 0 ? (int)value : 0;
        }
        catch (DbException)
        {
            return 0;
        }
    }

    /// <summary>PHP <c>epc_office_manager_persons()</c>: <c>shop_offices.users</c> ids that belong to a backend group.</summary>
    public static async Task<IReadOnlyList<int>> OfficeManagerIdsAsync(DbConnection connection, int officeId, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        if (officeId <= 0)
        {
            return ids;
        }

        try
        {
            var raw = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `users` FROM `shop_offices` WHERE `id` = ? LIMIT 1"), cancellationToken, officeId)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ids;
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            {
                return ids;
            }

            var listed = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : doc.RootElement.EnumerateObject().Select(p => p.Value).ToList();
            var backend = await BackendGroupIdsAsync(connection, cancellationToken).ConfigureAwait(false);
            foreach (var element in listed)
            {
                var uid = (int)PhpLong(Scalar(element));
                if (uid <= 0)
                {
                    continue;
                }

                if (!await InGroupsAsync(connection, uid, backend, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                ids.Add(uid);
            }
        }
        catch (Exception ex) when (ex is DbException or JsonException)
        {
        }

        return ids;
    }

    /// <summary>PHP <c>DP_User::isBackendGroupById()</c> group set: <c>for_backend = 1</c> roots plus every descendant.</summary>
    public static async Task<IReadOnlyList<long>> BackendGroupIdsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var all = new List<long>();
        var level = await IdsAsync(connection, "SELECT `id` FROM `groups` WHERE `for_backend` = 1", cancellationToken).ConfigureAwait(false);
        while (level.Count > 0)
        {
            var fresh = level.Where(id => !all.Contains(id)).ToList();
            all.AddRange(fresh);
            if (fresh.Count == 0)
            {
                break;
            }

            level = await IdsAsync(
                connection,
                "SELECT `id` FROM `groups` WHERE `parent` IN (" + string.Join(",", fresh.Select(i => i.ToString(CultureInfo.InvariantCulture))) + ")",
                cancellationToken).ConfigureAwait(false);
        }

        return all;
    }

    private static async Task<bool> InGroupsAsync(DbConnection connection, int userId, IReadOnlyList<long> groups, CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return false;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN ("
                + string.Join(",", groups.Select(g => g.ToString(CultureInfo.InvariantCulture))) + ")"),
            cancellationToken,
            userId).ConfigureAwait(false) > 0;
    }

    // ---- get_order_info_html_epc_staff.php ----

    public static async Task<string> BuildStaffHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        OrderEmailData data,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        if (data.Order is null)
        {
            return string.Empty;
        }

        var order = data.Order;
        var orderId = data.OrderId;
        var usdRate = await UsdRateAsync(connection, cancellationToken).ConfigureAwait(false);
        string Dual(decimal aed) => PhpNumber(aed, 2, ",") + " AED<br><span style=\"font-size:11px;color:#555;\">("
            + PhpNumber(aed / usdRate, 2, ",") + " USD)</span>";

        var mainColor = data.Template.MainColor.Length > 0 ? data.Template.MainColor : "#2b78d6";
        var backend = config.TryGetValue("backend_dir", out var b) && b.Trim().Length > 0 ? b.Trim() : "cp";
        var domain = (config.TryGetValue("domain_path", out var dp) ? dp.Trim() : string.Empty).TrimEnd('/');
        var cpOrderUrl = domain + "/" + backend + "/shop/orders/order?order_id=" + orderId.ToString(CultureInfo.InvariantCulture);
        var customerId = (int)PhpLong(Field(order, "user_id"));
        var howGet = PhpLong(Field(order, "how_get"));
        var paidType = PhpLong(Field(order, "paid_type"));
        var statusId = PhpLong(Field(order, "status"));
        var howGetJson = Field(order, "how_get_json");

        var deliveryType = string.Empty;
        if (data.ObtainModes.TryGetValue(howGet, out var mode))
        {
            deliveryType = await translator.TextAsync(mode.Caption, cancellationToken).ConfigureAwait(false);
        }

        var deliveryAddress = DeliveryAddress(howGetJson);
        var paymentLabel = data.PaidTypes.TryGetValue(paidType, out var pt) ? pt : string.Empty;
        var cartLabel = "Cart";
        try
        {
            var name = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `name` FROM `shop_carts` WHERE `user_id` = ? ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                customerId > 0 ? customerId : 0).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(name))
            {
                cartLabel = name;
            }
        }
        catch (DbException)
        {
        }

        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;color:#222;\">\n");
        html.Append("<p style=\"margin:0 0 12px;\">\n\t<a style=\"background:").Append(H(mainColor))
            .Append(";color:#fff;text-decoration:none;padding:8px 14px;border-radius:4px;display:inline-block;font-weight:bold;\"\n\t\thref=\"")
            .Append(H(cpOrderUrl)).Append("\" target=\"_blank\">View order in your Control Panel</a>\n</p>\n");
        html.Append("<p style=\"margin:0 0 16px;\"><strong>New order #").Append(orderId.ToString(CultureInfo.InvariantCulture)).Append("</strong>\n\t· ")
            .Append(StorefrontNotifyDispatcher.PlatformTime(PhpLong(Field(order, "time"))).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
            .Append("\n\t· ").Append(H(data.OrderStatuses.TryGetValue(statusId, out var sn) ? sn : string.Empty)).Append("\n</p>\n\n");
        html.Append(await CustomerProfileHtmlAsync(connection, translator, customerId, order, config, cancellationToken).ConfigureAwait(false));
        html.Append("\n\n<table style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;margin:12px 0 18px;border-collapse:collapse;\">\n");
        if (deliveryAddress.Length > 0)
        {
            html.Append("\t<tr><td style=\"padding:3px 12px 3px 0;font-weight:bold;\">Delivery address:</td><td>").Append(H(deliveryAddress)).Append("</td></tr>\n");
        }

        if (deliveryType.Length > 0)
        {
            html.Append("\t<tr><td style=\"padding:3px 12px 3px 0;font-weight:bold;\">Delivery type:</td><td>").Append(H(deliveryType)).Append("</td></tr>\n");
        }

        if (paymentLabel.Length > 0)
        {
            html.Append("\t<tr><td style=\"padding:3px 12px 3px 0;font-weight:bold;\">Payment method:</td><td>").Append(H(paymentLabel)).Append("</td></tr>\n");
        }

        html.Append("\t<tr><td style=\"padding:3px 12px 3px 0;font-weight:bold;\">Cart:</td><td>").Append(H(cartLabel)).Append("</td></tr>\n</table>\n\n");

        var countTotal = 0m;
        var priceSumTotal = 0m;
        var purchaseTotal = 0m;
        var weightTotal = 0m;
        var rows = new StringBuilder();
        foreach (var item in data.Items)
        {
            var count = Math.Truncate(item.CountNeed);
            var purchaseUnit = count > 0 ? item.PurchaseSum / count : item.T2PricePurchase;
            var warehouse = await WarehouseAsync(connection, data, item, cancellationToken).ConfigureAwait(false);
            var term = item.TimeToExe > 0
                ? (item.TimeToExeGuaranteed > item.TimeToExe
                    ? "from " + item.TimeToExe.ToString(CultureInfo.InvariantCulture) + " to " + item.TimeToExeGuaranteed.ToString(CultureInfo.InvariantCulture) + " days"
                    : item.TimeToExe.ToString(CultureInfo.InvariantCulture) + " days")
                : "—";
            var lineWeight = ProductWeight(item.ProductJson) * count;
            if (!data.NotCountStatuses.Contains(item.Status))
            {
                countTotal += count;
                priceSumTotal += item.PriceSum;
                purchaseTotal += item.PurchaseSum;
                weightTotal += lineWeight;
            }

            const string Td = "<td style=\"border:1px solid #ccc;padding:6px;\">";
            const string TdRight = "<td style=\"border:1px solid #ccc;padding:6px;text-align:right;\">";
            rows.Append("<tr style=\"vertical-align:top;\">")
                .Append(Td).Append(H(warehouse)).Append("</td>")
                .Append(Td).Append(H(item.Manufacturer)).Append("</td>")
                .Append(Td).Append(H(item.Article)).Append("</td>")
                .Append(Td).Append(H(item.Name)).Append("</td>")
                .Append(TdRight).Append(lineWeight > 0 ? PhpNumber(lineWeight, 3, string.Empty) : "—").Append("</td>")
                .Append(Td).Append(H(term)).Append("</td>")
                .Append("<td style=\"border:1px solid #ccc;padding:6px;text-align:center;\">").Append(count.ToString("0", CultureInfo.InvariantCulture)).Append("</td>")
                .Append(TdRight).Append(Dual(item.Price)).Append("</td>")
                .Append(TdRight).Append(Dual(purchaseUnit)).Append("</td>")
                .Append(TdRight).Append(Dual(item.PriceSum)).Append("</td>")
                .Append(TdRight).Append(Dual(item.PurchaseSum)).Append("</td>")
                .Append(Td).Append("</td>")
                .Append("</tr>");
        }

        var totals = await StaffTotalsAsync(connection, data, customerId, howGetJson, priceSumTotal, cancellationToken).ConfigureAwait(false);
        var margin = priceSumTotal - purchaseTotal;
        var total = totals.Gross + (totals.CourierGross > 0 ? totals.CourierGross : totals.CourierNet);
        var comment = await FirstCustomerMessageAsync(connection, orderId, cancellationToken).ConfigureAwait(false);

        html.Append("\n<h4 style=\"font-family:Calibri,Arial,sans-serif;margin:20px 0 8px;\">Additional information for employees</h4>\n");
        html.Append("<div style=\"overflow-x:auto;\">\n<table style=\"font-family:Calibri,Arial,sans-serif;font-size:12px;border-collapse:collapse;width:100%;min-width:900px;\">\n<thead>\n<tr style=\"background:#f0f4f8;\">\n");
        foreach (var head in new[] { "Warehouse", "Brand", "Part number", "Description", "Weight", "Term", "Qty", "Price, AED", "Purchase price", "Amount, AED", "Purchase amount", "Note" })
        {
            html.Append("<th style=\"border:1px solid #ccc;padding:6px;\">").Append(head).Append("</th>\n");
        }

        html.Append("</tr>\n</thead>\n<tbody>\n").Append(rows).Append("</tbody>\n</table>\n</div>\n\n");
        html.Append("<table style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;margin:16px 0 0 auto;border-collapse:collapse;min-width:320px;float:right;\">\n");
        void Row(string label, string value, string valueStyle) =>
            html.Append("<tr><td style=\"padding:4px 16px 4px 0;text-align:right;\">").Append(label)
                .Append("</td><td style=\"padding:4px 0;text-align:right;").Append(valueStyle).Append("\">").Append(value).Append("</td></tr>\n");
        Row("Net (ex VAT):", Dual(totals.Net), "font-weight:bold;");
        Row("VAT 5% (included in line prices):", Dual(totals.Vat), "font-weight:bold;");
        Row("Gross (incl. VAT):", Dual(totals.Gross), "font-weight:bold;");
        Row("Purchase amount:", Dual(purchaseTotal), string.Empty);
        Row("Your margin for this order:", Dual(margin), "font-weight:bold;color:#1a7f37;");
        Row("Courier (customer pays, ex-VAT):", Dual(totals.CourierNet), string.Empty);
        Row("VAT on courier:", Dual(totals.CourierVat), string.Empty);
        Row("Courier incl. VAT:", Dual(totals.CourierGross > 0 ? totals.CourierGross : totals.CourierNet), string.Empty);
        if (totals.VatNote.Length > 0)
        {
            html.Append("<tr><td colspan=\"2\" style=\"padding:6px 0 2px;text-align:right;font-size:12px;color:#555;\">").Append(H(totals.VatNote)).Append("</td></tr>\n");
        }

        html.Append("<tr><td style=\"padding:4px 16px 4px 0;text-align:right;font-size:16px;\"><strong>TOTAL:</strong></td><td style=\"padding:4px 0;text-align:right;font-size:16px;\"><strong>")
            .Append(Dual(total)).Append("</strong></td></tr>\n");
        html.Append("<tr><td style=\"padding:4px 16px 4px 0;text-align:right;\">Weight:</td><td style=\"padding:4px 0;text-align:right;\">")
            .Append(weightTotal > 0 ? PhpNumber(weightTotal, 3, string.Empty) : "—").Append("</td></tr>\n</table>\n<div style=\"clear:both;\"></div>\n\n");
        if (comment.Length > 0)
        {
            html.Append("<p style=\"margin:18px 0 6px;\"><strong>Comment:</strong></p>\n")
                .Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;padding:8px 12px;background:#f9f9f9;border:1px solid #e0e0e0;border-radius:4px;\">\n")
                .Append(Nl2Br(H(comment))).Append("\n</div>\n");
        }

        html.Append("\n<p style=\"margin:24px 0 0;\">\n\t<a style=\"color:").Append(H(mainColor)).Append(";\" href=\"").Append(H(cpOrderUrl))
            .Append("\" target=\"_blank\">View order in your Control Panel</a>\n</p>\n</div>\n");
        return html.ToString();
    }

    public sealed record StaffTotals(decimal Net, decimal Vat, decimal Gross, decimal CourierNet, decimal CourierVat, decimal CourierGross, string VatNote);

    /// <summary>
    /// The staff-layout VAT block: a first pass of <c>epc_uae_customer_vat_order_line()</c> over every line, then the
    /// destination-aware pass over counted lines (stored gross kept when within 0.02) plus <c>epc_order_courier_vat_amounts()</c>.
    /// </summary>
    public static async Task<StaffTotals> StaffTotalsAsync(
        DbConnection connection,
        OrderEmailData data,
        int customerId,
        string howGetJson,
        decimal priceSumTotal,
        CancellationToken cancellationToken)
    {
        var tax = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var ctx = await ErpDashboardReadService.CustomerContextAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        var displayInclusive = ErpDashboardReadService.DisplayMode(ctx.VatType) == "inclusive";

        var baseRate = ErpDashboardReadService.SupplyRate(ctx.Country, false, tax.RatePercent);
        decimal net = 0m, vat = 0m, gross = 0m;
        foreach (var item in data.Items)
        {
            var line = ErpDashboardReadService.LineAmounts(item.Price, item.CountNeed, baseRate, displayInclusive && baseRate > 0m, tax.SalesEnabled);
            net += line.LineNet;
            vat += line.VatAmount;
            gross += line.Gross;
        }

        if (gross <= 0m)
        {
            gross = priceSumTotal;
            net = gross;
            vat = 0m;
        }

        var dest = ErpDashboardReadService.DestinationCountry(howGetJson, ctx.Country);
        var exports = dest != "AE";
        var rate = ErpDashboardReadService.SupplyRate(ctx.Country, exports, tax.RatePercent);
        decimal itemsNet = 0m, itemsVat = 0m;
        foreach (var item in data.Items.Where(i => !data.NotCountStatuses.Contains(i.Status)))
        {
            var line = ErpDashboardReadService.LineAmounts(item.Price, item.CountNeed, rate, displayInclusive && rate > 0m, tax.SalesEnabled);
            itemsNet += line.LineNet;
            itemsVat += line.VatAmount;
        }

        if (itemsNet > 0m || itemsVat > 0m)
        {
            vat = ErpDashboardReadService.Round2(itemsVat);
            net = ErpDashboardReadService.Round2(itemsNet);
            gross = ErpDashboardReadService.Round2(itemsNet + itemsVat);
            if (priceSumTotal > 0m && Math.Abs(gross - priceSumTotal) < 0.02m)
            {
                gross = priceSumTotal;
            }
        }

        var courierNet = ErpDashboardReadService.Round2(Math.Max(0m, ErpDashboardReadService.CourierAmount(howGetJson)));
        var courierRate = ErpDashboardReadService.SupplyRate(dest, exports, tax.RatePercent);
        var courierVat = 0m;
        var courierGross = courierNet;
        if (courierNet > 0m && courierRate > 0m && tax.SalesEnabled)
        {
            courierVat = ErpDashboardReadService.Round2(courierNet * courierRate / 100m);
            courierGross = ErpDashboardReadService.Round2(courierNet + courierVat);
        }

        var note = exports
            ? "Zero-rated export (ship to " + dest + ") — no VAT on goods/courier"
            : "UAE — VAT on goods and courier (income)";
        return new StaffTotals(net, vat, gross, courierNet, courierVat, courierGross, note);
    }

    /// <summary>PHP <c>epc_build_customer_profile_html()</c>.</summary>
    public static async Task<string> CustomerProfileHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        int customerId,
        IReadOnlyDictionary<string, string>? order,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        var profile = await ProfileAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        if (customerId == 0 && order is not null)
        {
            profile["email"] = Field(order, "email_not_auth");
            profile["phone"] = Field(order, "phone_not_auth");
        }

        string P(string key) => profile.TryGetValue(key, out var v) ? v : string.Empty;
        var rows = new List<(string Label, string Value)>();
        void Push(string label, string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > 0)
            {
                rows.Add((label, value));
            }
        }

        var name = (P("name") + " " + P("surname")).Trim();
        if (name.Length == 0 && !PhpEmpty(P("company")))
        {
            name = P("company");
        }

        Push("Name", name);
        Push("Company", P("company"));
        foreach (var tinKey in new[] { "inn", "tin", "vat", "tax_id", "tax_number" })
        {
            if (!PhpEmpty(P(tinKey)))
            {
                Push("TIN", P(tinKey));
                break;
            }
        }

        var email = P("email");
        if (email.Length > 0)
        {
            rows.Add(("Email", "<a href=\"mailto:" + H(email) + "\">" + H(email) + "</a>"));
        }

        if (profile.TryGetValue("groups", out var firstGroup) && !PhpEmpty(firstGroup))
        {
            try
            {
                var gval = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `groups` WHERE `id` = ? LIMIT 1"), cancellationToken, PhpLong(firstGroup))
                    .ConfigureAwait(false) ?? string.Empty;
                if (gval.Length > 0)
                {
                    Push("Profile", await translator.TextAsync(gval, cancellationToken).ConfigureAwait(false));
                }
            }
            catch (DbException)
            {
            }
        }

        Push("Client city", P("city"));
        try
        {
            var fields = new List<(string Key, string Caption)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT IFNULL(`name`,''), IFNULL(`caption`,'') FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    fields.Add((Str(reader, 0), Str(reader, 1)));
                }
            }

            foreach (var (key, caption) in fields)
            {
                if (key.Length == 0 || key is "name" or "surname" or "company" or "inn" or "tin" or "vat" or "city" or "address")
                {
                    continue;
                }

                if (!PhpEmpty(P(key)))
                {
                    Push(await translator.TextAsync(caption, cancellationToken).ConfigureAwait(false), P(key));
                }
            }
        }
        catch (DbException)
        {
        }

        if (!PhpEmpty(P("phone")))
        {
            Push("Phone", P("phone"));
        }

        var html = new StringBuilder();
        html.Append("<h4 style=\"font-family:Calibri,Arial,sans-serif;margin:18px 0 8px;\">Information on the client who placed the order:</h4>");
        html.Append("<table style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;border-collapse:collapse;width:100%;max-width:640px;\">");
        foreach (var (label, value) in rows)
        {
            html.Append("<tr><td style=\"padding:4px 12px 4px 0;font-weight:bold;vertical-align:top;width:180px;\">")
                .Append(H(label)).Append(":</td><td style=\"padding:4px 0;\">").Append(value).Append("</td></tr>");
        }

        if (customerId > 0)
        {
            var backend = config.TryGetValue("backend_dir", out var b) && b.Trim().Length > 0 ? b.Trim() : "cp";
            var domain = (config.TryGetValue("domain_path", out var d) ? d.Trim() : string.Empty).TrimEnd('/');
            html.Append("<tr><td colspan=\"2\" style=\"padding-top:10px;\"><a style=\"background:#2b78d6;color:#fff;text-decoration:none;padding:6px 12px;border-radius:4px;display:inline-block;\" href=\"")
                .Append(H(domain + "/" + backend + "/users/usermanager/user?user_id=" + customerId.ToString(CultureInfo.InvariantCulture)))
                .Append("\">Open customer in Control Panel</a></td></tr>");
        }

        html.Append("</table>");
        return html.ToString();
    }

    // ---- get_order_info_html_for_user.php ----

    public static async Task<string> BuildCustomerHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        OrderEmailData data,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        async Task<string> T(int id) => await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        var domainPath = config.TryGetValue("domain_path", out var dp) ? dp.Trim() : string.Empty;
        var orderNo = data.OrderId.ToString(CultureInfo.InvariantCulture);
        var html = new StringBuilder();
        if (data.Order is not null)
        {
            var order = data.Order;
            var mainColor = data.Template.MainColor.Length > 0 ? data.Template.MainColor : "#799658";
            var customerId = PhpLong(Field(order, "user_id"));
            var time = StorefrontNotifyDispatcher.PlatformTime(PhpLong(Field(order, "time")));
            var statusId = PhpLong(Field(order, "status"));
            var paidType = PhpLong(Field(order, "paid_type"));
            html.Append("\n<div style=\"margin-top:10px;\">\n\n");
            html.Append("<a style=\"background: ").Append(mainColor)
                .Append("; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;\" target=\"_blank\" href=\"")
                .Append(domainPath).Append(customerId > 0 ? "shop/orders/order?order_id=" : "shop/orders/zakaz-bez-registracii?order_id=").Append(orderNo).Append("\">")
                .Append(await T(4643)).Append("</a>\n\n");
            html.Append("<h4>").Append(await T(4883)).Append("</h4>\n\t\n<table>\n");
            html.Append("\t<tr> <td>").Append(await T(1082)).Append("</td> <td>").Append(orderNo).Append("</td> </tr>\n");
            html.Append("\t<tr> <td>").Append(await T(2242)).Append("</td> <td>")
                .Append(time.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append(' ')
                .Append(time.Hour.ToString(CultureInfo.InvariantCulture)).Append(':').Append(time.ToString("mm", CultureInfo.InvariantCulture)).Append("</td> </tr>\n");
            html.Append("\t<tr> <td>").Append(await T(2081)).Append("</td> <td>").Append(data.OrderStatuses.TryGetValue(statusId, out var sn) ? sn : string.Empty).Append("</td> </tr>\n");
            html.Append("\t<tr> <td>").Append(await T(4645)).Append("</td> <td>").Append(data.PaidTypes.TryGetValue(paidType, out var pt) ? pt : string.Empty).Append("</td> </tr>\n</table>\n\n");
            html.Append("<h4>").Append(await T(4528)).Append("</h4>\n\n<div style=\"overflow: hidden; overflow-x: auto;\">\n\t<table>\n\t\t<tr>\n");
            foreach (var id in new[] { 4529, 3516, 4379, 4530 })
            {
                html.Append("\t\t\t<td>").Append(await T(id)).Append("</td>\n");
            }

            html.Append("\t\t</tr>\n\t\t<tr>\n\t\t\t<td>\n\t\t\t\t<strong>\n\t\t\t\t\t");
            switch (PhpLong(Field(order, "paid")))
            {
                case 0:
                    html.Append("<div style=\"color:#FFF;background-color:#e74c3c;border-radius:3px;padding:6px 12px;font-weight:normal;\">").Append(await T(3513)).Append("</div>");
                    break;
                case 1:
                    html.Append("<div style=\"color:#FFF;background-color:#62cb31;border-radius:3px;padding:6px 12px;font-weight:normal;\">").Append(await T(3514)).Append("</div>");
                    break;
                case 2:
                    html.Append("<div style=\"color:#FFF;background-color:#3498db;border-radius:3px;padding:6px 12px;font-weight:normal;\">").Append(await T(3515)).Append("</div>");
                    break;
            }

            html.Append("\t\t\t\t</strong>\n\t\t\t</td>\n");
            html.Append("\t\t\t<td>").Append(data.PriceSum).Append("</td>\n");
            html.Append("\t\t\t<td>").Append(data.PaidSum).Append("</td>\n");
            html.Append("\t\t\t<td>").Append(data.PaidLeft).Append("</td>\n");
            html.Append("\t\t</tr>\n\t</table>\n</div>\n\n<div style=\"overflow-x:auto; margin-top:0px;\">\n");
            html.Append(await ObtainInfoHtmlAsync(connection, translator, data, cancellationToken).ConfigureAwait(false));
            html.Append("</div>\n\t\n");
        }

        var countTotal = 0m;
        var priceSumTotal = 0m;
        html.Append("\n<h4>").Append(await T(3498)).Append("</h4>\n\n<table>\n\t<tr>\n\t\t<td>ID</td>\n");
        foreach (var id in new[] { 2070, 2071, 2102, 2751, 2752, 3251, 2081, 3550 })
        {
            html.Append("\t\t<td>").Append(await T(id)).Append("</td>\n");
        }

        html.Append("\t</tr>\n\n");
        var termSuffix = await translator.TextAsync("5315", cancellationToken).ConfigureAwait(false);
        foreach (var item in data.Items)
        {
            var term = item.TimeToExe < item.TimeToExeGuaranteed
                ? item.TimeToExe.ToString(CultureInfo.InvariantCulture) + " - " + item.TimeToExeGuaranteed.ToString(CultureInfo.InvariantCulture)
                : item.TimeToExe.ToString(CultureInfo.InvariantCulture);
            term += " " + termSuffix;
            if (!data.NotCountStatuses.Contains(item.Status))
            {
                countTotal += item.CountNeed;
                priceSumTotal += item.PriceSum;
            }

            data.ItemStatuses.TryGetValue(item.Status, out var status);
            html.Append("\t\n\t<tr style=\"background: ").Append(status?.Color ?? string.Empty).Append(";\">\n");
            html.Append("\t\t<td>").Append(item.Id.ToString(CultureInfo.InvariantCulture)).Append("</td>\n");
            html.Append("\t\t<td>").Append(item.Manufacturer).Append("</td>\n");
            html.Append("\t\t<td>").Append(item.Article).Append("</td>\n");
            html.Append("\t\t<td>").Append(item.Name).Append("</td>\n");
            html.Append("\t\t<td>").Append(PhpNumber(item.Price, 2, string.Empty)).Append("</td>\n");
            html.Append("\t\t<td>").Append(item.CountNeedRaw).Append("</td>\n");
            html.Append("\t\t<td>").Append(PhpNumber(item.PriceSum, 2, string.Empty)).Append("</td>\n");
            html.Append("\t\t<td>").Append(status?.Name ?? string.Empty).Append("</td>\n");
            html.Append("\t\t<td>").Append(term).Append("</td>\n\t</tr>\n");
        }

        html.Append("\n\t<tr>\n\t\t<td></td>\n\t\t<td></td>\n\t\t<td></td>\n\t\t<td></td>\n\t\t<td>").Append(await T(3503)).Append("</td>\n");
        html.Append("\t\t<td>").Append(PhpFloat(countTotal)).Append("</td>\n");
        html.Append("\t\t<td>").Append(PhpNumber(priceSumTotal, 2, string.Empty)).Append("</td>\n\t\t<td></td>\n\t\t<td></td>\n\t</tr>\n\n</table>\n\n</div>\n\n");
        return html.ToString();
    }

    /// <summary>The <c>shop/obtaining_modes/{handler}/show_actual_info.php</c> include for the order's mode.</summary>
    public static async Task<string> ObtainInfoHtmlAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        OrderEmailData data,
        CancellationToken cancellationToken)
    {
        if (data.Order is null || !data.ObtainModes.TryGetValue(PhpLong(Field(data.Order, "how_get")), out var mode))
        {
            return string.Empty;
        }

        var how = HowGetMap(Field(data.Order, "how_get_json"));
        string HowGet(string key) => how.TryGetValue(key, out var v) ? v : string.Empty;
        var html = new StringBuilder();
        if (mode.Handler == EpcObtainModes.EpcCarriers)
        {
            var code = how.TryGetValue("carrier", out var c) ? c : "dhl";
            var carrier = EpcObtainModes.Carriers.FirstOrDefault(x => x.Code == code);
            html.Append("<p class=\"lead\">Delivery — ").Append(H(carrier?.Name ?? code.ToUpperInvariant())).Append("</p>\n");
            html.Append("<table class=\"table\">\n<tr><td>").Append(H(HowGet("city"))).Append(", ").Append(H(HowGet("country"))).Append("</td></tr>\n");
            html.Append("<tr><td>").Append(H(HowGet("address"))).Append("</td></tr>\n");
            html.Append("<tr><td>Phone: ").Append(H(HowGet("phone"))).Append("</td></tr>\n</table>\n");
            var shipments = new List<(string Carrier, string Label, string Tracking, string Status)>();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`carrier_code`,''), IFNULL(`label_url`,''), IFNULL(`tracking_number`,''), IFNULL(`status`,'') FROM `epc_carrier_shipments` WHERE `order_id` = ? ORDER BY `id` DESC");
                ErpDb.AddParameters(command, data.OrderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    shipments.Add((Str(reader, 0), Str(reader, 1), Str(reader, 2), Str(reader, 3)));
                }
            }
            catch (DbException)
            {
            }

            if (shipments.Count > 0)
            {
                html.Append("<h5>Tracking</h5>\n<ul>\n");
                foreach (var s in shipments)
                {
                    html.Append("\t<li>\n\t\t").Append(H(s.Carrier.ToUpperInvariant())).Append(":\n\t\t\t");
                    html.Append(s.Label.Length > 0
                        ? "<a href=\"" + H(s.Label) + "\" target=\"_blank\" rel=\"noopener\">" + H(s.Tracking) + "</a>"
                        : H(s.Tracking));
                    html.Append("\n\t\t(").Append(H(s.Status)).Append(")\n\t</li>\n");
                }

                html.Append("</ul>\n");
            }

            return html.ToString();
        }

        if (mode.Handler != EpcObtainModes.GetInOffice)
        {
            return string.Empty;
        }

        html.Append("<p class=\"lead\">").Append(await translator.TextAsync(3507, cancellationToken).ConfigureAwait(false)).Append(" - ")
            .Append(await translator.TextAsync(mode.Caption, cancellationToken).ConfigureAwait(false)).Append("</p>\n");
        var office = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT IFNULL(`city`,''), IFNULL(`address`,''), IFNULL(`timetable`,''), IFNULL(`phone`,'') FROM `shop_offices` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, PhpLong(HowGet("office_id")));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                office["city"] = Str(reader, 0);
                office["address"] = Str(reader, 1);
                office["timetable"] = Str(reader, 2);
                office["phone"] = Str(reader, 3);
            }
        }
        catch (DbException)
        {
        }

        string O(string key) => office.TryGetValue(key, out var v) ? v : string.Empty;
        html.Append("\n<table class=\"table\">\n\t<tr>\n\t\t<th>").Append(await translator.TextAsync(4418, cancellationToken).ConfigureAwait(false)).Append("</th>\n\t</tr>\n\t<tr>\n\t\t<td>\n\t\t\t<span>")
            .Append(await translator.TextAsync(3376, cancellationToken).ConfigureAwait(false)).Append(": ")
            .Append(await translator.TextAsync(O("city"), cancellationToken).ConfigureAwait(false)).Append(", ")
            .Append(await translator.TextAsync(O("address"), cancellationToken).ConfigureAwait(false))
            .Append("</span> <a title=\"").Append(await translator.TextAsync(4445, cancellationToken).ConfigureAwait(false))
            .Append("\" style=\"cursor:pointer;\" data-toggle=\"collapse\" onClick=\"ymaps_init();\" data-target=\"#collapse_office_map_container\" aria-expanded=\"false\" aria-controls=\"collapse_office_map_container\"><i class=\"fa fa-map-o\" aria-hidden=\"true\"></i></a>\n")
            .Append("\t\t\t<div class=\"collapse\" id=\"collapse_office_map_container\">\n\t\t\t\t<br/>\n\t\t\t\t<div style=\"width: 100%;\" id=\"map\" class=\"office_map_container\"></div>\n\t\t\t</div>\n\t\t</td>\n\t</tr>\n");
        html.Append("\t<tr>\n\t\t<td>").Append(await translator.TextAsync(4446, cancellationToken).ConfigureAwait(false)).Append(": ")
            .Append((await translator.TextAsync(O("timetable"), cancellationToken).ConfigureAwait(false)).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</td>\n\t</tr>\n");
        html.Append("\t<tr>\n\t\t<td>").Append(await translator.TextAsync(1312, cancellationToken).ConfigureAwait(false)).Append(": ").Append(O("phone")).Append("</td>\n\t</tr>\n</table>\n");
        return html.ToString();
    }

    // ---- shared order data (the reference lists both PHP layouts load) ----

    public sealed record ItemStatus(string Name, string Color);

    public sealed record ObtainMode(string Caption, string Handler);

    public sealed record OrderItem(
        long Id,
        long Status,
        decimal Price,
        decimal CountNeed,
        string CountNeedRaw,
        decimal PriceSum,
        decimal PurchaseSum,
        decimal T2PricePurchase,
        string Manufacturer,
        string Article,
        string Name,
        long StorageId,
        string Storage,
        int TimeToExe,
        int TimeToExeGuaranteed,
        string ProductJson);

    public sealed class OrderEmailData
    {
        public long OrderId { get; init; }
        public IReadOnlyDictionary<string, string>? Order { get; init; }
        public string PriceSum { get; init; } = string.Empty;
        public string PaidSum { get; init; } = string.Empty;
        public string PaidLeft { get; init; } = string.Empty;
        public IReadOnlyList<OrderItem> Items { get; init; } = [];
        public IReadOnlyDictionary<long, string> OrderStatuses { get; init; } = new Dictionary<long, string>();
        public IReadOnlyDictionary<long, ItemStatus> ItemStatuses { get; init; } = new Dictionary<long, ItemStatus>();
        public IReadOnlySet<long> NotCountStatuses { get; init; } = new HashSet<long>();
        public IReadOnlyDictionary<long, string> Storages { get; init; } = new Dictionary<long, string>();
        public IReadOnlyDictionary<long, string> PaidTypes { get; init; } = new Dictionary<long, string>();
        public IReadOnlyDictionary<long, ObtainMode> ObtainModes { get; init; } = new Dictionary<long, ObtainMode>();
        public StorefrontNotifyDispatcher.EmailTemplateSettings Template { get; init; } = StorefrontNotifyDispatcher.EmailTemplateSettings.None;

        public static async Task<OrderEmailData> LoadAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
        {
            var orderStatuses = new Dictionary<long, string>();
            foreach (var row in await RowsAsync(connection, "SELECT * FROM `shop_orders_statuses_ref` ORDER BY `order` ASC", [], cancellationToken).ConfigureAwait(false))
            {
                orderStatuses[PhpLong(Field(row, "id"))] = Field(row, "name");
            }

            var itemStatuses = new Dictionary<long, ItemStatus>();
            var notCount = new HashSet<long>();
            foreach (var row in await RowsAsync(connection, "SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC", [], cancellationToken).ConfigureAwait(false))
            {
                var id = PhpLong(Field(row, "id"));
                itemStatuses[id] = new ItemStatus(Field(row, "name"), Field(row, "color"));
                if (row.ContainsKey("count_flag") && PhpLong(Field(row, "count_flag")) == 0)
                {
                    notCount.Add(id);
                }
            }

            var storages = new Dictionary<long, string>();
            foreach (var row in await RowsAsync(connection, "SELECT `id`, `name` FROM `shop_storages`", [], cancellationToken).ConfigureAwait(false))
            {
                storages[PhpLong(Field(row, "id"))] = Field(row, "name");
            }

            var obtain = new Dictionary<long, ObtainMode>();
            foreach (var row in await RowsAsync(connection, "SELECT * FROM `shop_obtaining_modes`", [], cancellationToken).ConfigureAwait(false))
            {
                obtain[PhpLong(Field(row, "id"))] = new ObtainMode(Field(row, "caption"), EpcObtainModes.SanitizeHandler(Field(row, "handler")));
            }

            var template = await StorefrontNotifyDispatcher.LoadTemplateSettingsAsync(connection, cancellationToken).ConfigureAwait(false);
            var orderRows = await RowsAsync(connection, "SELECT * FROM `shop_orders` WHERE `id` = ?", [orderId], cancellationToken).ConfigureAwait(false);
            var order = orderRows.Count > 0 ? orderRows[0] : null;
            var paidTypes = new Dictionary<long, string>();
            var priceSum = string.Empty;
            var paidSum = string.Empty;
            var paidLeft = string.Empty;
            if (order is not null)
            {
                foreach (var row in await RowsAsync(connection, "SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`", [], cancellationToken).ConfigureAwait(false))
                {
                    paidTypes[PhpLong(Field(row, "id"))] = Field(row, "name");
                }

                var where = string.Concat(notCount.Order().Select(s => " AND `status` != " + s.ToString(CultureInfo.InvariantCulture)));
                decimal? sum = null;
                try
                {
                    var raw = await ErpDb.ScalarAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT CAST(SUM(`price`*`count_need`) AS DECIMAL(20,2)) FROM `shop_orders_items` WHERE `order_id` = ?" + where),
                        cancellationToken,
                        orderId).ConfigureAwait(false);
                    sum = raw is null or DBNull ? null : Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                }
                catch (DbException)
                {
                }

                var paid = 0m;
                try
                {
                    var raw = await ErpDb.ScalarAsync(
                        connection,
                        null,
                        ErpDb.Positional(
                            "SELECT IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?), 0) "
                            + "- IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)"),
                        cancellationToken,
                        orderId,
                        orderId).ConfigureAwait(false);
                    paid = raw is null or DBNull ? 0m : Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
                }
                catch (DbException)
                {
                }

                priceSum = sum is null ? string.Empty : PhpNumber(sum.Value, 2, string.Empty);
                paidSum = PhpNumber(paid, 2, string.Empty);
                paidLeft = sum is null ? string.Empty : PhpNumber(sum.Value - paid, 2, string.Empty);
            }

            var items = new List<OrderItem>();
            foreach (var row in await RowsAsync(
                connection,
                "SELECT *, IFNULL((SELECT SUM(`price_purchase`*`count_reserved`) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), "
                + "CAST(`t2_price_purchase`*`count_need` AS DECIMAL(8,2))) AS `epc_price_purchase_sum`, CAST(`price`*`count_need` AS DECIMAL(8,2)) AS `epc_price_sum` "
                + "FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`",
                [orderId],
                cancellationToken).ConfigureAwait(false))
            {
                items.Add(new OrderItem(
                    PhpLong(Field(row, "id")),
                    PhpLong(Field(row, "status")),
                    PhpDecimal(Field(row, "price")),
                    PhpDecimal(Field(row, "count_need")),
                    Field(row, "count_need"),
                    PhpDecimal(Field(row, "epc_price_sum")),
                    PhpDecimal(Field(row, "epc_price_purchase_sum")),
                    PhpDecimal(Field(row, "t2_price_purchase")),
                    Field(row, "t2_manufacturer"),
                    Field(row, "t2_article"),
                    Field(row, "t2_name"),
                    PhpLong(Field(row, "t2_storage_id")),
                    Field(row, "t2_storage"),
                    (int)PhpLong(Field(row, "t2_time_to_exe")),
                    (int)PhpLong(Field(row, "t2_time_to_exe_guaranteed")),
                    Field(row, "t2_product_json")));
            }

            return new OrderEmailData
            {
                OrderId = orderId,
                Order = order,
                PriceSum = priceSum,
                PaidSum = paidSum,
                PaidLeft = paidLeft,
                Items = items,
                OrderStatuses = orderStatuses,
                ItemStatuses = itemStatuses,
                NotCountStatuses = notCount,
                Storages = storages,
                PaidTypes = paidTypes,
                ObtainModes = obtain,
                Template = template,
            };
        }
    }

    // ---- helpers ----

    /// <summary>PHP <c>number_format($v, $decimals, '.', $thousands)</c> (half away from zero).</summary>
    public static string PhpNumber(decimal value, int decimals, string thousands)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var format = (thousands.Length > 0 ? "#,##0" : "0") + (decimals > 0 ? "." + new string('0', decimals) : string.Empty);
        var text = rounded.ToString(format, CultureInfo.InvariantCulture);
        if (text.StartsWith("-", StringComparison.Ordinal) && rounded == 0m)
        {
            text = text[1..];
        }

        return thousands.Length > 0 && thousands != "," ? text.Replace(",", thousands, StringComparison.Ordinal) : text;
    }

    /// <summary>PHP <c>echo</c> of a numeric sum: integral values print without decimals.</summary>
    public static string PhpFloat(decimal value)
        => value == Math.Truncate(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##############", CultureInfo.InvariantCulture);

    /// <summary>The staff-layout delivery address: the first non-empty how_get_json key, else the PHP <c>json_encode</c> of it.</summary>
    public static string DeliveryAddress(string howGetJson)
    {
        if (string.IsNullOrWhiteSpace(howGetJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(howGetJson);
            var root = doc.RootElement;
            var nonEmpty = root.ValueKind switch
            {
                JsonValueKind.Object => root.EnumerateObject().Any(),
                JsonValueKind.Array => root.GetArrayLength() > 0,
                _ => false,
            };
            if (!nonEmpty)
            {
                return string.Empty;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in DeliveryAddressKeys)
                {
                    if (root.TryGetProperty(key, out var v) && !PhpEmpty(Scalar(v)) && v.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    {
                        return Scalar(v).Trim();
                    }
                }
            }

            return PhpJsonEncode(root).Trim();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>PHP <c>json_encode($v, JSON_UNESCAPED_UNICODE)</c>: compact, <c>\/</c> escaped, unicode literal.</summary>
    public static string PhpJsonEncode(JsonElement element)
    {
        var sb = new StringBuilder();
        Encode(element, sb);
        return sb.ToString();

        static void Encode(JsonElement e, StringBuilder sb)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    sb.Append('{');
                    var first = true;
                    foreach (var p in e.EnumerateObject())
                    {
                        if (!first)
                        {
                            sb.Append(',');
                        }

                        first = false;
                        EncodeString(p.Name, sb);
                        sb.Append(':');
                        Encode(p.Value, sb);
                    }

                    sb.Append('}');
                    break;
                case JsonValueKind.Array:
                    sb.Append('[');
                    var firstItem = true;
                    foreach (var item in e.EnumerateArray())
                    {
                        if (!firstItem)
                        {
                            sb.Append(',');
                        }

                        firstItem = false;
                        Encode(item, sb);
                    }

                    sb.Append(']');
                    break;
                case JsonValueKind.String:
                    EncodeString(e.GetString() ?? string.Empty, sb);
                    break;
                case JsonValueKind.True:
                    sb.Append("true");
                    break;
                case JsonValueKind.False:
                    sb.Append("false");
                    break;
                case JsonValueKind.Null:
                    sb.Append("null");
                    break;
                default:
                    sb.Append(e.GetRawText());
                    break;
            }
        }

        static void EncodeString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '/': sb.Append("\\/"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < 0x20)
                        {
                            sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(ch);
                        }

                        break;
                }
            }

            sb.Append('"');
        }
    }

    /// <summary>Staff layout line weight: <c>t2_product_json</c> weight ?? mass ?? Weight.</summary>
    public static decimal ProductWeight(string productJson)
    {
        if (string.IsNullOrWhiteSpace(productJson))
        {
            return 0m;
        }

        try
        {
            using var doc = JsonDocument.Parse(productJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 0m;
            }

            foreach (var key in new[] { "weight", "mass", "Weight" })
            {
                if (doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null)
                {
                    return PhpDecimal(Scalar(v));
                }
            }
        }
        catch (JsonException)
        {
        }

        return 0m;
    }

    public static string H(string value) => StorefrontSupplierLpoNotifier.H(value);

    /// <summary>PHP <c>nl2br()</c>: <c>&lt;br /&gt;</c> before each line break, the break itself kept.</summary>
    public static string Nl2Br(string value)
        => System.Text.RegularExpressions.Regex.Replace(value, "(\r\n|\n\r|\n|\r)", "<br />$1");

    private static async Task<string> WarehouseAsync(DbConnection connection, OrderEmailData data, OrderItem item, CancellationToken cancellationToken)
    {
        var sid = item.StorageId;
        var digits = item.Storage.Length > 0 && item.Storage.All(char.IsAsciiDigit);
        if (sid <= 0 && digits)
        {
            sid = PhpLong(item.Storage);
        }

        var warehouse = sid > 0 && data.Storages.TryGetValue(sid, out var name) ? name : string.Empty;
        if (warehouse.Length == 0 && item.Storage.Length > 0 && !digits)
        {
            warehouse = item.Storage;
        }

        if (warehouse.Length == 0 && sid > 0)
        {
            try
            {
                warehouse = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    sid).ConfigureAwait(false) ?? string.Empty;
            }
            catch (DbException)
            {
                warehouse = string.Empty;
            }
        }

        return warehouse;
    }

    private static async Task<decimal> UsdRateAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await ErpDb.ScalarAsync(connection, null, "SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = '840' LIMIT 1", cancellationToken).ConfigureAwait(false);
            if (raw is null or DBNull)
            {
                return 3.6725m;
            }

            var rate = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return rate <= 0m ? 1m : rate;
        }
        catch (DbException)
        {
            return 3.6725m;
        }
    }

    private static async Task<string> FirstCustomerMessageAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }

    /// <summary><c>DP_User::getUserProfileById()</c> flattened: users columns, users_profiles keys, first group id under <c>groups</c>.</summary>
    private static async Task<Dictionary<string, string>> ProfileAsync(DbConnection connection, int customerId, CancellationToken cancellationToken)
    {
        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        if (customerId == 0)
        {
            try
            {
                profile["groups"] = await ErpDb.StringAsync(connection, null, "SELECT `id` FROM `groups` WHERE `for_guests` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false) ?? string.Empty;
            }
            catch (DbException)
            {
            }

            return profile;
        }

        var users = await RowsAsync(connection, "SELECT * FROM `users` WHERE `user_id` = ?", [customerId], cancellationToken).ConfigureAwait(false);
        if (users.Count > 0)
        {
            foreach (var key in new[] { "email", "email_confirmed", "phone", "phone_confirmed", "reg_variant" })
            {
                profile[key] = Field(users[0], key);
            }
        }

        foreach (var row in await RowsAsync(connection, "SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?", [customerId], cancellationToken).ConfigureAwait(false))
        {
            profile[Field(row, "data_key")] = Field(row, "data_value");
        }

        var groups = await RowsAsync(connection, "SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?", [customerId], cancellationToken).ConfigureAwait(false);
        profile["groups"] = groups.Count > 0 ? Field(groups[0], "group_id") : string.Empty;
        return profile;
    }

    private static async Task<List<Dictionary<string, string>>> RowsAsync(
        DbConnection connection,
        string sql,
        object?[] parameters,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = Str(reader, i);
                }

                rows.Add(row);
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }

    private static async Task<List<long>> IdsAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
        }

        return ids;
    }

    private static async Task LogAsync(DbConnection connection, long orderId, string text, List<string> log, CancellationToken cancellationToken)
    {
        log.Add(text);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?, ?, 0, 0, ?, 1)"),
                cancellationToken,
                orderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                text).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static Dictionary<string, string> HowGetMap(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    map[p.Name] = Scalar(p.Value);
                }
            }
        }
        catch (JsonException)
        {
        }

        return map;
    }

    internal static string HostOf(string domainPath)
    {
        var raw = domainPath.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return uri.Host.ToLowerInvariant();
        }

        return raw.Trim('/').ToLowerInvariant();
    }

    private static string Field(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var v) ? v : string.Empty;

    private static string Str(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Scalar(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "1",
        _ => string.Empty,
    };

    /// <summary>PHP <c>empty()</c> for a scalar string: "" and "0" are empty.</summary>
    private static bool PhpEmpty(string value) => value.Length == 0 || value == "0";

    private static long PhpLong(string raw)
    {
        raw = (raw ?? string.Empty).Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] == '-')))
        {
            end++;
        }

        return long.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static decimal PhpDecimal(string raw)
        => decimal.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;
}
