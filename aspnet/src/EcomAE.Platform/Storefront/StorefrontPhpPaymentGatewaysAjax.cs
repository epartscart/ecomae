using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The UAE gateway stubs under PHP <c>content/shop/finance/payment_systems/</c>: <c>{handler}/go_to_pay.php</c>,
/// <c>pay_page_entry.php</c> / <c>pay_page.php</c> / <c>epc_demo/pay_page.php</c>, <c>crypto_pay_page.php</c>,
/// <c>{handler}/notification.php</c> and the NOWPayments IPN, byte-for-byte with the PHP templates.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string PaymentSystemsPath = "/content/shop/finance/payment_systems";

    public const string NoHandler = "No handler";

    private const string GatewayHtmlType = "text/html; charset=UTF-8";

    /// <summary>The handler folders whose go_to_pay.php / notification.php delegate to <c>epc_demo</c>.</summary>
    public static readonly IReadOnlyList<string> DemoGatewayHandlers =
    [
        "adyen", "amazon_ps", "authorize_net", "cashu", "ccavenue", "checkout_com", "cybersource", "easypaisa",
        "hyperpay", "jazzcash", "myfatoorah", "network_intl", "nowpayments", "payoneer", "paypal", "paytabs",
        "razorpay", "skrill", "stripe", "tabby", "tamara", "tap", "telr", "twocheckout",
    ];

    /// <summary>The query, form, cookies and referer a gateway page reads, with the PHP <c>lang_href</c> and config.php values.</summary>
    public sealed record GatewayPageRequest(
        IReadOnlyDictionary<string, string> Query,
        IReadOnlyDictionary<string, string> Form,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer,
        string LangHref,
        IReadOnlyDictionary<string, string> Config);

    /// <summary>A PHP <c>header('Location: …')</c> answer.</summary>
    public sealed record GatewayRedirect(string Location);

    /// <summary>POST <paramref name="json"/> to a NOWPayments URL with the <c>x-api-key</c> header; the HTTP status and body.</summary>
    public delegate Task<(int Status, string Body)> NowPaymentsPost(string url, string apiKey, string json, CancellationToken cancellationToken);

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', $handler)</c>: case-sensitive, nothing is lowered.</summary>
    public static string PhpGatewayHandler(string? raw) => Regex.Replace(raw ?? string.Empty, "[^a-z0-9_]", string.Empty);

    /// <summary>
    /// PHP <c>{handler}/go_to_pay.php</c> → <c>epc_demo/go_to_pay.php</c> → shared <c>go_to_pay.php</c>: <c>stop_csrf.php</c>,
    /// the caller's pending operation (else <c>{"result":false,"code":2}</c>), then the hidden form to the crypto page
    /// (nowpayments), the "Configure gateway" alert (live mode) or the hidden form to the demo pay page.
    /// </summary>
    public static async Task<object> GoToPayAsync(DbConnection connection, string? handlerName, GatewayPageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(handlerName))
        {
            return new RawHttp(NoHandler, GatewayHtmlType);
        }

        var csrf = request.Query.TryGetValue("csrf_guard_key", out var fromQuery)
            ? fromQuery
            : request.Form.TryGetValue("csrf_guard_key", out var fromForm) ? fromForm : null;
        var failure = await StopCsrfAsync(connection, csrf, request.Session, request.UserCookie, request.AdminSession, request.AdminUser, request.Referer, GatewayHtmlType, cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var userId = await CookieUserIdAsync(connection, request.Session, request.UserCookie, cancellationToken).ConfigureAwait(false);
        var operationId = ShopPayForOrderService.PhpIntCast(request.Query.TryGetValue("operation", out var op) ? op : null);
        string? amount = null;
        string? payOrders = null;
        var found = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `amount`, `pay_orders` FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;");
            ErpDb.AddParameters(command, operationId, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                amount = reader.IsDBNull(0) ? null : ErpDocumentControlRender.PdoString(reader, 0);
                payOrders = reader.IsDBNull(1) ? null : ErpDocumentControlRender.PdoString(reader, 1);
            }
        }

        if (!found)
        {
            return new RawHttp("{\"result\":false,\"code\":2}", GatewayHtmlType);
        }

        var translator = new StorefrontPhpTranslator(connection, GatewayLang(request.LangHref));
        var description = await translator.TextAsync(string.IsNullOrEmpty(payOrders) ? 4338 : 4350, cancellationToken).ConfigureAwait(false);
        var parameters = await StorefrontPaymentAccounts.ParametersAsync(connection, operationId, handlerName, request.Config.ContainsKey("wholesaler"), cancellationToken)
            .ConfigureAwait(false);
        var demoMode = ShopPayForOrderService.PhpTruthy(parameters.Values.TryGetValue("demo_mode", out var demo) ? demo : null);
        var currency = parameters.Values.TryGetValue("currency", out var configured) && ShopPayForOrderService.PhpTruthy(configured) ? configured : "AED";
        var sum = PhpFloatString(PhpFloatCast(amount));
        var handler = PhpGatewayHandler(handlerName);

        string Inputs(string indent)
            => indent + "<input type=\"hidden\" name=\"EPC_PAY_HANDLER\" value=\"" + H(handler) + "\">\n"
               + indent + "<input type=\"hidden\" name=\"operation_id\" value=\"" + operationId.ToString(CultureInfo.InvariantCulture) + "\">\n"
               + indent + "<input type=\"hidden\" name=\"sum\" value=\"" + H(sum) + "\">\n"
               + indent + "<input type=\"hidden\" name=\"operation_description\" value=\"" + H(description) + "\">\n"
               + indent + "<input type=\"hidden\" name=\"user_id\" value=\"" + userId.ToString(CultureInfo.InvariantCulture) + "\">\n"
               + indent + "<input type=\"hidden\" name=\"currency\" value=\"" + H(currency) + "\">\n";

        if (handler == "nowpayments")
        {
            return new RawHttp(
                "\t<form name=\"pay_form\" style=\"display:none\" method=\"post\" action=\"" + H(PaymentSystemsPath + "/crypto_pay_page.php") + "\">\n"
                + Inputs("\t\t")
                + "\t</form>\n\t<script>document.forms['pay_form'].submit();</script>\n\t",
                GatewayHtmlType);
        }

        if (!demoMode)
        {
            return new RawHttp(
                "\t<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Configure gateway</title>\n"
                + "\t<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css\"></head>\n"
                + "\t<body style=\"padding:40px\"><div class=\"container\"><div class=\"alert alert-warning\">\n"
                + "\t<strong>" + H(GatewayTitle(handler)) + "</strong> is set to live mode but live API redirect for this acquirer is not wired yet.\n"
                + "\tEnable <em>Demo mode</em> in CP → Payment gateways, or keep using Crypto (NOWPayments) which supports live API keys.\n"
                + "\t<br><a href=\"" + request.LangHref + "/shop/balans\" class=\"btn btn-default btn-sm\" style=\"margin-top:12px\">Back</a>\n"
                + "\t</div></div></body></html>\n\t",
                GatewayHtmlType);
        }

        return new RawHttp(
            "<form name=\"pay_form\" style=\"display:none\" method=\"post\" action=\"" + PaymentSystemsPath + "/pay_page_entry.php\">\n"
            + Inputs("\t")
            + "</form>\n<script>document.forms['pay_form'].submit();</script>\n",
            GatewayHtmlType);
    }

    /// <summary>
    /// PHP <c>pay_page_entry.php</c> (and <c>epc_demo/pay_page.php</c>, which includes it) when <paramref name="entry"/>,
    /// else <c>pay_page.php</c> hit directly: the posted handler, then <c>pay_execute</c> (auto-post to the handler's
    /// notification.php with the demo token, or back to the balance with "Demo payment declined") or the demo checkout.
    /// </summary>
    public static RawHttp PayPage(GatewayPageRequest request, bool entry)
    {
        var posted = request.Form.TryGetValue("EPC_PAY_HANDLER", out var raw) ? raw : null;
        var preset = entry ? (posted is null ? string.Empty : PhpGatewayHandler(posted)) : string.Empty;
        if (preset.Length == 0 && ShopPayForOrderService.PhpTruthy(posted))
        {
            preset = PhpGatewayHandler(posted);
        }

        if (preset.Length == 0)
        {
            return new RawHttp(NoHandler, GatewayHtmlType);
        }

        var handler = PhpGatewayHandler(preset);
        var form = request.Form;
        var title = GatewayTitle(handler);
        var operationId = form.TryGetValue("operation_id", out var op) ? ShopPayForOrderService.PhpIntCast(op) : 0;
        var sum = form.TryGetValue("sum", out var postedSum) ? PhpFloatCast(postedSum) : 0d;
        var description = form.TryGetValue("operation_description", out var desc) ? desc : "Payment";
        var currency = form.TryGetValue("currency", out var cur) ? cur : "AED";
        var userId = form.TryGetValue("user_id", out var user) ? ShopPayForOrderService.PhpIntCast(user) : 0;
        var notifyUrl = PaymentSystemsPath + "/" + handler + "/notification.php";
        var ids = (Op: operationId.ToString(CultureInfo.InvariantCulture), User: userId.ToString(CultureInfo.InvariantCulture), Sum: H(PhpFloatString(sum)));

        if (form.TryGetValue("action", out var action) && action == "pay_execute")
        {
            if (form.TryGetValue("need_result", out var needResult) && needResult == "success")
            {
                return new RawHttp(
                    "\t\t<form name=\"success_form\" style=\"display:none\" method=\"post\" action=\"" + H(notifyUrl) + "\">\n"
                    + "\t\t\t<input type=\"hidden\" name=\"operation_id\" value=\"" + ids.Op + "\">\n"
                    + "\t\t\t<input type=\"hidden\" name=\"sum\" value=\"" + ids.Sum + "\">\n"
                    + "\t\t\t<input type=\"hidden\" name=\"user_id\" value=\"" + ids.User + "\">\n"
                    + "\t\t\t<input type=\"hidden\" name=\"demo_token\" value=\"" + StorefrontPaymentWriteService.DemoToken + "\">\n"
                    + "\t\t</form>\n\t\t<script>document.forms['success_form'].submit();</script>\n\t\t",
                    GatewayHtmlType);
            }

            return new RawHttp(
                "\t<script>location='" + request.LangHref + "/shop/balans?error_message=" + OAuthStart.PhpUrlEncode("Demo payment declined") + "';</script>\n\t",
                GatewayHtmlType);
        }

        return new RawHttp(
            "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n"
            + "\t<meta charset=\"utf-8\">\n"
            + "\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n"
            + "\t<title>" + H(title) + " — Demo checkout</title>\n"
            + "\t<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css\">\n"
            + "\t<style>\n"
            + "\t\tbody { background:#f1f5f9; padding:40px 15px; }\n"
            + "\t\t.demo-card { max-width:480px; margin:0 auto; background:#fff; border-radius:12px; box-shadow:0 4px 24px rgba(0,0,0,.08); overflow:hidden; }\n"
            + "\t\t.demo-head { background:linear-gradient(135deg,#1e40af,#2563eb); color:#fff; padding:20px 24px; }\n"
            + "\t\t.demo-body { padding:24px; }\n"
            + "\t\t.demo-badge { display:inline-block; background:#fef3c7; color:#92400e; font-size:11px; font-weight:700; padding:4px 10px; border-radius:999px; text-transform:uppercase; }\n"
            + "\t\t.amount { font-size:28px; font-weight:700; color:#0f172a; }\n"
            + "\t</style>\n</head>\n<body>\n"
            + "\t<div class=\"demo-card\">\n"
            + "\t\t<div class=\"demo-head\">\n"
            + "\t\t\t<span class=\"demo-badge\">Demo mode</span>\n"
            + "\t\t\t<h3 style=\"margin:12px 0 4px;\">" + H(title) + "</h3>\n"
            + "\t\t\t<p style=\"margin:0;opacity:.9;font-size:13px;\">No real charge — replace dummy API keys in CP when ready.</p>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"demo-body\">\n"
            + "\t\t\t<p class=\"text-muted\">" + H(description) + "</p>\n"
            + "\t\t\t<p class=\"amount\">" + PhpNumberFormat(sum) + " " + H(currency) + "</p>\n"
            + "\t\t\t<p><small class=\"text-muted\">Operation #" + ids.Op + "</small></p>\n"
            + "\t\t\t<hr>\n"
            + "\t\t\t<form method=\"post\">\n"
            + "\t\t\t\t<input type=\"hidden\" name=\"action\" value=\"pay_execute\">\n"
            + "\t\t\t\t<input type=\"hidden\" name=\"operation_id\" value=\"" + ids.Op + "\">\n"
            + "\t\t\t\t<input type=\"hidden\" name=\"sum\" value=\"" + ids.Sum + "\">\n"
            + "\t\t\t\t<input type=\"hidden\" name=\"user_id\" value=\"" + ids.User + "\">\n"
            + "\t\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t\t<label>Simulate result</label>\n"
            + "\t\t\t\t\t<select class=\"form-control\" name=\"need_result\">\n"
            + "\t\t\t\t\t\t<option value=\"success\">Successful payment</option>\n"
            + "\t\t\t\t\t\t<option value=\"error\">Declined / failed</option>\n"
            + "\t\t\t\t\t</select>\n"
            + "\t\t\t\t</div>\n"
            + "\t\t\t\t<button type=\"submit\" class=\"btn btn-success btn-block btn-lg\">Complete demo payment</button>\n"
            + "\t\t\t</form>\n"
            + "\t\t</div>\n"
            + "\t</div>\n</body>\n</html>\n",
            GatewayHtmlType);
    }

    /// <summary>
    /// PHP <c>crypto_pay_page.php</c>: the coin picker, then <c>create_invoice</c> (the demo invoice, or a live
    /// NOWPayments <c>/payment</c> with the IPN callback), and <c>confirm_demo</c> (auto-post to the notification).
    /// </summary>
    public static async Task<object> CryptoPayPageAsync(DbConnection connection, GatewayPageRequest request, NowPaymentsPost post, CancellationToken cancellationToken)
    {
        var form = request.Form;
        var posted = form.TryGetValue("EPC_PAY_HANDLER", out var raw) ? raw : null;
        var handler = PhpGatewayHandler(ShopPayForOrderService.PhpTruthy(posted) ? PhpGatewayHandler(posted) : string.Empty);
        if (handler.Length == 0)
        {
            handler = "nowpayments";
        }

        var operationId = form.TryGetValue("operation_id", out var op)
            ? ShopPayForOrderService.PhpIntCast(op)
            : request.Query.TryGetValue("operation_id", out var queryOp) ? ShopPayForOrderService.PhpIntCast(queryOp) : 0;
        var sum = form.TryGetValue("sum", out var postedSum) ? PhpFloatCast(postedSum) : 0d;
        var description = form.TryGetValue("operation_description", out var desc) ? desc : "Crypto payment";
        var currency = form.TryGetValue("currency", out var cur) ? cur.ToUpperInvariant() : "USD";
        long userId = form.TryGetValue("user_id", out var user)
            ? ShopPayForOrderService.PhpIntCast(user)
            : await CookieUserIdAsync(connection, request.Session, request.UserCookie, cancellationToken).ConfigureAwait(false);
        var notifyUrl = PaymentSystemsPath + "/" + handler + "/notification.php";
        var parameters = (await StorefrontPaymentAccounts.ParametersAsync(connection, operationId, handler, request.Config.ContainsKey("wholesaler"), cancellationToken)
            .ConfigureAwait(false)).Values;
        var demoMode = ShopPayForOrderService.PhpTruthy(parameters.TryGetValue("demo_mode", out var demo) ? demo : null);
        var coins = CryptoAllowedCoins(parameters);
        var selectedCoin = form.TryGetValue("pay_coin", out var coin) ? Regex.Replace(coin, "[^a-z0-9]", string.Empty).ToLowerInvariant() : string.Empty;
        var action = form.TryGetValue("action", out var postedAction) ? postedAction : null;
        CryptoInvoice? invoice = null;
        var error = string.Empty;
        var sumText = H(PhpFloatString(sum));
        var ids = (Op: operationId.ToString(CultureInfo.InvariantCulture), User: userId.ToString(CultureInfo.InvariantCulture));

        if (action == "create_invoice")
        {
            if (selectedCoin.Length == 0 || !coins.Any(c => c.Code == selectedCoin))
            {
                error = "Select a cryptocurrency.";
            }
            else if (demoMode)
            {
                invoice = CryptoDemoInvoice(operationId, sum, selectedCoin);
            }
            else
            {
                var payload = "{\"price_amount\":" + PhpJsonFloat(sum)
                    + ",\"price_currency\":" + OAuthStart.PhpJsonString(currency.ToLowerInvariant())
                    + ",\"pay_currency\":" + OAuthStart.PhpJsonString(selectedCoin)
                    + ",\"order_id\":" + OAuthStart.PhpJsonString(ids.Op)
                    + ",\"order_description\":" + OAuthStart.PhpJsonString(description)
                    + ",\"ipn_callback_url\":" + OAuthStart.PhpJsonString(TrimEndSlash(Config(request.Config, "domain_path")) + notifyUrl)
                    + ",\"is_fixed_rate\":false}";
                var (ok, message, payment) = await CryptoCreateNowPaymentAsync(parameters, payload, post, cancellationToken).ConfigureAwait(false);
                if (!ok)
                {
                    error = message.Length > 0 ? message : "Could not create crypto invoice";
                }
                else
                {
                    invoice = payment;
                }
            }
        }

        if (action == "confirm_demo" && demoMode)
        {
            return new RawHttp(
                "\t<form name=\"success_form\" style=\"display:none\" method=\"post\" action=\"" + H(notifyUrl) + "\">\n"
                + "\t\t<input type=\"hidden\" name=\"operation_id\" value=\"" + ids.Op + "\">\n"
                + "\t\t<input type=\"hidden\" name=\"sum\" value=\"" + sumText + "\">\n"
                + "\t\t<input type=\"hidden\" name=\"user_id\" value=\"" + ids.User + "\">\n"
                + "\t\t<input type=\"hidden\" name=\"demo_token\" value=\"" + StorefrontPaymentWriteService.DemoToken + "\">\n"
                + "\t\t<input type=\"hidden\" name=\"pay_coin\" value=\"" + H(selectedCoin) + "\">\n"
                + "\t</form>\n\t<script>document.forms['success_form'].submit();</script>\n\t",
                GatewayHtmlType);
        }

        var html = new StringBuilder(8192);
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n")
            .Append("\t<meta charset=\"utf-8\">\n")
            .Append("\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("\t<title>Pay with crypto</title>\n")
            .Append("\t<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css\">\n")
            .Append("\t<style>\n")
            .Append("\t\t:root { --ink:#0f172a; --muted:#64748b; --line:#e2e8f0; --accent:#0f766e; --soft:#f8fafc; }\n")
            .Append("\t\tbody { background: linear-gradient(180deg,#ecfeff 0%, #f8fafc 40%, #fff 100%); padding:36px 14px; font-family: Georgia, 'Times New Roman', serif; }\n")
            .Append("\t\t.card { max-width:520px; margin:0 auto; background:#fff; border:1px solid var(--line); border-radius:14px; box-shadow:0 10px 30px rgba(15,23,42,.08); overflow:hidden; }\n")
            .Append("\t\t.head { padding:20px 22px; background: radial-gradient(700px 160px at 0% 0%, rgba(15,118,110,.14), transparent 55%), linear-gradient(180deg,#fff,#f8fafc); border-bottom:1px solid var(--line); }\n")
            .Append("\t\t.head h1 { margin:8px 0 4px; font-size:26px; color:var(--ink); letter-spacing:-.02em; }\n")
            .Append("\t\t.head p { margin:0; color:var(--muted); font-size:13px; font-family: system-ui, sans-serif; }\n")
            .Append("\t\t.badge { display:inline-block; background:#ccfbf1; color:#115e59; font:700 11px/1 system-ui,sans-serif; padding:5px 9px; border-radius:8px; text-transform:uppercase; letter-spacing:.04em; }\n")
            .Append("\t\t.body { padding:22px; font-family: system-ui, -apple-system, sans-serif; }\n")
            .Append("\t\t.amount { font-size:30px; font-weight:750; color:var(--ink); letter-spacing:-.02em; }\n")
            .Append("\t\t.coins { display:grid; grid-template-columns:1fr 1fr; gap:8px; margin:14px 0; }\n")
            .Append("\t\t.coins label { display:block; border:1px solid var(--line); border-radius:10px; padding:10px 12px; cursor:pointer; background:var(--soft); }\n")
            .Append("\t\t.coins label:has(input:checked) { border-color:#5eead4; background:#f0fdfa; box-shadow:inset 0 0 0 1px #99f6e4; }\n")
            .Append("\t\t.coins strong { display:block; font-size:13px; color:var(--ink); }\n")
            .Append("\t\t.coins span { font-size:11px; color:var(--muted); }\n")
            .Append("\t\t.btn-pay { background:var(--accent); border-color:var(--accent); font-weight:700; }\n")
            .Append("\t\t.invoice { background:var(--soft); border:1px solid var(--line); border-radius:12px; padding:14px; margin-top:12px; }\n")
            .Append("\t\t.addr { font-family: ui-monospace, Menlo, monospace; word-break:break-all; font-size:12px; background:#fff; border:1px dashed #99f6e4; padding:10px; border-radius:8px; }\n")
            .Append("\t\t@keyframes rise { from { opacity:0; transform:translateY(8px);} to { opacity:1; transform:none;} }\n")
            .Append("\t\t.card { animation: rise .4s ease both; }\n")
            .Append("\t</style>\n</head>\n<body>\n")
            .Append("\t<div class=\"card\">\n")
            .Append("\t\t<div class=\"head\">\n")
            .Append("\t\t\t<span class=\"badge\">").Append(demoMode ? "Demo crypto" : "Live crypto").Append("</span>\n")
            .Append("\t\t\t<h1>Pay with crypto</h1>\n")
            .Append("\t\t\t<p>USDT, Bitcoin, Ethereum and more — settle your order securely.</p>\n")
            .Append("\t\t</div>\n")
            .Append("\t\t<div class=\"body\">\n")
            .Append("\t\t\t<p class=\"text-muted\" style=\"margin-top:0;\">").Append(H(description)).Append("</p>\n")
            .Append("\t\t\t<div class=\"amount\">").Append(PhpNumberFormat(sum)).Append(' ').Append(H(currency)).Append("</div>\n")
            .Append("\t\t\t<p><small class=\"text-muted\">Operation #").Append(ids.Op).Append("</small></p>\n")
            .Append("\n\t\t\t");
        if (error.Length > 0)
        {
            html.Append("\t\t\t\t<div class=\"alert alert-danger\">").Append(H(error)).Append("</div>\n\t\t\t");
        }

        html.Append("\n\t\t\t");
        string Hidden(string name, string value) => "\t\t\t\t<input type=\"hidden\" name=\"" + name + "\" value=\"" + value + "\">\n";
        if (invoice is null)
        {
            html.Append("\t\t\t<form method=\"post\">\n")
                .Append(Hidden("action", "create_invoice"))
                .Append(Hidden("EPC_PAY_HANDLER", H(handler)))
                .Append(Hidden("operation_id", ids.Op))
                .Append(Hidden("sum", sumText))
                .Append(Hidden("operation_description", H(description)))
                .Append(Hidden("user_id", ids.User))
                .Append(Hidden("currency", H(currency)))
                .Append("\t\t\t\t<div class=\"coins\">\n\t\t\t\t\t");
            for (var i = 0; i < coins.Count; i++)
            {
                var (code, label, network) = coins[i];
                html.Append("\t\t\t\t\t<label>\n")
                    .Append("\t\t\t\t\t\t<input type=\"radio\" name=\"pay_coin\" value=\"").Append(H(code)).Append("\" ")
                    .Append(i == 0 || selectedCoin == code ? "checked" : string.Empty).Append(">\n")
                    .Append("\t\t\t\t\t\t<strong>").Append(H(label)).Append("</strong>\n")
                    .Append("\t\t\t\t\t\t<span>").Append(H(network.Length > 0 ? network : code.ToUpperInvariant())).Append("</span>\n")
                    .Append("\t\t\t\t\t</label>\n\t\t\t\t\t");
            }

            html.Append("\t\t\t\t</div>\n")
                .Append("\t\t\t\t<button type=\"submit\" class=\"btn btn-success btn-block btn-lg btn-pay\">Create crypto invoice</button>\n")
                .Append("\t\t\t</form>\n\t\t\t");
        }
        else
        {
            html.Append("\t\t\t<div class=\"invoice\">\n")
                .Append("\t\t\t\t<p style=\"margin:0 0 8px;\"><strong>Send exactly</strong></p>\n")
                .Append("\t\t\t\t<p class=\"amount\" style=\"font-size:22px;margin:0 0 10px;\">").Append(H(invoice.PayAmount)).Append(' ')
                .Append(H((invoice.PayCurrency ?? selectedCoin).ToUpperInvariant())).Append("</p>\n")
                .Append("\t\t\t\t<p style=\"margin:0 0 6px;color:#64748b;font-size:12px;\">To address</p>\n")
                .Append("\t\t\t\t<div class=\"addr\">").Append(H(invoice.PayAddress)).Append("</div>\n")
                .Append("\t\t\t\t<p style=\"margin:12px 0 0;font-size:12px;color:#64748b;\">\n")
                .Append("\t\t\t\t\tPayment ID: ").Append(H(invoice.PaymentId))
                .Append("\t\t\t\t\t").Append(invoice.Demo ? " · Demo invoice (no chain broadcast)" : string.Empty)
                .Append("\t\t\t\t</p>\n")
                .Append("\t\t\t</div>\n\t\t\t");
            if (invoice.Demo)
            {
                html.Append("\t\t\t<form method=\"post\" style=\"margin-top:14px;\">\n")
                    .Append(Hidden("action", "confirm_demo"))
                    .Append(Hidden("EPC_PAY_HANDLER", H(handler)))
                    .Append(Hidden("operation_id", ids.Op))
                    .Append(Hidden("sum", sumText))
                    .Append(Hidden("user_id", ids.User))
                    .Append(Hidden("pay_coin", H(selectedCoin.Length > 0 ? selectedCoin : invoice.PayCurrency ?? string.Empty)))
                    .Append("\t\t\t\t<button type=\"submit\" class=\"btn btn-success btn-block btn-lg btn-pay\">I paid — confirm (demo)</button>\n")
                    .Append("\t\t\t</form>\n\t\t\t");
            }
            else
            {
                html.Append("\t\t\t<p class=\"text-muted\" style=\"margin-top:14px;font-size:13px;\">After the network confirms your transfer, NOWPayments will notify this store and your order will be marked paid automatically. You can close this page.</p>\n")
                    .Append("\t\t\t<a class=\"btn btn-default btn-block\" href=\"").Append(H(request.LangHref + "/shop/balans")).Append("\">Back to balance</a>\n\t\t\t");
            }

            html.Append("\t\t\t");
        }

        html.Append("\t\t</div>\n\t</div>\n</body>\n</html>\n");
        return new RawHttp(html.ToString(), GatewayHtmlType);
    }

    /// <summary>
    /// PHP <c>{handler}/notification.php</c> → shared <c>notification.php</c>: the demo token (always required here), then
    /// activate + <c>pay_notify.php</c> + <c>pay_for_order.php</c> + settlements for a pending operation, and the redirect to
    /// the balance with translation 4355 either way.
    /// </summary>
    public static async Task<object> GatewayNotificationAsync(
        DbConnection connection,
        IStorefrontPaymentWriteService payments,
        string? handlerName,
        GatewayPageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(handlerName))
        {
            return new RawHttp(NoHandler, GatewayHtmlType);
        }

        var operationId = ShopPayForOrderService.PhpIntCast(
            request.Form.TryGetValue("operation_id", out var posted) ? posted : request.Query.TryGetValue("operation_id", out var queried) ? queried : null);
        var sum = request.Form.TryGetValue("sum", out var postedSum) ? PhpFloatCast(postedSum) : 0d;
        var token = request.Form.TryGetValue("demo_token", out var postedToken) ? postedToken : string.Empty;
        if (token != StorefrontPaymentWriteService.DemoToken)
        {
            return new RawHttp("Forbidden", GatewayHtmlType);
        }

        await payments.NotifyAsync(connection, 0, operationId, (decimal)sum, token, handlerName, cancellationToken).ConfigureAwait(false);
        var translator = new StorefrontPhpTranslator(connection, GatewayLang(request.LangHref));
        var message = await translator.TextAsync(4355, cancellationToken).ConfigureAwait(false);
        return new GatewayRedirect(Config(request.Config, "domain_path") + request.LangHref.TrimStart('/') + "/shop/balans?success_message=" + OAuthStart.PhpUrlEncode(message));
    }

    /// <summary>
    /// PHP <c>nowpayments/notification.php</c>: a JSON body with <c>payment_status</c> or <c>payment_id</c> is an IPN
    /// (HMAC-SHA512 of the raw body with <c>ipn_secret</c> against <c>x-nowpayments-sig</c>, a paid status and an operation,
    /// else 400 "IPN rejected"); anything else is the shared demo notification.
    /// </summary>
    public static async Task<object> NowPaymentsNotificationAsync(
        DbConnection connection,
        IStorefrontPaymentWriteService payments,
        GatewayPageRequest request,
        string rawBody,
        string? signature,
        CancellationToken cancellationToken)
    {
        JsonElement json;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(rawBody) ? "null" : rawBody);
            json = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            json = default;
        }

        string? Field(string name)
            => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
                ? PhpJsonScalarString(value)
                : null;

        var isIpn = json.ValueKind == JsonValueKind.Object && (Field("payment_status") is not null || Field("payment_id") is not null);
        if (!isIpn)
        {
            return await GatewayNotificationAsync(connection, payments, "nowpayments", request, cancellationToken).ConfigureAwait(false);
        }

        var operationId = ShopPayForOrderService.PhpIntCast(Field("order_id"));
        var sum = PhpFloatCast(Field("price_amount"));
        var parameters = (await StorefrontPaymentAccounts.ParametersAsync(connection, operationId, "nowpayments", request.Config.ContainsKey("wholesaler"), cancellationToken)
            .ConfigureAwait(false)).Values;
        var verified = CryptoVerifyIpn(parameters, rawBody, signature);
        var status = (Field("payment_status") ?? string.Empty).ToLowerInvariant();
        if (!verified || status is not ("finished" or "confirmed" or "sending") || operationId <= 0)
        {
            return new RawHttp("{\"result\":false,\"message\":\"IPN rejected\"}", "application/json", StatusCodes.Status400BadRequest);
        }

        var result = await payments.ApplyIpnAsync(connection, operationId, (decimal)sum, cancellationToken).ConfigureAwait(false);
        return new RawHttp(result.Code == "already" ? "{\"result\":true,\"message\":\"already processed\"}" : "{\"result\":true}", "application/json");
    }

    /// <summary>PHP <c>epc_crypto_verify_ipn</c>.</summary>
    public static bool CryptoVerifyIpn(IReadOnlyDictionary<string, string> parameters, string rawBody, string? signature)
    {
        var secret = (parameters.TryGetValue("ipn_secret", out var configured) ? configured : string.Empty).Trim();
        if (secret.Length == 0 || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var calc = Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(calc), Encoding.UTF8.GetBytes(signature));
    }

    /// <summary>One NOWPayments invoice as the crypto page prints it.</summary>
    public sealed record CryptoInvoice(string PayAmount, string PayAddress, string? PayCurrency, string PaymentId, bool Demo);

    /// <summary>
    /// PHP <c>epc_crypto_allowed_coins</c> (<c>content/shop/payments/epc_crypto_payments.php</c>): the <c>allowed_coins</c>
    /// CSV, unknown codes labelled by their upper case; <c>epc_crypto_default_coins</c> otherwise.
    /// </summary>
    public static IReadOnlyList<(string Code, string Label, string Network)> CryptoAllowedCoins(IReadOnlyDictionary<string, string> parameters)
    {
        IReadOnlyList<(string Code, string Label, string Network)> all =
        [
            ("usdttrc20", "USDT (TRC20)", "Tron"),
            ("usdtbsc", "USDT (BEP20)", "BNB Smart Chain"),
            ("btc", "Bitcoin", "BTC"),
            ("eth", "Ethereum", "ETH"),
            ("ltc", "Litecoin", "LTC"),
        ];
        var raw = (parameters.TryGetValue("allowed_coins", out var configured) ? configured : string.Empty).Trim();
        if (raw.Length == 0)
        {
            return all;
        }

        var picked = new List<(string Code, string Label, string Network)>();
        foreach (var part in Regex.Split(raw, "\\s*,\\s*"))
        {
            var code = Regex.Replace(part, "[^a-z0-9]", string.Empty).ToLowerInvariant();
            if (code.Length == 0)
            {
                continue;
            }

            var known = all.FirstOrDefault(c => c.Code == code);
            var entry = known.Code is null ? (code, code.ToUpperInvariant(), string.Empty) : known;
            var index = picked.FindIndex(c => c.Code == code);
            if (index >= 0)
            {
                picked[index] = entry;
            }
            else
            {
                picked.Add(entry);
            }
        }

        return picked.Count > 0 ? picked : all;
    }

    /// <summary>PHP <c>epc_crypto_demo_invoice</c>: a deterministic address per operation and coin at stub rates.</summary>
    public static CryptoInvoice CryptoDemoInvoice(long operationId, double sum, string coin)
    {
        var op = operationId.ToString(CultureInfo.InvariantCulture);
        var seed = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("epc-crypto|" + op + "|" + coin)))[..40];
        var address = "TDemo" + seed[..30].ToUpperInvariant();
        if (coin == "btc")
        {
            address = "bc1q" + seed[..38];
        }
        else if (coin == "eth" || coin.Contains("eth", StringComparison.Ordinal) || coin.Contains("bsc", StringComparison.Ordinal))
        {
            address = "0x" + seed;
        }

        var rate = coin switch { "btc" => 65000, "eth" => 3500, "ltc" => 85, _ => 1 };
        var fiat = Math.Max(0.01, sum);
        var payAmount = PhpRound(fiat / rate, rate >= 100 ? 8 : 2);
        return new CryptoInvoice(PhpFloatString(payAmount), address, coin, "demo_" + op + "_" + coin, true);
    }

    /// <summary>
    /// PHP <c>epc_crypto_create_nowpayment</c>: no call for an empty or DUMMY key, the <c>epc_crypto_api_base</c> sandbox or
    /// live URL, <c>epc_crypto_http_json</c> (ok on 2xx with a JSON array), else the API message, error or HTTP code.
    /// </summary>
    private static async Task<(bool Ok, string Message, CryptoInvoice? Payment)> CryptoCreateNowPaymentAsync(
        IReadOnlyDictionary<string, string> parameters,
        string payload,
        NowPaymentsPost post,
        CancellationToken cancellationToken)
    {
        var apiKey = (parameters.TryGetValue("api_key", out var key) ? key : string.Empty).Trim();
        if (apiKey.Length == 0 || apiKey.Contains("DUMMY", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "NOWPayments API key not configured", null);
        }

        var url = (ShopPayForOrderService.PhpTruthy(parameters.TryGetValue("sandbox", out var sandbox) ? sandbox : null)
            ? "https://api-sandbox.nowpayments.io/v1"
            : "https://api.nowpayments.io/v1") + "/payment";
        int status;
        string body;
        try
        {
            (status, body) = await post(url, apiKey, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            (status, body) = (0, string.Empty);
        }

        JsonElement data = default;
        try
        {
            using var doc = JsonDocument.Parse(body);
            data = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
        }

        var isArray = data.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        string? Get(string name)
            => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? PhpJsonScalarString(value) : null;
        if (status < 200 || status >= 300 || !isArray)
        {
            return (false, Get("message") ?? Get("error") ?? "NOWPayments HTTP " + status.ToString(CultureInfo.InvariantCulture), null);
        }

        return (true, string.Empty, new CryptoInvoice(Get("pay_amount") ?? Get("amount") ?? string.Empty, Get("pay_address") ?? string.Empty, Get("pay_currency"), Get("payment_id") ?? string.Empty, false));
    }

    /// <summary>
    /// PHP <c>stop_csrf.php</c>: <c>Error! CSRF 1</c> without a key, <c>3</c> for an empty key, <c>3.1</c> without the
    /// session (the admin one when the referer is the control panel) and <c>4</c> on a mismatch; null when it passes.
    /// </summary>
    internal static async Task<RawHttp?> StopCsrfAsync(
        DbConnection connection,
        string? csrfKey,
        string? session,
        string? userCookie,
        string? adminSession,
        string? adminUser,
        string? referer,
        string contentType,
        CancellationToken cancellationToken)
    {
        RawHttp Error(string message) => new(JsonSerializer.Serialize(new { error = message, message, status = false }), contentType);
        if (csrfKey is null)
        {
            return Error("Error! CSRF 1");
        }

        if (PhpEmptyValue(csrfKey))
        {
            return Error("Error! CSRF 3");
        }

        var stored = RefererIsControlPanel(referer)
            ? await PrintSessionKeyAsync(connection, adminSession, adminUser, true, cancellationToken).ConfigureAwait(false)
            : await PrintSessionKeyAsync(connection, session, userCookie, false, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return Error("Error! CSRF 3.1");
        }

        return string.Equals(stored, csrfKey, StringComparison.Ordinal) ? null : Error("Error! CSRF 4");
    }

    /// <summary>PHP <c>(float)$string</c>: the leading decimal number, 0 when there is none.</summary>
    public static double PhpFloatCast(string? raw)
    {
        var match = Regex.Match(raw ?? string.Empty, "^[ \\t\\n\\r\\v\\f]*[+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?");
        return match.Success && double.TryParse(match.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    /// <summary>PHP <c>(string)$float</c> (precision 14, <c>zend_gcvt</c>): exponent form below 1e-4 or past 14 integer digits.</summary>
    public static string PhpFloatString(double value)
    {
        if (double.IsNaN(value))
        {
            return "NAN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "INF" : "-INF";
        }

        if (value == 0)
        {
            return double.IsNegative(value) ? "-0" : "0";
        }

        var e = value.ToString("E13", CultureInfo.InvariantCulture);
        var negative = e[0] == '-';
        var body = negative ? e[1..] : e;
        var mark = body.IndexOf('E', StringComparison.Ordinal);
        var digits = (body[..mark].Replace(".", string.Empty, StringComparison.Ordinal)).TrimEnd('0');
        var exponent = int.Parse(body[(mark + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var decpt = exponent + 1;
        var sb = new StringBuilder(24);
        if (negative)
        {
            sb.Append('-');
        }

        if (decpt < -3 || decpt > 14)
        {
            sb.Append(digits[0]).Append('.').Append(digits.Length > 1 ? digits[1..] : "0").Append('E').Append(exponent < 0 ? '-' : '+')
                .Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture));
        }
        else if (decpt <= 0)
        {
            sb.Append("0.").Append('0', -decpt).Append(digits);
        }
        else if (digits.Length <= decpt)
        {
            sb.Append(digits).Append('0', decpt - digits.Length);
        }
        else
        {
            sb.Append(digits[..decpt]).Append('.').Append(digits[decpt..]);
        }

        return sb.ToString();
    }

    /// <summary>PHP <c>json_encode</c> of a float (<c>serialize_precision = -1</c>): the shortest round trip, integral values keep <c>.0</c>.</summary>
    public static string PhpJsonFloat(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (text.Contains('E', StringComparison.Ordinal))
        {
            var mark = text.IndexOf('E', StringComparison.Ordinal);
            var mantissa = text[..mark];
            var exponent = int.Parse(text[(mark + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return (mantissa.Contains('.', StringComparison.Ordinal) ? mantissa : mantissa + ".0") + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
        }

        return text.Contains('.', StringComparison.Ordinal) ? text : text + ".0";
    }

    /// <summary>PHP <c>round()</c>: half away from zero on the 15-digit decimal value, which absorbs binary noise like PHP's pre-rounding.</summary>
    private static double PhpRound(double value, int places)
        => (double)Math.Round((decimal)value, places, MidpointRounding.AwayFromZero);

    /// <summary>PHP <c>(string)</c> of a decoded JSON scalar: ints as written, floats by precision 14, <c>true</c> → "1", <c>false</c> → "".</summary>
    internal static string PhpJsonScalarString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "1",
            JsonValueKind.False => string.Empty,
            JsonValueKind.Number => value.TryGetInt64(out var whole) ? whole.ToString(CultureInfo.InvariantCulture) : PhpFloatString(value.GetDouble()),
            JsonValueKind.Object or JsonValueKind.Array => "Array",
            _ => string.Empty,
        };

    private static string PhpNumberFormat(double value) => ErpDocumentControlRender.PhpNumberFormat((decimal)value);

    private static string H(string? value) => ErpDocumentControlRender.H(value);

    private static string GatewayTitle(string handler)
    {
        var spaced = handler.Replace('_', ' ');
        return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static string GatewayLang(string langHref)
    {
        var lang = langHref.Trim('/');
        return lang.Length == 0 ? "en" : lang;
    }

    private static string Config(IReadOnlyDictionary<string, string> config, string key) => config.TryGetValue(key, out var value) ? value : string.Empty;

    private static string TrimEndSlash(string value) => value.TrimEnd('/');
}
