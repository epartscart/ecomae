using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The storefront "My data" page (<c>/users/profile</c>, PHP <c>content/users/profileform.php</c>), byte for byte: the
/// trade account, VAT treatment, tax-exempt certificate and registration variant rows, the phone and e-mail contact
/// widgets, the profile fields of <c>DP_User::getUserProfile()</c> that are not <c>users</c> columns, the edit link and
/// the dealing currency panel. Rendering stores <c>customer_vat_type</c> when it is missing and runs the VAT and
/// e-invoice schema helpers, as PHP does. <see cref="PostAsync"/> is the page's currency change request.
/// </summary>
public static partial class StorefrontProfileForm
{
    public const string AlertItemKey = "epc.storefront.profile.alert";

    public const string CurrencyChangeAlert = "<div class=\"alert alert-success\">Currency change request sent to the manager.</div>";

    private static readonly IReadOnlyDictionary<string, string> VatTypeLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["local_b2c"] = "UAE retail (B2C) — prices incl. VAT",
        ["local_b2b"] = "UAE business (B2B) — prices excl. VAT",
        ["gcc"] = "GCC buyer — export / zero-rated",
        ["export"] = "Export — zero-rated",
        ["tax_exempt"] = "Tax-exempt certificate",
    };

    private static readonly string[] ProfileUserColumns =
    [
        "email", "email_confirmed", "email_code_send_lock_expired", "phone", "phone_confirmed", "phone_code_send_lock_expired", "reg_variant",
    ];

    private static readonly (string Key, string Value)[] UaeVatSettingDefaults =
    [
        ("vat_percent", "5.00"), ("vat_uae_sales_only", "1"), ("company_country_code", "AE"), ("company_trn", ""), ("company_vat_registered", "1"), ("company_legal_name", ""),
    ];

    /// <summary>The visitor as <c>DP_User</c> sees them: the <c>session</c> and <c>u_id</c> cookies, the language prefix and the PHP config.</summary>
    public sealed record Request(string? SessionCookie, string? UserCookie, string LangHref, IReadOnlyDictionary<string, string> Config);

    /// <summary>The admin notice of a currency change request: PHP <c>epc_build_customer_profile_html()</c> and <c>send_notify()</c> to <c>epc_staff_notify_persons()</c>.</summary>
    public sealed record Notifier(
        Func<DbConnection, long, CancellationToken, Task<string>> ProfileHtml,
        Func<DbConnection, string, IReadOnlyDictionary<string, string>, long, CancellationToken, Task> Send);

    /// <summary>The answer to a POST: the JSON of a CSRF refusal, else the alert the page prints first (none for a guest).</summary>
    public sealed record PostOutcome(string? Body, string? Alert);

    public static bool IsCurrencyChangePost(IReadOnlyDictionary<string, string> post) => post.ContainsKey("epc_request_currency_change");

    /// <summary>PHP <c>DP_User::getUserId()</c>: <c>u_id</c> when exactly one session row has both cookies, else 0.</summary>
    public static async Task<long> UserIdAsync(DbConnection connection, Request request, CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?"), cancellationToken, request.SessionCookie, request.UserCookie).ConfigureAwait(false);
        return count == 1 ? ShopPayForOrderService.PhpIntCast(request.UserCookie) : 0;
    }

    /// <summary>
    /// The <c>epc_request_currency_change</c> POST of a signed-in customer: the <c>content/users/stop_csrf.php</c> check
    /// against the customer session (the query key wins), <c>epc_trade_request_currency_change()</c>, then the
    /// <c>reg_notify_admin</c> notice with the requested currency and the note.
    /// </summary>
    public static async Task<PostOutcome> PostAsync(
        DbConnection connection,
        Request request,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, string> query,
        Notifier notifier,
        CancellationToken cancellationToken)
    {
        var userId = await UserIdAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (userId == 0 || !IsCurrencyChangePost(post))
        {
            return new PostOutcome(null, null);
        }

        var posted = query.TryGetValue("csrf_guard_key", out var fromQuery) ? fromQuery : post.GetValueOrDefault("csrf_guard_key");
        if (posted is null)
        {
            return new PostOutcome(StorefrontLoginPost.CsrfError("Error! CSRF 1"), null);
        }

        if (AuthEmailOtp.PhpEmpty(posted))
        {
            return new PostOutcome(StorefrontLoginPost.CsrfError("Error! CSRF 3"), null);
        }

        var stored = await SessionCsrfAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return new PostOutcome(StorefrontLoginPost.CsrfError("Error! CSRF 3.1"), null);
        }

        if (!string.Equals(stored, posted, StringComparison.Ordinal))
        {
            return new PostOutcome(StorefrontLoginPost.CsrfError("Error! CSRF 4"), null);
        }

        var requestedIso = new string((post.GetValueOrDefault("epc_requested_currency") ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var note = PhpTrim(post.GetValueOrDefault("epc_currency_change_note") ?? string.Empty);
        await EpcCustomerTrade.RequestCurrencyChangeAsync(connection, null, userId, requestedIso, note, cancellationToken).ConfigureAwait(false);

        var body = new StringBuilder(await notifier.ProfileHtml(connection, userId, cancellationToken).ConfigureAwait(false));
        var currencies = await EpcCurrency.RecordsAsync(connection, Config(request, "shop_currency"), cancellationToken).ConfigureAwait(false);
        var requested = requestedIso.Length > 0 ? currencies.FirstOrDefault(c => c.IsoCode == requestedIso) : null;
        if (requested is not null)
        {
            body.Append("<p><strong>Requested currency:</strong> ").Append(H(requested.CaptionShort)).Append("</p>");
        }

        if (note.Length > 0)
        {
            body.Append("<p><strong>Customer note:</strong> ").Append(H(note)).Append("</p>");
        }

        body.Append("<p>Customer requested a dealing currency change. Review in CP → Users → Customer approvals.</p>");
        await notifier.Send(
            connection,
            "reg_notify_admin",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["user_profile"] = body.ToString() },
            userId,
            cancellationToken).ConfigureAwait(false);
        return new PostOutcome(null, CurrencyChangeAlert);
    }

    /// <summary>The page for the visitor: translation 4709 for a guest, else the profile.</summary>
    public static async Task<string> RenderAsync(DbConnection connection, Request request, Func<string?, Task<string>> t, CancellationToken cancellationToken)
    {
        var userId = await UserIdAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (userId == 0)
        {
            return await t("4709").ConfigureAwait(false);
        }

        var profile = await UserProfileAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var csrf = await SessionCsrfAsync(connection, request, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var lang = request.LangHref;
        var shopCurrency = Config(request, "shop_currency");
        var sb = new StringBuilder();
        sb.Append("\t\n\t\n\t\n\t<!-- Здесь храним html для формы ввода кода подтверждения телефона -->\n\t<div id=\"phone_code_store\" style=\"display:none;\" class=\"hidden\">\n\t\t<form method=\"GET\" action=\"")
            .Append(lang)
            .Append("/users/confirm_contact\">\n\t\t\t<input type=\"hidden\" name=\"u_id\" value=\"")
            .Append(userId.ToString(CultureInfo.InvariantCulture))
            .Append("\" />\n\t\t\t<input type=\"hidden\" name=\"type\" value=\"phone\" />\n\t\t\n\t\t\t<div class=\"input-group\">\n\t\t\t\t<input value=\"\" type=\"text\" class=\"form-control\" placeholder=\"")
            .Append(await t("4708").ConfigureAwait(false))
            .Append("\" name=\"code\" id=\"code\" />\n\t\t\t\t<span class=\"input-group-btn\">\n\t\t\t\t\t<button class=\"btn btn-ar btn-primary\" type=\"submit\">")
            .Append(await t("4521").ConfigureAwait(false))
            .Append("</button>\n\t\t\t\t</span>\n\t\t\t</div>\n\t\t</form>\n\t</div>\n\t\n\t\n    \n    <table class=\"table\">\n\t");

        var tradeStatus = await EpcCustomerTrade.ApprovalStatusAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
        var tradeType = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_customer_type", cancellationToken).ConfigureAwait(false);
        if (tradeType.Length > 0 || tradeStatus != "approved")
        {
            sb.Append("<tr><td><b>Trade account</b></td><td>");
            if (tradeType.Length > 0)
            {
                sb.Append(EpcCustomerTrade.CustomerTypeLabel(tradeType));
            }

            sb.Append("<br><span class=\"label label-").Append(tradeStatus == "approved" ? "success" : tradeStatus == "pending" ? "warning" : "danger").Append("\">")
                .Append(H(Ucfirst(tradeStatus))).Append("</span>");
            if (tradeStatus == "pending")
            {
                sb.Append("<br><small>You can browse and add to cart. Checkout opens after manager approval.</small>");
            }

            if (tradeStatus == "approved")
            {
                var iso = await EpcCustomerTrade.UserCurrencyIsoAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
                var rows = await EpcCurrency.RecordsAsync(connection, shopCurrency, cancellationToken).ConfigureAwait(false);
                sb.Append("<br><strong>Dealing currency:</strong> ").Append(H(rows.FirstOrDefault(r => r.IsoCode == iso)?.CaptionShort ?? iso));
                if (await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_currency_change_requested", cancellationToken).ConfigureAwait(false) == "1")
                {
                    sb.Append(" <span class=\"label label-info\">Change pending</span>");
                }
            }

            sb.Append("</td></tr>");
        }

        var vatType = await VatTypeAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var priceLabel = await SalesVatEnabledAsync(connection, cancellationToken).ConfigureAwait(false) ? PriceLabel(vatType) : string.Empty;
        sb.Append("<tr><td><b>VAT treatment</b></td><td>").Append(H(VatTypeLabels[vatType]))
            .Append("<br><small>Prices shown: <strong>").Append(vatType == "local_b2c" ? "incl. VAT" : "excl. VAT").Append("</strong>")
            .Append(priceLabel.Length > 0 ? " · " + H(priceLabel) : string.Empty)
            .Append("</small></td></tr>");

        if (tradeType == "wholesale")
        {
            var certPath = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_tax_exempt_cert_path", cancellationToken).ConfigureAwait(false);
            var certStatus = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_tax_exempt_cert_status", cancellationToken).ConfigureAwait(false);
            sb.Append("<tr><td><b>Tax-exempt certificate</b></td><td>");
            if (certPath.Length > 0)
            {
                sb.Append("<span class=\"label label-").Append(certStatus == "approved" ? "success" : "warning").Append("\">")
                    .Append(H(certStatus.Length > 0 ? Ucwords(certStatus.Replace('_', ' ')) : "Uploaded")).Append("</span>")
                    .Append(" <a href=\"").Append(H(certPath)).Append("\" target=\"_blank\" rel=\"noopener\">View file</a><br>");
            }
            else
            {
                sb.Append("<span class=\"text-muted\">Upload your tax-exempt certificate for manager review.</span><br>");
            }

            sb.Append("<form id=\"epc-tax-exempt-form\" style=\"margin-top:8px;\" enctype=\"multipart/form-data\">")
                .Append("<input type=\"hidden\" name=\"csrf_guard_key\" value=\"").Append(H(csrf)).Append("\" />")
                .Append("<input type=\"file\" name=\"tax_exempt_cert\" accept=\".pdf,.jpg,.jpeg,.png,.webp\" class=\"form-control\" style=\"max-width:320px;display:inline-block;\" /> ")
                .Append("<button type=\"button\" class=\"btn btn-sm btn-primary\" onclick=\"epcUploadTaxExempt();\">Upload</button>")
                .Append("<div id=\"epc-tax-exempt-msg\" class=\"small\" style=\"margin-top:6px;\"></div></form>")
                .Append("</td></tr>");
        }

        if (await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `reg_variants`", cancellationToken).ConfigureAwait(false) > 1)
        {
            var caption = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `caption` FROM `reg_variants` WHERE `id` = ?"), cancellationToken, profile.Text("reg_variant")).ConfigureAwait(false);
            sb.Append("<tr> <td><b>").Append(await t("4646").ConfigureAwait(false)).Append("</b></td> <td>").Append(await t(caption).ConfigureAwait(false)).Append("</td></tr>");
        }

        sb.Append(await ContactScriptAsync(request, csrf, t).ConfigureAwait(false)).Append("\n\t");

        var communications = await StorefrontPhpAjax.AvailableCommunicationsAsync(connection, request.Config, cancellationToken).ConfigureAwait(false);
        if (communications.All || communications.Sms)
        {
            sb.Append("        <tr> \n\t\t\t<td><b>").Append(await t("1312").ConfigureAwait(false))
                .Append("</b></td>\n\t\t\t<td id=\"phone_work\"></td>\n\t\t</tr>\n\t\t<script>\n\t\tset_contact_html('").Append(profile.Text("phone"))
                .Append("', ").Append(ShopPayForOrderService.PhpIntCast(profile.Text("phone_confirmed")).ToString(CultureInfo.InvariantCulture))
                .Append(", 'phone');//Инициализация при загрузке страницы\n\t\t</script>\n        ");
        }

        if (communications.All || !communications.Sms)
        {
            sb.Append("        <tr> \n\t\t\t<td><b>E-mail</b></td>\n\t\t\t<td id=\"email_work\"></td>\n\t\t</tr>\n\t\t<script>\n\t\tset_contact_html('").Append(profile.Text("email"))
                .Append("', ").Append(ShopPayForOrderService.PhpIntCast(profile.Text("email_confirmed")).ToString(CultureInfo.InvariantCulture))
                .Append(", 'email');//Инициализация при загрузке страницы\n\t\t</script>\n\t\t");
        }

        var userColumns = await UsersColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var key in profile.Keys)
        {
            if (userColumns.Contains(key))
            {
                continue;
            }

            string parameter;
            string value;
            if (key == "user_id")
            {
                parameter = await t("3007").ConfigureAwait(false);
                value = profile.Text(key);
            }
            else if (key == "groups")
            {
                parameter = await t("3547").ConfigureAwait(false);
                var names = new StringBuilder();
                foreach (var groupId in profile.Groups)
                {
                    var groupValue = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `groups` WHERE `id` = ?"), cancellationToken, groupId).ConfigureAwait(false);
                    if (names.Length > 0)
                    {
                        names.Append(";<br>");
                    }

                    names.Append(await t(groupValue).ConfigureAwait(false));
                }

                value = names.ToString();
            }
            else
            {
                var caption = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `caption` FROM `reg_fields` WHERE `name` = ?"), cancellationToken, key).ConfigureAwait(false);
                parameter = await t(caption).ConfigureAwait(false);
                value = profile.Text(key);
            }

            sb.Append("        <tr> <td><b>").Append(parameter).Append("</b></td> <td>").Append(value).Append("</td></tr>\n        ");
        }

        sb.Append("    \n    </table>\n    \n    <a class=\"btn btn-ar btn-primary\" href=\"").Append(lang).Append("/users/editform\">")
            .Append(await t("4739").ConfigureAwait(false)).Append("</a>\n\n");

        var dealing = ShopPayForOrderService.PhpIntCast(await EpcCustomerTrade.UserCurrencyIsoAsync(connection, null, userId, cancellationToken).ConfigureAwait(false));
        if (tradeStatus == "approved" && dealing > 0)
        {
            var rows = await EpcCustomerTrade.CurrencyOptionsAsync(connection, shopCurrency, cancellationToken).ConfigureAwait(false);
            var dealingText = dealing.ToString(CultureInfo.InvariantCulture);
            var label = rows.FirstOrDefault(r => r.IsoCode == dealingText)?.CaptionShort ?? dealingText;
            var changeRequested = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_currency_change_requested", cancellationToken).ConfigureAwait(false) == "1";
            var requestedIso = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, "epc_currency_change_requested_iso", cancellationToken).ConfigureAwait(false);
            sb.Append("\t<div class=\"panel panel-default\" style=\"margin-top:24px;\">\n\t\t<div class=\"panel-heading\"><strong>Dealing currency</strong></div>\n\t\t<div class=\"panel-body\">\n\t\t\t<p>Your approved dealing currency is <strong>")
                .Append(H(label))
                .Append("</strong>. Prices and checkout use this currency until a manager approves a change.</p>\n\t\t\t");
            if (changeRequested)
            {
                sb.Append("\t\t\t\t<div class=\"alert alert-info\">You have a pending currency change request");
                var requested = requestedIso.Length > 0 ? rows.FirstOrDefault(r => r.IsoCode == requestedIso) : null;
                if (requested is not null)
                {
                    sb.Append(" to <strong>").Append(H(requested.CaptionShort)).Append("</strong>");
                }

                sb.Append(". A manager will review it.</div>\n\t\t\t");
            }
            else
            {
                sb.Append("\t\t\t\t<form method=\"post\" action=\"\">\n\t\t\t\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"").Append(H(csrf))
                    .Append("\" />\n\t\t\t\t\t<input type=\"hidden\" name=\"epc_request_currency_change\" value=\"1\">\n\t\t\t\t\t<div class=\"form-group\">\n\t\t\t\t\t\t<label>Request a different currency</label>\n\t\t\t\t\t\t<select name=\"epc_requested_currency\" class=\"form-control\" style=\"max-width:280px;\" required>\n\t\t\t\t\t\t\t");
                foreach (var row in rows)
                {
                    var iso = ShopPayForOrderService.PhpIntCast(row.IsoCode);
                    if (iso == dealing)
                    {
                        continue;
                    }

                    sb.Append("<option value=\"").Append(iso.ToString(CultureInfo.InvariantCulture)).Append("\">").Append(H(row.CaptionShort + " (" + row.IsoName + ")")).Append("</option>");
                }

                sb.Append("\t\t\t\t\t\t</select>\n\t\t\t\t\t</div>\n\t\t\t\t\t<div class=\"form-group\">\n\t\t\t\t\t\t<label>Reason (optional)</label>\n\t\t\t\t\t\t<textarea name=\"epc_currency_change_note\" class=\"form-control\" rows=\"2\" style=\"max-width:480px;\" placeholder=\"Why do you need a different currency?\"></textarea>\n\t\t\t\t\t</div>\n\t\t\t\t\t<button type=\"submit\" class=\"btn btn-default\">Submit currency change request</button>\n\t\t\t\t</form>\n\t\t\t");
            }

            sb.Append("\t\t</div>\n\t</div>\n\t");
        }

        sb.Append(TaxExemptScript).Append("\n    \n");
        return sb.ToString();
    }

    /// <summary>
    /// The VAT type of the VAT row: the stored <c>customer_vat_type</c>, else the resolved type, which is then stored.
    /// The context is read again afterwards (which ensures the e-invoice tables), as the PHP resolve does.
    /// </summary>
    private static async Task<string> VatTypeAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var stored = await EpcCustomerTrade.ProfileGetAsync(connection, null, userId, EpcUaeCustomerVat.ProfileKey, cancellationToken).ConfigureAwait(false);
        if (!VatTypeLabels.ContainsKey(stored))
        {
            stored = await EpcUaeCustomerVat.ResolveTypeAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
            await EpcCustomerTrade.ProfileSetAsync(connection, null, userId, EpcUaeCustomerVat.ProfileKey, stored, cancellationToken).ConfigureAwait(false);
        }

        await EpcUaeCustomerVat.ContextAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
        return stored;
    }

    /// <summary>PHP <c>epc_uae_customer_vat_price_label()</c> once VAT sales are on.</summary>
    private static string PriceLabel(string vatType) => vatType switch
    {
        "local_b2b" => "excl. VAT",
        "gcc" or "export" => "Export",
        "tax_exempt" => "Tax exempt",
        _ => "incl. VAT",
    };

    /// <summary>
    /// PHP <c>epc_uae_vat_sales_enabled()</c>: <c>vat_uae_sales_only</c> on, then (after the missing UAE VAT price settings
    /// are added) the e-invoice seller country is AE and the company is VAT registered.
    /// </summary>
    private static async Task<bool> SalesVatEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!PhpSettingOn(await PriceSettingAsync(connection, "vat_uae_sales_only", "1", cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        foreach (var (key, value) in UaeVatSettingDefaults)
        {
            try
            {
                var exists = await ErpDb.ScalarAsync(connection, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), cancellationToken, key).ConfigureAwait(false);
                if (exists is null && !await PriceSettingExistsAsync(connection, key, cancellationToken).ConfigureAwait(false))
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)"), cancellationToken, key, value).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
            }
        }

        var registered = PhpSettingOn(await PriceSettingAsync(connection, "company_vat_registered", "1", cancellationToken).ConfigureAwait(false));
        await EpcEinvoiceBuyer.EnsureSchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
        string? seller;
        try
        {
            seller = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"), cancellationToken, "seller_country_code").ConfigureAwait(false);
        }
        catch (DbException)
        {
            seller = null;
        }

        return EpcEinvoiceBuyer.NormalizeCountry(seller) == "AE" && registered;
    }

    /// <summary>PHP <c>epc_pricing_get_setting()</c>: the stored value (null for a NULL value), the default when there is no row or table.</summary>
    private static async Task<string?> PriceSettingAsync(DbConnection connection, string key, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), cancellationToken, key).ConfigureAwait(false);
            return value is null && !await PriceSettingExistsAsync(connection, key, cancellationToken).ConfigureAwait(false) ? fallback : value;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static async Task<bool> PriceSettingExistsAsync(DbConnection connection, string key, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_price_settings` WHERE `setting_key` = ?"), cancellationToken, key).ConfigureAwait(false) > 0;

    private static bool PhpSettingOn(string? value) => value is "1" or "true" or "";

    private static async Task<string> ContactScriptAsync(Request request, string csrf, Func<string?, Task<string>> t)
    {
        var ids = Regex.Matches(ContactScript, @"\{\{T:(\d+)\}\}").Select(m => m.Groups[1].Value).Distinct().ToArray();
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            texts[id] = await t(id).ConfigureAwait(false);
        }

        var mask = string.Empty;
        if (ShopPayForOrderService.PhpIntCast(Config(request, "show_phone_mask")) == 1)
        {
            mask = Config(request, "country_phone_mask") switch
            {
                "ru" => "mask = \"+7 (999) 999-99-99\";//Россия",
                "kz" => "mask = \"+7 (999) 999-99-99\";//Казахстан",
                "by" => "mask = \"+375 (99) 999-99-99\";//Белоруссия",
                "ua" => "mask = \"+380 (99) 999-9999\";//Украина",
                _ => string.Empty,
            };
        }

        return Regex.Replace(ContactScript, @"\{\{(T:(\d+)|MASK|CSRF|LANG)\}\}", m => m.Groups[1].Value switch
        {
            "MASK" => mask,
            "CSRF" => csrf,
            "LANG" => request.LangHref,
            _ => texts[m.Groups[2].Value],
        });
    }

    /// <summary>PHP <c>DP_User::getUserSession()</c>'s <c>csrf_guard_key</c>; null without the session row.</summary>
    internal static async Task<string?> SessionCsrfAsync(DbConnection connection, Request request, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1");
        ErpDb.AddParameters(command, request.SessionCookie, request.UserCookie);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
    }

    private static async Task<HashSet<string>> UsersColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `COLUMN_NAME` FROM `INFORMATION_SCHEMA`.`COLUMNS` WHERE `TABLE_NAME` = 'users' AND `TABLE_SCHEMA` = ?");
        ErpDb.AddParameters(command, connection.Database);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return columns;
    }

    /// <summary>
    /// PHP <c>DP_User::getUserProfile()</c> for a signed-in customer, in PHP array order: <c>user_id</c>, the contact
    /// columns of <c>users</c>, the <c>users_profiles</c> keys (a later row overwrites in place) and <c>groups</c>
    /// (the bound groups, else the first registered-customer group).
    /// </summary>
    private static async Task<UserProfile> UserProfileAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var profile = new UserProfile();
        profile.Set("user_id", userId.ToString(CultureInfo.InvariantCulture));
        var user = new Dictionary<string, string?>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT * FROM `users` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    user[reader.GetName(i)] = PdoText(reader, i);
                }
            }
        }

        foreach (var column in ProfileUserColumns)
        {
            profile.Set(column, user.GetValueOrDefault(column));
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                profile.Set(
                    Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                profile.Groups.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        if (profile.Groups.Count == 0)
        {
            var registered = await ErpDb.StringAsync(connection, null, "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            if (registered is not null)
            {
                profile.Groups.Add(registered);
            }
        }

        profile.Set("groups", null);
        return profile;
    }

    /// <summary>A column as PDO returns it: null for NULL, <c>1</c>/<c>0</c> for a <c>TINYINT(1)</c> the driver reads as a bool.</summary>
    internal static string? PdoText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private sealed class UserProfile
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        public List<string> Keys { get; } = [];

        public List<string> Groups { get; } = [];

        public void Set(string key, string? value)
        {
            if (!_values.ContainsKey(key))
            {
                Keys.Add(key);
            }

            _values[key] = value;
        }

        public string Text(string key) => _values.GetValueOrDefault(key) ?? string.Empty;
    }

    private static string Config(Request request, string key) => request.Config.TryGetValue(key, out var value) ? value : string.Empty;

    private static string Ucfirst(string value) => value.Length > 0 && value[0] is >= 'a' and <= 'z' ? (char)(value[0] - 32) + value[1..] : value;

    private static string Ucwords(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if ((i == 0 || chars[i - 1] is ' ' or '\t' or '\r' or '\n' or '\f' or '\v') && chars[i] is >= 'a' and <= 'z')
            {
                chars[i] = (char)(chars[i] - 32);
            }
        }

        return new string(chars);
    }

    private static string PhpTrim(string value) => value.Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    private static string H(string value) => StorefrontSupplierLpoNotifier.H(value);
}
