using System.Text;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The storefront login page (<c>/users/login</c>, PHP <c>content/users/loginform.php</c>): the auth card around
/// <c>modules/login/login_form_general.php</c> with the password tab (<c>modules/login/pass/app.php</c>), the e-mail
/// code tab (<c>modules/login/epc_code/app.php</c>) and its modal, the provider buttons, the per-template styles and
/// <c>epc_storefront_auth_links_styles()</c>. The output is PHP's byte for byte, CRLF line ends included where the PHP
/// files have them; translations are echoed unescaped as PHP echoes them. The password form posts back to the page
/// (<see cref="StorefrontLoginPostMiddleware"/>).
/// </summary>
public static class StorefrontLoginForm
{
    /// <summary>The translation ids the page prints.</summary>
    public static IReadOnlyList<int> StringIds { get; } = [4008, 5659, 5644, 1312, 4018, 4017, 1311, 4666, 3987, 4667, 5651, 3452, 4668, 3583, 4030, 254, 4669, 5140, 4270, 376, 3996];

    public const string OtpOnSuccess = "if(data.redirect){location.href=data.redirect;}";

    public sealed record Input
    {
        public long UserId { get; init; }

        public string LangHref { get; init; } = "/en/";

        public string CsrfGuardKey { get; init; } = string.Empty;

        public bool Sms { get; init; }

        /// <summary>The provider buttons markup; null when no provider renderer is installed.</summary>
        public string? SocialButtons { get; init; }

        /// <summary>The current front template's <c>templates.id</c>.</summary>
        public long TemplateId { get; init; }

        public string TenantKey { get; init; } = string.Empty;

        public string LoginLabel { get; init; } = "Shop";

        public string LogoUrl { get; init; } = string.Empty;

        public string SendUrl { get; init; } = StorefrontOtpModal.DefaultSendUrl;

        public string VerifyUrl { get; init; } = StorefrontOtpModal.VerifyCodeUrl;

        public StorefrontAuthPartials.AuthLayout Layout { get; init; } = new();

        public StorefrontOtpModal Modal { get; init; } = new();
    }

    private sealed record Method(string Key, string Type, string Caption, string Icon, string Placeholder);

    /// <summary>PHP <c>epc_storefront_auth_lang_href()</c> + <c>users/registration</c>.</summary>
    public static string SignupUrl(string langHref) => langHref.TrimEnd('/') + "/users/registration";

    public static string Render(Input input, Func<int, string> t)
    {
        var lang = input.LangHref;
        if (input.UserId > 0)
        {
            return "<div class=\"alert alert-info\">You are already signed in. <a href=\"" + H(lang.TrimEnd('/') + "/users/profile") + "\">Go to profile</a></div>";
        }

        var heading = t(4008);
        var sb = new StringBuilder();
        sb.Append(input.Layout.Open())
            .Append("<div class=\"panel panel-primary epc-login-page\">\n\t<div class=\"panel-heading\">")
            .Append(H(AuthEmailOtp.PhpEmpty(heading) ? "Login" : heading))
            .Append("</div>\n\t<div class=\"panel-body\">\n\t\t<p class=\"help-block\">Sign in to browse, track orders, and checkout. New customer? <a href=\"")
            .Append(H(SignupUrl(lang)))
            .Append("\"><strong>Sign up</strong></a> — retail accounts are approved instantly and can see prices; wholesale requires manager approval before prices and stock details unlock.</p>\n\t\t")
            .Append(General(input, t, "login_page_1", lang.TrimEnd('/') + "/"))
            .Append("\t</div>\n</div>\n")
            .Append(StorefrontAuthPartials.AuthLayout.Close())
            .Append(AuthLinksStyles);
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_storefront_auth_links_styles()</c>.</summary>
    public const string AuthLinksStyles = "<style>"
        + ".epc-auth-header-links{display:inline-flex;align-items:center;gap:8px;white-space:nowrap;font-weight:700}"
        + ".epc-auth-header-links__group{display:inline-flex;align-items:center;gap:5px}"
        + ".epc-auth-header-links__icon,.epc-auth-header-links__group > .fa{color:#ef4444;margin-right:2px;font-size:14px;line-height:1;width:1em;text-align:center}"
        + ".epc-auth-header-links__sep{color:rgba(255,255,255,.35);font-weight:400;padding:0 2px}"
        + ".epc-auth-header-links__slash{color:rgba(255,255,255,.45);padding:0 1px;font-weight:600}"
        + ".epc-auth-header-links a{text-decoration:none;font-weight:700;color:inherit}"
        + ".epc-auth-header-links a:hover{color:#fff;text-decoration:underline}"
        + ".epc-auth-header-links__vendor-register,.epc-auth-header-links__signup{color:#fda4af}"
        + ".epc-er-utility__actions .epc-auth-header-links{margin-left:6px}"
        + "@media(max-width:991px){.epc-auth-header-links{flex-wrap:wrap;white-space:normal;row-gap:4px}}"
        + "</style>";

    /// <summary>
    /// The <c>$login_form_postfix</c> PHP builds for the <paramref name="count"/>-th form on a page:
    /// <paramref name="postfix"/> (empty when unset), <c>_</c>, the count.
    /// </summary>
    public static string Postfix(string? postfix, int count) => (postfix ?? string.Empty) + "_" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// PHP <c>modules/login/login_form_general.php</c> for a built <paramref name="postfix"/> (see <see cref="Postfix"/>):
    /// the sign-in tabs for a visitor (the login page variant when the postfix starts with <c>login_page</c>), else the
    /// account links and the logout form. <paramref name="target"/> is <c>$login_form_target</c>, null when unset.
    /// The e-mail code tab is always offered, as on a deployment that has the shared auth core.
    /// </summary>
    public static string General(Input input, Func<int, string> t, string postfix, string? target)
    {
        var page = postfix.StartsWith("login_page", StringComparison.Ordinal);
        var sb = new StringBuilder();
        if (input.UserId > 0)
        {
            var lang = input.LangHref;
            sb.Append(Crlf(
                "\t<div class=\"panel-heading\">" + t(3452) + "</div>\n"
                + "\t<div class=\"panel-body\" style=\"color:#777;\">\n"
                + "\t\t<form method=\"POST\" name=\"auth_form" + postfix + "\">\n"
                + "\t\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + input.CsrfGuardKey + "\" />\n"
                + "\t\t\t<input type=\"hidden\" name=\"logout\" value=\"true\"/>\n"
                + "\t\t\t<div class=\"form-group\">\n"
                + "\t\t\t\t<a href=\"" + lang + "/users/profile\" class=\"btn btn-ar btn-success btn_profile\" style=\"color:#FFF;\">" + t(4668) + "</a>\n\n"
                + "\t\t\t\t<hr class=\"dotted margin-10\">\n\t\t\t\t\n"
                + "\t\t\t\t<a href=\"" + lang + "/shop/orders\" class=\"btn btn-ar btn-warning btn_orders\" style=\"color:#FFF; \">" + t(3583) + "</a>\n"
                + "                <a href=\"" + lang + "/shop/returns/returns_list\" class=\"btn btn-ar btn-warning btn_return\" style=\"color:#FFF;\">" + t(4030) + "</a>\n"
                + "\t\t\t\t<a href=\"" + lang + "/shop/cart\" class=\"btn btn-ar btn-warning btn_cart\" style=\"color:#FFF;\">" + t(254) + "</a>\n"
                + "\t\t\t\t<a href=\"" + lang + "/garazh\" class=\"btn btn-ar btn-warning btn_garazh\" style=\"color:#FFF;\">" + t(4669) + "</a>\n"
                + "\t\t\t\t<a href=\"" + lang + "/requests\" class=\"btn btn-ar btn-warning btn_requests\" style=\"color:#FFF;\">" + t(5140) + "</a>\n"
                + "\t\t\t\t<a href=\"" + lang + "/garazh/bloknot?garage=0\" class=\"btn btn-ar btn-warning btn_bloknot\" style=\"color:#FFF;\">" + t(4270) + "</a>\n"
                + "\t\t\t\t<a href=\"" + lang + "/shop/balans\" class=\"btn btn-ar btn-warning btn_balans\" style=\"color:#FFF;\">" + t(376) + "</a>\n"
                + "\t\t\t\t\n\t\t\t\t<hr class=\"dotted margin-10\">\n\t\t\t\t\n"
                + "\t\t\t\t<a href=\"javascript:void(0);\" onclick=\"forms['auth_form" + postfix + "'].submit();\" class=\"btn btn-ar btn-danger btn_exit\" style=\"color:#FFF;\">" + t(3996) + "</a>\n"
                + "\t\t\t\t\n\t\t\t\t<div class=\"clearfix\"></div>\n"
                + "\t\t\t</div>\n\t\t</form>\n\t</div>\n"));
            return sb.Append(Styles(input)).ToString();
        }

        var tabs = new (string Key, string Caption)[] { ("pass", t(5659)), ("epc_code", "Email code") };
        var social = string.Empty;
        if (!string.IsNullOrEmpty(input.SocialButtons))
        {
            social = "<div class=\"epc-social-top\" style=\"margin:6px 0 10px\">" + input.SocialButtons + "<div class=\"epc-social-divider\"><span>Or</span></div></div>";
        }

        sb.Append("\t\t<div class=\"row login_form").Append(page ? " login_form--page" : string.Empty)
            .Append("\">\r\n\t\t<div class=\"col-lg-12\">\r\n\t\t\r\n\t\t\t\t\t")
            .Append(page ? string.Empty : "\t<div class=\"panel-heading\">" + t(5651) + "</div>\r\n\t\t\t\t\t")
            .Append("\r\n\t\t\r\n\t\t\t\r\n\t\t\t")
            .Append(social)
            .Append("\r\n\t\t\t<!-- Nav tabs auth -->\r\n\t\t\t<ul class=\"nav nav-tabs \" style=\"padding: 0px; margin: 0; margin-top: -2px; background: #f1f3f4;\">\r\n\t\t\t\t");
        for (var i = 0; i < tabs.Length; i++)
        {
            sb.Append("\t\t\t\t\t<li style=\"border-radius: 0; border-left: 0; border-right: 0;\" class=\"")
                .Append(i == 0 ? "active" : string.Empty)
                .Append("\"><a style=\"padding: 3px 15px; margin: 0;\" href=\"#auth_type_tab_")
                .Append(tabs[i].Key).Append('_').Append(postfix)
                .Append("\" data-toggle=\"tab\">").Append(tabs[i].Caption).Append("</a></li>\r\n\t\t\t\t\t");
        }

        sb.Append("\t\t\t</ul>\r\n\t\t\t\r\n\t\t\t<!-- Tab panes auth -->\r\n\t\t\t<div class=\"tab-content\" style=\"padding: 0px;\">\r\n\t\t\t\t");
        for (var i = 0; i < tabs.Length; i++)
        {
            sb.Append("\t\t\t\t\t<div class=\"tab-pane ").Append(i == 0 ? "active" : string.Empty)
                .Append("\" id=\"auth_type_tab_").Append(tabs[i].Key).Append('_').Append(postfix)
                .Append("\">\r\n\t\t\t\t\t\t<div class=\"row\">\r\n\t\t\t\t\t\t\t<div class=\"col-lg-12\">\r\n\t\t\t\t\t\t\t\t")
                .Append(i == 0 ? PassTab(input, t, postfix, target ?? string.Empty, page) : CodeTab(input, postfix))
                .Append("\t\t\t\t\t\t\t</div>\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t</div>\r\n\t\t\t\t\t");
        }

        sb.Append("\t\t\t</div>\r\n\t\t\r\n\t\t\r\n\t\t</div>\r\n\t</div>\r\n").Append(Styles(input));
        return sb.ToString();
    }

    private static string Styles(Input input)
    {
        const string N = "\r\n\r\n\r\n\r\n\r\n";
        var sb = new StringBuilder();
        sb.Append(GeneralStyles)
            .Append(N)
            .Append(input.TemplateId == 59 ? ExpanStyles : string.Empty)
            .Append(N)
            .Append(input.TemplateId == 61 ? Limo61Styles : string.Empty)
            .Append("\r\n")
            .Append(CodeTabStyles)
            .Append(N)
            .Append(input.TemplateId == 62 ? Limo62Styles : string.Empty)
            .Append("\r\n")
            .Append(CodeTabStyles)
            .Append("\r\n\r\n\r\n\r\n");
        return sb.ToString();
    }

    private static string PassTab(Input input, Func<int, string> t, string postfix, string target, bool page)
    {
        var methods = new List<Method>();
        if (input.Sms)
        {
            methods.Add(new Method("phone", "phone", t(1312), "fa fa-phone", t(4018)));
        }

        methods.Add(new Method("email", "email", "E-mail", "fa fa-envelope", t(4017)));
        var singleEmail = methods.Count == 1;
        var formId = "auth_form_pass_" + postfix;
        var js = "pass_" + postfix;
        var lang = input.LangHref;

        var sb = new StringBuilder();
        sb.Append(Crlf(
            "\n\n\n\n\n<div class=\"panel-body no-auth epc-pass-login\">\n\n\t<form method=\"POST\" name=\"" + formId + "\" id=\"" + formId + "\">\n\n\t\t\n\n"
            + "\t\t<input type=\"hidden\" name=\"form_name\" value=\"auth_form" + postfix + "\" />\n\n"
            + "\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + input.CsrfGuardKey + "\" />\n\n"
            + "\t\t<input type=\"hidden\" name=\"authentication\" value=\"true\"/>\n\n"
            + "\t\t<input type=\"hidden\" name=\"auth_contact\" value=\"\" id=\"auth_contact_" + js + "\"/>\n\n"
            + "\t\t<input type=\"hidden\" name=\"auth_contact_type\" value=\"\" id=\"auth_contact_type_" + js + "\"/>\n\n\t\t\n"
            + "\t\t<input type=\"hidden\" name=\"target\" value=\"" + target + "\"/>\n\n\t\t\n\n"
            + "\t\t<div class=\"form-group\">\n\n\t\t\t\n\n\t\t\t\n\t\t\t"));

        if (!singleEmail)
        {
            var select = new StringBuilder();
            select.Append("<!-- Селектор контакта для аутентификации -->\n\n\t\t\t<div class=\"epc-pass-auth-method-select\">\n\n\t\t\t\t<div class=\"input-group login-input\">\n\n")
                .Append("\t\t\t\t\t<span style=\"padding-left: 3px; padding-right: 2px;\" class=\"input-group-addon\"><small>").Append(t(5644)).Append("</small></span>\n\n")
                .Append("\t\t\t\t\t<select name=\"auth_contact_type\" class=\"form-control\" id=\"auth_contact_select_").Append(js)
                .Append("\" onchange=\"onChangeAuthMethod_").Append(js)
                .Append("();\" style=\"height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555; padding-left: 8px;\">\n\n\t\t\t\t\t\t");
            foreach (var method in methods)
            {
                select.Append("\n\t\t\t\t\t\t<option value=\"").Append(method.Key).Append("\">").Append(method.Caption).Append("</option>\n\n\t\t\t\t\t\t");
            }

            select.Append("\n\t\t\t\t\t</select>\n\n\t\t\t\t</div>\n\n\t\t\t\t<br/>\n\n\t\t\t</div>");
            sb.Append(Crlf(select.ToString()));
        }
        else
        {
            sb.Append("<input type=\"hidden\" value=\"email\" id=\"auth_contact_select_").Append(js).Append("\" data-epc-pass-method=\"email\" />");
        }

        var body = new StringBuilder("\n\n\t\t\t\n\t\t\t\n\n\t\t\t\n");
        for (var i = 0; i < methods.Count; i++)
        {
            var method = methods[i];
            var email = method.Type == "email";
            body.Append("\t\t\t<div id=\"wrapper_pass_").Append(method.Key).Append('_').Append(postfix)
                .Append("\" class=\"input-group login-input epc-pass-contact-wrap").Append(singleEmail || i == 0 ? string.Empty : " hidden").Append("\">\n\n")
                .Append("\t\t\t\t<span class=\"input-group-addon\"><i class=\"").Append(method.Icon).Append("\"></i></span>\n\n")
                .Append("\t\t\t\t<input style=\"height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;\" type=\"").Append(email ? "email" : "text")
                .Append("\" class=\"form-control\" placeholder=\"").Append(method.Placeholder).Append("\" name=\"").Append(method.Type)
                .Append("\" id=\"auth_contact_input_pass_").Append(method.Key).Append('_').Append(postfix).Append('"')
                .Append(email ? " required autocomplete=\"email\"" : " autocomplete=\"tel\"").Append(" />\n\n\t\t\t</div>\n\n\t\t\t\n");
        }

        body.Append("\t\t\t\n\n\t\t\t<div class=\"input-group login-input epc-pass-password-wrap\">\n\n")
            .Append("\t\t\t\t<span class=\"input-group-addon\"><i style=\"padding: 0px 2px 0px 3px;\" class=\"fa fa-lock\"></i></span>\n\n")
            .Append("\t\t\t\t<input style=\"height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;\" type=\"password\" class=\"form-control\" placeholder=\"")
            .Append(t(1311)).Append("\" name=\"password\" autocomplete=\"current-password\" required />\n\n\t\t\t</div>\n\n\t\t\t\n\n")
            .Append("\t\t\t<div class=\"checkbox\">\n\n\t\t\t\t<input type=\"checkbox\" id=\"checkbox_remember_").Append(js).Append("\" name=\"rememberme\" />\n\n")
            .Append("\t\t\t\t<label for=\"checkbox_remember_").Append(js).Append("\">").Append(t(4666)).Append("</label>\n\n\t\t\t</div>\n\n\t\t\t\n\n")
            .Append("\t\t\t<a href=\"javascript:void(0);\" onclick=\"onAuthFormSubmit_").Append(js)
            .Append("();\" class=\"btn btn-ar btn-primary btn_auth\" style=\"color:#FFF;\">").Append(t(4008)).Append("</a>\n\n\t\t\t\n")
            .Append(page ? "\t\t\t<a href=\"" + lang + "/users/registration\" class=\"btn btn-ar btn-success btn_reg\" style=\"color:#FFF;\">" + t(3987) + "</a>\n\n\t\t\t\n" : string.Empty)
            .Append("\t\t\t\n\n")
            .Append("\t\t\t<hr class=\"dotted margin-10\">\n\n\t\t\t\n\n")
            .Append("\t\t\t<a href=\"").Append(lang).Append("/users/forgot_password\" class=\"btn btn-ar btn-warning btn_forget\" style=\"color:#FFF;\">").Append(t(4667)).Append("</a>\n\n\t\t\t\n\n")
            .Append("\t\t\t<div class=\"clearfix\"></div>\n\n\t\t\t\n\n\t\t</div>\n\n\t\t\n\n\t</form>\n\n</div>\n\n\n\n\n\n\n\n")
            .Append("<script>\n\n(function(){\n\n")
            .Append("\tvar formId = ").Append(OAuthStart.PhpJsonString(formId)).Append(";\n\n")
            .Append("\tvar jsSuffix = ").Append(OAuthStart.PhpJsonString(js)).Append(";\n\n")
            .Append("\tvar singleEmail = ").Append(singleEmail ? "true" : "false").Append(";\n\n")
            .Append("\tvar methods = [").Append(string.Join(',', methods.Select(m => OAuthStart.PhpJsonString(m.Key)))).Append("];\n\n")
            .Append("\tvar methodTypes = {").Append(string.Join(',', methods.Select(m => OAuthStart.PhpJsonString(m.Key) + ":" + OAuthStart.PhpJsonString(m.Type)))).Append("};")
            .Append(PassScript.Replace("\n", "\n\n", StringComparison.Ordinal));
        sb.Append(Crlf(body.ToString()));
        return sb.ToString();
    }

    private static string CodeTab(Input input, string postfix)
    {
        var uid = "epc_sf_" + new string(postfix.Select(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' ? c : '_').ToArray());
        var modalId = uid + "_otpm";
        var label = H(input.LoginLabel);
        var eid = H(uid);
        return "<div class=\"epc-cp-auth-modern epc-storefront-auth\" id=\"" + eid + "\">\n"
            + "\t<p class=\"epc-cp-auth-hint\">Enter your email — we&rsquo;ll send a 6-digit code. New customers are registered automatically.</p>\n"
            + "\t<div class=\"form-group\">\n"
            + "\t\t<input type=\"email\" class=\"form-control\" id=\"" + eid + "_email\"\n"
            + "\t\t\tautocomplete=\"email\" placeholder=\"your@email.com\" />\n"
            + "\t</div>\n"
            + "\t<button type=\"button\" class=\"btn btn-ar btn-block epc-continue-email\" id=\"" + eid + "_send\">\n"
            + "\t\tContinue with Email\n"
            + "\t</button>\n"
            + "\t<style>.epc-continue-email{background:#111827;border-color:#111827;color:#fff;font-weight:600}.epc-continue-email:hover,.epc-continue-email:focus{background:#000;border-color:#000;color:#fff}</style>\n"
            + "\t<p class=\"epc-cp-auth-msg\" id=\"" + eid + "_msg\" aria-live=\"polite\"></p>\n"
            + "</div>\n\n"
            + input.Modal.Render(new StorefrontOtpModal.Options
            {
                ModalId = modalId,
                Context = "storefront",
                TenantKey = input.TenantKey,
                SendUrl = input.SendUrl,
                VerifyUrl = input.VerifyUrl,
                ReturnUrl = input.LangHref.TrimEnd('/') + "/",
                LogoUrl = input.LogoUrl,
                Label = label,
                OnSuccess = OtpOnSuccess,
            })
            + "<script>\n(function(){\nvar uid=" + OAuthStart.PhpJsonString(uid) + ";\nvar modalId=" + OAuthStart.PhpJsonString(modalId) + ";\n"
            + CodeScript;
    }

    private static string Crlf(string text) => text.Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string H(string value) => StorefrontSupplierLpoNotifier.H(value);

    private const string PassScript = """


	function wrapperEl(key) {
		return document.getElementById('wrapper_pass_' + key + '_' + jsSuffix.replace(/^pass_/, ''));
	}

	window['onChangeAuthMethod_' + jsSuffix] = function()
	{
		if (singleEmail) {
			return;
		}
		var select = document.getElementById('auth_contact_select_' + jsSuffix);
		if (!select) {
			return;
		}
		var method = select.value || 'email';
		methods.forEach(function(key) {
			var wrap = wrapperEl(key);
			if (!wrap) {
				return;
			}
			if (method === key) {
				wrap.classList.remove('hidden');
			} else {
				wrap.classList.add('hidden');
			}
		});
	};

	window['onAuthFormSubmit_' + jsSuffix] = function()
	{
		var form = document.forms[formId] || document.getElementById(formId);
		if (!form) {
			return;
		}
		var activeKey = singleEmail ? 'email' : (document.getElementById('auth_contact_select_' + jsSuffix).value || 'email');
		var wrap = wrapperEl(activeKey);
		var input = wrap ? wrap.querySelector('input') : null;
		var contactHidden = document.getElementById('auth_contact_' + jsSuffix);
		var typeHidden = document.getElementById('auth_contact_type_' + jsSuffix);
		if (input && contactHidden) {
			contactHidden.value = input.value;
		}
		if (typeHidden && methodTypes[activeKey]) {
			typeHidden.value = methodTypes[activeKey];
		}
		if (typeof form.reportValidity === 'function' && !form.reportValidity()) {
			return;
		}
		form.submit();
	};

	function initPassLogin() {
		if (!singleEmail) {
			window['onChangeAuthMethod_' + jsSuffix]();
		}
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', initPassLogin);
	} else {
		initPassLogin();
	}
})();
</script>


""";

    private const string CodeScript = """
var sendBtn=document.getElementById(uid+'_send');
var emailIn=document.getElementById(uid+'_email');
var msgEl=document.getElementById(uid+'_msg');

function showMsg(t,ok){
	if(msgEl){msgEl.textContent=t;msgEl.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}
}

if(sendBtn){
	sendBtn.addEventListener('click',function(){
		var email=(emailIn||{}).value||'';
		email=email.trim();
		if(!email||!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)){
			showMsg('Please enter a valid email address.',false);
			return;
		}
		showMsg('',true);
		if(window.EpcOtpModal&&window.EpcOtpModal[modalId]){
			window.EpcOtpModal[modalId].open(email);
		}
	});
}
if(emailIn){
	emailIn.addEventListener('keydown',function(e){
		if(e.key==='Enter'){e.preventDefault();if(sendBtn)sendBtn.click();}
	});
}
})();
</script>

""";

    private static readonly string GeneralStyles = Crlf("""
<style>
.no-auth select
{
	border-radius:0 !important;
}
.auth-contact-methods-header
{
	background: none;
	color: #999;
	border: 1px solid #999;
	border-radius: 3px;
	margin: 0px 5px;
	text-decoration: none;
	padding: 2px 20px;
	cursor: pointer;
}
.auth-contact-methods-header.active
{
	border: 1px solid #555555;
	background: #555555;
	color: #fff;
}

.login_form li > a:hover{
	background:none;
	border-radius:0;
}

@media (max-width: 767px)
{
	.login_form .nav-tabs:before{
		display:none;
	}
	.login_form .tab-content .input-group {
		background: none;
		border: none;
		border-radius: 0 !important;
		padding-left: 0px; 
		padding-right: 0px; 
		
		position: relative;
		display: table;
		border-collapse: separate;
	}
	.login_form .btn-ar {
		margin-bottom: 3px;
	}
}
</style>

""");

    private static readonly string ExpanStyles = Crlf("""
<style>
.login_form .nav-tabs + .tab-content
{
	padding: 0px;
    border: none;
}
.dropdown-search-box, .dropdown-login-box{
	padding: 0;
}
.panel-heading 
{
    background: #fff;
}
.dropdown-menu .active > a{
	background:#fff;
}
.login_form .panel-body {
    padding: 15px;
    background: #fff;
}
.login_form .tab-content>.active {
    display: block;
    background: #fff;
}
</style>
<script>
	$(document).on('click', '.dropdown-menu', function (e) {
		e.stopPropagation();// Что бы окно формы входа не закрывалась при клике или смене типа авторизации
	});
</script>

""");

    private static readonly string Limo61Styles = Crlf("""
<style>
.login_form .nav-tabs + .tab-content
{
	padding: 0px;
	padding: 15px !important;
    border: 1px solid #ddd;
	border-top:0;
	position: relative;
    top: -1px;
}
</style>

""");

    private static readonly string Limo62Styles = Crlf("""
<style>
.header-user-box .login_form .nav-tabs + .tab-content
{
	padding-top: 15px !important;
}
.login_form .nav-tabs + .tab-content
{
	padding: 0px;
	border:0;
}
.login_form .nav-tabs li a
{
	border:0 !important;
}
</style>
<script>
	$(document).on('click', '.dropdown-menu', function (e) {
		e.stopPropagation();
	});
</script>

""");

    private static readonly string CodeTabStyles = Crlf("""
<style>
.epc-cp-auth-modern{margin-bottom:12px}
.epc-cp-auth-tabs{display:flex;gap:6px;margin-bottom:10px;flex-wrap:wrap}
.epc-cp-auth-tab{flex:1;min-width:70px;border:1px solid #ddd;background:#f5f5f5;font-size:12px;font-weight:600;padding:6px 8px;border-radius:6px;cursor:pointer}
.epc-cp-auth-tab.is-active{background:#fff;border-color:#c0392b;color:#c0392b}
.epc-cp-auth-pane{display:none}
.epc-cp-auth-pane.is-active{display:block}
.epc-cp-auth-hint{font-size:12px;color:#777;margin:0 0 8px}
.epc-cp-auth-google{background:#fff;border:1px solid #dadce0;color:#3c4043;font-weight:600;margin-bottom:6px}
.epc-cp-auth-google.is-disabled{opacity:.6;cursor:not-allowed}
.epc-cp-auth-msg{font-size:12px;margin:8px 0 0;min-height:1.2em}
.epc-cp-auth-msg.is-ok{color:#1a7f6e}
.epc-cp-auth-msg.is-err{color:#c0392b}
</style>

""");
}
