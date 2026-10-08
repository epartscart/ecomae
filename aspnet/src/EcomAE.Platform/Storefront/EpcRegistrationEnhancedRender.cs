using System.Text;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The render half of the enhanced registration form: the e-mail code and social sign-up panel, the country select,
/// the Retail / Wholesale tabs with the wholesale KYC fields, and their tab, country, TRN and client-validation script.
/// The output is PHP's byte for byte; the save half is <see cref="EpcRegistrationEnhanced"/>.
/// </summary>
public static class EpcRegistrationEnhancedRender
{
    public const string SocialUid = "epc_reg_auth";

    /// <summary>
    /// PHP <c>epc_reg_render_social_block()</c>. <paramref name="tradeName"/> is the site trade name (null when the site
    /// context is not loaded); <paramref name="loginLabel"/>, <paramref name="tenantKey"/> and the two URLs come from the
    /// storefront login context (null = key missing); <paramref name="socialButtons"/> is the rendered provider buttons.
    /// Empty when the modern auth core is unavailable.
    /// </summary>
    public static string SocialBlock(
        bool authAvailable,
        string? langHref,
        string? tenantKey,
        string? tradeName,
        string? loginLabel,
        string socialButtons,
        string? sendCodeUrl,
        string? verifyCodeUrl)
    {
        if (!authAvailable)
        {
            return string.Empty;
        }

        var returnUrl = (langHref ?? "/en/").TrimEnd('/') + "/";
        tenantKey ??= string.Empty;
        var siteName = tradeName is null ? string.Empty : AuthEmailOtp.PhpTrim(tradeName);
        if (siteName.Length == 0)
        {
            siteName = loginLabel ?? "our store";
        }

        var sendUrl = sendCodeUrl ?? StorefrontOtpModal.DefaultSendUrl;
        var verifyUrl = verifyCodeUrl ?? StorefrontOtpModal.VerifyCodeUrl;
        var sb = new StringBuilder();
        sb.Append("\t<div class=\"panel panel-default epc-reg-social-panel\" style=\"margin-bottom:14px\">\n");
        sb.Append("\t\t<div class=\"panel-body epc-cp-auth-modern\">\n");
        sb.Append("\t\t\t<p class=\"epc-reg-auth-title\" style=\"font-size:18px;font-weight:700;color:#111;margin:0 0 4px;text-align:center\">Register and join ");
        sb.Append(H(siteName));
        sb.Append("</p>\n");
        sb.Append("\t\t\t<p class=\"help-block\" style=\"margin-top:0;text-align:center\">Quick sign-in with email code or social — or complete the form below for retail (instant) or wholesale (subject to approval only).</p>\n");
        sb.Append("\t\t\t");
        if (socialButtons.Length > 0)
        {
            sb.Append(socialButtons);
            sb.Append("<div class=\"epc-social-divider\"><span>Or</span></div>");
        }
        sb.Append("\t\t\t<div class=\"epc-reg-email-otp\" id=\"");
        sb.Append(SocialUid);
        sb.Append("\" data-tenant-key=\"");
        sb.Append(H(tenantKey));
        sb.Append("\" style=\"margin-top:4px\">\n");
        sb.Append("\t\t\t\t<div class=\"form-group\" style=\"margin-bottom:8px\">\n");
        sb.Append("\t\t\t\t\t<input type=\"email\" class=\"form-control\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_email\" autocomplete=\"email\" placeholder=\"you@example.com\" />\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<input type=\"button\" class=\"btn btn-ar btn-block epc-continue-email\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_send\" value=\"Continue with Email\" />\n");
        sb.Append("\t\t\t\t<div class=\"form-group epc-cp-auth-code-wrap\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_codewrap\" style=\"display:none;margin-top:12px;margin-bottom:8px\">\n");
        sb.Append("\t\t\t\t\t<input type=\"text\" class=\"form-control\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_code\" inputmode=\"numeric\" maxlength=\"6\" autocomplete=\"one-time-code\" placeholder=\"6-digit code\" />\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<input type=\"button\" class=\"btn btn-ar btn-success btn-block\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_verify\" style=\"display:none;margin-top:8px\" value=\"Verify &amp; sign in\" />\n");
        sb.Append("\t\t\t\t<p class=\"epc-cp-auth-msg\" id=\"");
        sb.Append(SocialUid);
        sb.Append("_msg\" aria-live=\"polite\"></p>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t<style>.epc-continue-email{background:#111827;border-color:#111827;color:#fff;font-weight:600}.epc-continue-email:hover,.epc-continue-email:focus{background:#000;border-color:#000;color:#fff}</style>\n");
        sb.Append("\t<script>\n");
        sb.Append("\t(function(){\n");
        sb.Append("\t\tvar root=document.getElementById(");
        sb.Append(Json(SocialUid));
        sb.Append(");\n");
        sb.Append("\t\tif(!root)return;\n");
        sb.Append("\t\tvar tenantKey=root.getAttribute('data-tenant-key')||'';\n");
        sb.Append("\t\tvar sendUrl=");
        sb.Append(Json(sendUrl));
        sb.Append(",verifyUrl=");
        sb.Append(Json(verifyUrl));
        sb.Append(";\n");
        sb.Append("\t\tvar returnUrl=");
        sb.Append(Json(returnUrl));
        sb.Append(";\n");
        sb.Append("\t\tvar msg=document.getElementById(");
        sb.Append(Json(SocialUid + "_msg"));
        sb.Append(");\n");
        sb.Append("\t\tvar codeWrap=document.getElementById(");
        sb.Append(Json(SocialUid + "_codewrap"));
        sb.Append(");\n");
        sb.Append("\t\tvar verifyBtn=document.getElementById(");
        sb.Append(Json(SocialUid + "_verify"));
        sb.Append(");\n");
        sb.Append("\t\tfunction showMsg(t,ok){if(msg){msg.textContent=t;msg.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}}\n");
        sb.Append("\t\tdocument.getElementById(");
        sb.Append(Json(SocialUid + "_send"));
        sb.Append(").addEventListener('click',function(){\n");
        sb.Append("\t\t\tvar em=(document.getElementById(");
        sb.Append(Json(SocialUid + "_email"));
        sb.Append(")||{}).value||'';\n");
        sb.Append("\t\t\tshowMsg('Sending…',true);\n");
        sb.Append("\t\t\tfetch(sendUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})\n");
        sb.Append("\t\t\t.then(function(r){return r.json();}).then(function(d){showMsg(d.message||'',!!d.ok);if(d.ok){codeWrap.style.display='';verifyBtn.style.display='';}}).catch(function(){showMsg('Network error',false);});\n");
        sb.Append("\t\t});\n");
        sb.Append("\t\tverifyBtn.addEventListener('click',function(){\n");
        sb.Append("\t\t\tvar em=(document.getElementById(");
        sb.Append(Json(SocialUid + "_email"));
        sb.Append(")||{}).value||'';\n");
        sb.Append("\t\t\tvar code=(document.getElementById(");
        sb.Append(Json(SocialUid + "_code"));
        sb.Append(")||{}).value||'';\n");
        sb.Append("\t\t\tshowMsg('Verifying…',true);\n");
        sb.Append("\t\t\tfetch(verifyUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,code:code,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})\n");
        sb.Append("\t\t\t.then(function(r){return r.json();}).then(function(d){if(d.ok&&d.redirect){location.href=d.redirect;return;}showMsg(d.message||'Error',!!d.ok);}).catch(function(){showMsg('Network error',false);});\n");
        sb.Append("\t\t});\n");
        sb.Append("\t})();\n");
        sb.Append("\t</script>\n");
        sb.Append("\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_reg_render_country_select()</c>.</summary>
    public static string CountrySelect(string id = "epc_reg_country", string selected = "", string name = "")
    {
        if (name.Length == 0)
        {
            name = id;
        }

        var sb = new StringBuilder();
        sb.Append("\t<select name=\"");
        sb.Append(H(name));
        sb.Append("\" id=\"");
        sb.Append(H(id));
        sb.Append("\" class=\"form-control epc-reg-country\">\n");
        sb.Append("\t\t<option value=\"\">— Select country —</option>\n");
        sb.Append("\t\t");
        foreach (var (code, label) in EpcCountries.RegistrationOptions)
        {
            sb.Append("\t\t<option value=\"");
            sb.Append(H(code));
            sb.Append("\"");
            sb.Append(selected == code ? " selected=\"selected\"" : string.Empty);
            sb.Append(">");
            sb.Append(H(label));
            sb.Append("</option>\n");
            sb.Append("\t\t");
        }
        sb.Append("\t</select>\n");
        sb.Append("\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_reg_render_account_tabs()</c>, which ends with <see cref="TabScripts"/>.</summary>
    public static string AccountTabs()
    {
        var sb = new StringBuilder();
        sb.Append("\t<input type=\"hidden\" name=\"epc_customer_type\" id=\"epc_customer_type_field\" value=\"retail\" />\n");
        sb.Append("\t<div class=\"panel panel-primary epc-reg-account-panel\">\n");
        sb.Append("\t\t<div class=\"panel-heading\">Customer type</div>\n");
        sb.Append("\t\t<div class=\"panel-body\">\n");
        sb.Append("\t\t\t<p class=\"help-block\" style=\"margin-top:0;\">Fields marked <strong>*</strong> are mandatory. <strong>Retail customer</strong> — short form (name, phone, address); approved immediately; no KYC documents. <strong>Wholesale customer</strong> — <em>subject to approval only</em>; company details plus <em>Additional information</em> (KYC / AML documents) required; pending until a manager approves trade pricing and currency in the Control Panel.</p>\n");
        sb.Append("\t\t\t<ul class=\"nav nav-tabs epc-reg-type-tabs\" role=\"tablist\">\n");
        sb.Append("\t\t\t\t<li role=\"presentation\" class=\"active\"><a href=\"#epc_reg_tab_retail\" aria-controls=\"epc_reg_tab_retail\" role=\"tab\" data-toggle=\"tab\" data-epc-type=\"retail\">Retail customer</a></li>\n");
        sb.Append("\t\t\t\t<li role=\"presentation\"><a href=\"#epc_reg_tab_wholesale\" aria-controls=\"epc_reg_tab_wholesale\" role=\"tab\" data-toggle=\"tab\" data-epc-type=\"wholesale\">Wholesale customer <span style=\"font-weight:600;opacity:.85\">(subject to approval only)</span></a></li>\n");
        sb.Append("\t\t\t</ul>\n");
        sb.Append("\t\t\t<div class=\"tab-content epc-reg-type-panes\" style=\"padding-top:14px;\">\n");
        sb.Append("\t\t\t\t<div role=\"tabpanel\" class=\"tab-pane active\" id=\"epc_reg_tab_retail\">\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_first_name\" class=\"col-sm-4 col-lg-3 control-label\">First name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-retail-req\" name=\"epc_retail_first_name\" id=\"epc_retail_first_name\" maxlength=\"80\" placeholder=\"Given name\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_last_name\" class=\"col-sm-4 col-lg-3 control-label\">Last name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-retail-req\" name=\"epc_retail_last_name\" id=\"epc_retail_last_name\" maxlength=\"80\" placeholder=\"Family name\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_mobile\" class=\"col-sm-4 col-lg-3 control-label\">Mobile phone*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"tel\" class=\"form-control epc-reg-retail-req epc-reg-phone\" name=\"epc_retail_mobile\" id=\"epc_retail_mobile\" maxlength=\"30\" placeholder=\"e.g. +971501234567\" data-epc-phone-for=\"epc_retail_country\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_country\" class=\"col-sm-4 col-lg-3 control-label\">Country*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">");
        sb.Append(CountrySelect("epc_retail_country", string.Empty, "epc_retail_country"));
        sb.Append("</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_city\" class=\"col-sm-4 col-lg-3 control-label\">City*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-retail-req\" name=\"epc_retail_city\" id=\"epc_retail_city\" maxlength=\"80\" placeholder=\"City\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_address\" class=\"col-sm-4 col-lg-3 control-label\">Delivery address*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-retail-req\" name=\"epc_retail_address\" id=\"epc_retail_address\" maxlength=\"255\" placeholder=\"Street, building, area\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group epc-reg-retail-emirate-row\" id=\"epc_retail_emirate_row\" style=\"display:none\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_emirate\" class=\"col-sm-4 col-lg-3 control-label\">Emirate</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<select name=\"epc_retail_emirate\" id=\"epc_retail_emirate\" class=\"form-control\">\n");
        sb.Append("\t\t\t\t\t\t\t\t");
        foreach (var em in EpcCountries.UaeEmirates)
        {
            sb.Append("\t\t\t\t\t\t\t\t<option value=\"");
            sb.Append(H(em));
            sb.Append("\">");
            sb.Append(H(em));
            sb.Append("</option>\n");
            sb.Append("\t\t\t\t\t\t\t\t");
        }
        sb.Append("\t\t\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\" id=\"epc_retail_state_row\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_state\" class=\"col-sm-4 col-lg-3 control-label\" id=\"epc_retail_state_label\">State / region</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_retail_state\" id=\"epc_retail_state\" maxlength=\"80\" placeholder=\"Emirate, province, or region\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_retail_postal\" class=\"col-sm-4 col-lg-3 control-label\">Postal / ZIP code</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_retail_postal\" id=\"epc_retail_postal\" maxlength=\"20\" placeholder=\"e.g. 00000\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label class=\"col-sm-4 col-lg-3 control-label\">Notifications</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<label class=\"checkbox-inline\"><input type=\"checkbox\" name=\"epc_retail_sms_notify\" value=\"1\" /> Receive order updates via SMS</label>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<div role=\"tabpanel\" class=\"tab-pane\" id=\"epc_reg_tab_wholesale\">\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_company\" class=\"col-sm-4 col-lg-3 control-label\">Company name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_company\" id=\"epc_wholesale_company\" maxlength=\"200\" placeholder=\"Registered business name\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_legal_name\" class=\"col-sm-4 col-lg-3 control-label\">Legal entity name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_legal_name\" id=\"epc_wholesale_legal_name\" maxlength=\"200\" placeholder=\"As on trade licence\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_first_name\" class=\"col-sm-4 col-lg-3 control-label\">Contact first name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_first_name\" id=\"epc_wholesale_first_name\" maxlength=\"80\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_last_name\" class=\"col-sm-4 col-lg-3 control-label\">Contact last name*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_last_name\" id=\"epc_wholesale_last_name\" maxlength=\"80\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_job_title\" class=\"col-sm-4 col-lg-3 control-label\">Contact job title*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_job_title\" id=\"epc_wholesale_job_title\" maxlength=\"80\" placeholder=\"e.g. Purchasing manager\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_mobile\" class=\"col-sm-4 col-lg-3 control-label\">Mobile phone*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"tel\" class=\"form-control epc-reg-wholesale-req epc-reg-phone\" name=\"epc_wholesale_mobile\" id=\"epc_wholesale_mobile\" maxlength=\"30\" placeholder=\"e.g. +971501234567\" data-epc-phone-for=\"epc_wholesale_country\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_country\" class=\"col-sm-4 col-lg-3 control-label\">Country*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">");
        sb.Append(CountrySelect("epc_wholesale_country", string.Empty, "epc_wholesale_country"));
        sb.Append("</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_city\" class=\"col-sm-4 col-lg-3 control-label\">City*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_city\" id=\"epc_wholesale_city\" maxlength=\"80\" value=\"Dubai\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_address\" class=\"col-sm-4 col-lg-3 control-label\">Business address*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_address\" id=\"epc_wholesale_address\" maxlength=\"255\" placeholder=\"Street, building, PO box\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_postal\" class=\"col-sm-4 col-lg-3 control-label\">Postal / ZIP code</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_wholesale_postal\" id=\"epc_wholesale_postal\" maxlength=\"20\" placeholder=\"e.g. 00000\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_business_type\" class=\"col-sm-4 col-lg-3 control-label\">Business type*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<select name=\"epc_wholesale_business_type\" id=\"epc_wholesale_business_type\" class=\"form-control epc-reg-wholesale-req\">\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"\">— Select —</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"distributor\">Distributor / wholesaler</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"retailer\">Retailer / reseller</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"workshop\">Workshop / garage</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"fleet\">Fleet / transport</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"manufacturer\">Manufacturer</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"other\">Other</option>\n");
        sb.Append("\t\t\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group epc-reg-uae-emirate-row\" id=\"epc_wholesale_emirate_row\" style=\"display:none\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_emirate\" class=\"col-sm-4 col-lg-3 control-label\">Emirate*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<select name=\"epc_wholesale_emirate\" id=\"epc_wholesale_emirate\" class=\"form-control\">\n");
        sb.Append("\t\t\t\t\t\t\t\t");
        foreach (var em in EpcCountries.UaeEmirates)
        {
            sb.Append("\t\t\t\t\t\t\t\t<option value=\"");
            sb.Append(H(em));
            sb.Append("\">");
            sb.Append(H(em));
            sb.Append("</option>\n");
            sb.Append("\t\t\t\t\t\t\t\t");
        }
        sb.Append("\t\t\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\" id=\"epc_wholesale_trn_block\">\n");
        sb.Append("\t\t\t\t\t\t<label class=\"col-sm-4 col-lg-3 control-label\">Tax registration (TRN)</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<p class=\"help-block\" style=\"margin-top:0;\" id=\"epc_trn_help\">UAE companies must provide a 15-digit TRN. Other countries may enter TRN or mark as not available.</p>\n");
        sb.Append("\t\t\t\t\t\t\t<div id=\"epc_trn_uae_only\" style=\"display:none\">\n");
        sb.Append("\t\t\t\t\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"epc_wholesale_trn\" id=\"epc_wholesale_trn\" maxlength=\"15\" placeholder=\"e.g. 100123456700003\" />\n");
        sb.Append("\t\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t\t\t<div id=\"epc_trn_non_uae\" style=\"display:none\">\n");
        sb.Append("\t\t\t\t\t\t\t\t<label class=\"radio-inline\" style=\"margin-right:14px;\"><input type=\"radio\" name=\"epc_reg_trn_mode\" value=\"has_trn\" /> I have a TRN / VAT number</label>\n");
        sb.Append("\t\t\t\t\t\t\t\t<label class=\"radio-inline\"><input type=\"radio\" name=\"epc_reg_trn_mode\" value=\"not_available\" /> Not available</label>\n");
        sb.Append("\t\t\t\t\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"epc_wholesale_trn_optional\" id=\"epc_wholesale_trn_optional\" maxlength=\"20\" placeholder=\"TRN / VAT number\" style=\"margin-top:8px;display:none\" />\n");
        sb.Append("\t\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_trade_licence\" class=\"col-sm-4 col-lg-3 control-label\">Trade licence no.*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control epc-reg-wholesale-req\" name=\"epc_wholesale_trade_licence\" id=\"epc_wholesale_trade_licence\" maxlength=\"60\" placeholder=\"Commercial / trade licence number\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_wholesale_website\" class=\"col-sm-4 col-lg-3 control-label\">Company website</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"url\" class=\"form-control\" name=\"epc_wholesale_website\" id=\"epc_wholesale_website\" maxlength=\"200\" placeholder=\"https://example.com\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label class=\"col-sm-4 col-lg-3 control-label\">Notifications</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<label class=\"checkbox-inline\"><input type=\"checkbox\" name=\"epc_wholesale_sms_notify\" value=\"1\" checked=\"checked\" /> Receive order updates via SMS</label>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\n");
        sb.Append("\t\t\t\t\t<hr style=\"margin:18px 0 12px;border-top:1px solid #e5e7eb;\" />\n");
        sb.Append("\t\t\t\t\t<p class=\"help-block\" style=\"margin:0 0 4px;font-size:15px;font-weight:700;color:#0f172a;\">Additional information (wholesale only)</p>\n");
        sb.Append("\t\t\t\t\t<p class=\"help-block\" style=\"margin:0 0 12px;\"><strong>KYC / AML &amp; e-invoice documents</strong> — required for wholesale approval under UAE compliance practice. Not shown for retail customers. PDF or image, max 8&nbsp;MB each.</p>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_emirates_id_no\" class=\"col-sm-4 col-lg-3 control-label\">Emirates ID no.</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_emirates_id_no\" id=\"epc_emirates_id_no\" maxlength=\"20\" placeholder=\"784-XXXX-XXXXXXX-X\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_authorized_signatory\" class=\"col-sm-4 col-lg-3 control-label\">Authorized signatory</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_authorized_signatory\" id=\"epc_authorized_signatory\" maxlength=\"120\" placeholder=\"Full name as on Emirates ID / passport\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_authorized_signatory_id\" class=\"col-sm-4 col-lg-3 control-label\">Signatory ID no.</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_authorized_signatory_id\" id=\"epc_authorized_signatory_id\" maxlength=\"40\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_ubo_name\" class=\"col-sm-4 col-lg-3 control-label\">Ultimate beneficial owner</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_ubo_name\" id=\"epc_ubo_name\" maxlength=\"160\" placeholder=\"UBO full name (25%+ ownership)\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_pep_declaration\" class=\"col-sm-4 col-lg-3 control-label\">PEP declaration*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t\t<select name=\"epc_pep_declaration\" id=\"epc_pep_declaration\" class=\"form-control epc-reg-wholesale-req\">\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"\">— Select —</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"No\">No — not a politically exposed person</option>\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"Yes\">Yes — PEP / related to a PEP</option>\n");
        sb.Append("\t\t\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_source_of_funds\" class=\"col-sm-4 col-lg-3 control-label\">Source of funds</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"epc_source_of_funds\" id=\"epc_source_of_funds\" maxlength=\"255\" placeholder=\"Business income, investment, etc.\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_doc_trade_licence\" class=\"col-sm-4 col-lg-3 control-label\">Trade licence scan*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"file\" class=\"form-control epc-reg-wholesale-file\" name=\"epc_doc_trade_licence\" id=\"epc_doc_trade_licence\" accept=\".pdf,.jpg,.jpeg,.png,.webp\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_doc_emirates_id\" class=\"col-sm-4 col-lg-3 control-label\">Emirates ID copy*</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"file\" class=\"form-control epc-reg-wholesale-file\" name=\"epc_doc_emirates_id\" id=\"epc_doc_emirates_id\" accept=\".pdf,.jpg,.jpeg,.png,.webp\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_doc_vat_certificate\" class=\"col-sm-4 col-lg-3 control-label\">VAT / TRN certificate</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"file\" class=\"form-control\" name=\"epc_doc_vat_certificate\" id=\"epc_doc_vat_certificate\" accept=\".pdf,.jpg,.jpeg,.png,.webp\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t\t<label for=\"epc_ubo_id_document\" class=\"col-sm-4 col-lg-3 control-label\">UBO ID document</label>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\"><input type=\"file\" class=\"form-control\" name=\"epc_ubo_id_document\" id=\"epc_ubo_id_document\" accept=\".pdf,.jpg,.jpeg,.png,.webp\" /></div>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t");
        sb.Append(TabScripts());
        sb.Append("\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_reg_render_tab_scripts()</c>.</summary>
    public static string TabScripts()
    {
        var sb = new StringBuilder();
        sb.Append("\t<style>\n");
        sb.Append("\t.epc-reg-type-tabs{margin-bottom:0}\n");
        sb.Append("\t.epc-reg-type-tabs>li>a{font-weight:600}\n");
        sb.Append("\t.epc-reg-account-panel .tab-pane .form-group:last-child{margin-bottom:0}\n");
        sb.Append("\t</style>\n");
        sb.Append("\t<script>\n");
        sb.Append("\tfunction epcRegActiveType(){\n");
        sb.Append("\t\tvar hf=document.getElementById('epc_customer_type_field');\n");
        sb.Append("\t\treturn (hf&&hf.value)?hf.value:'retail';\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegSyncCountryHidden(){\n");
        sb.Append("\t\tvar t=epcRegActiveType();\n");
        sb.Append("\t\tvar sel=document.getElementById(t==='wholesale'?'epc_wholesale_country':'epc_retail_country');\n");
        sb.Append("\t\tvar hid=document.getElementById('epc_reg_country_sync');\n");
        sb.Append("\t\tif(sel&&hid){hid.value=sel.value||'';}\n");
        sb.Append("\t}\n");
        sb.Append("\tvar epcRegDialMap=");
        sb.Append(DialCodesJson());
        sb.Append(";\n");
        sb.Append("\tvar epcRegAddrMeta=");
        sb.Append(AddressMetaJson());
        sb.Append(";\n");
        sb.Append("\tfunction epcRegDialPrefix(cc){return epcRegDialMap[cc]?'+'+epcRegDialMap[cc]:'';}\n");
        sb.Append("\tfunction epcRegAddrFor(cc){return epcRegAddrMeta[cc]||epcRegAddrMeta.default;}\n");
        sb.Append("\tfunction epcRegPhoneHint(countryId,phoneId){\n");
        sb.Append("\t\tvar cc=(document.getElementById(countryId)||{}).value||'';\n");
        sb.Append("\t\tvar ph=document.getElementById(phoneId);\n");
        sb.Append("\t\tif(!ph)return;\n");
        sb.Append("\t\tvar p=epcRegDialPrefix(cc);\n");
        sb.Append("\t\tph.placeholder=p?('e.g. '+p+'501234567'):'Include country code e.g. +971501234567';\n");
        sb.Append("\t\tif(p&&!(ph.value||'').trim()){ph.value=p+' ';}\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegRetailCountryUi(){\n");
        sb.Append("\t\tvar cc=(document.getElementById('epc_retail_country')||{}).value||'';\n");
        sb.Append("\t\tvar meta=epcRegAddrFor(cc);\n");
        sb.Append("\t\tvar emRow=document.getElementById('epc_retail_emirate_row');\n");
        sb.Append("\t\tvar stRow=document.getElementById('epc_retail_state_row');\n");
        sb.Append("\t\tvar stLbl=document.getElementById('epc_retail_state_label');\n");
        sb.Append("\t\tif(emRow)emRow.style.display=meta.use_emirate_select?'':'none';\n");
        sb.Append("\t\tif(stRow)stRow.style.display=meta.use_emirate_select?'none':'';\n");
        sb.Append("\t\tif(stLbl)stLbl.textContent=meta.state_label||'State / region';\n");
        sb.Append("\t\tepcRegPhoneHint('epc_retail_country','epc_retail_mobile');\n");
        sb.Append("\t\tepcRegSyncCountryHidden();\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegWholesaleTrnUi(){\n");
        sb.Append("\t\tvar country=(document.getElementById('epc_wholesale_country')||{}).value||'';\n");
        sb.Append("\t\tvar uae=document.getElementById('epc_trn_uae_only');\n");
        sb.Append("\t\tvar non=document.getElementById('epc_trn_non_uae');\n");
        sb.Append("\t\tvar emRow=document.getElementById('epc_wholesale_emirate_row');\n");
        sb.Append("\t\tvar help=document.getElementById('epc_trn_help');\n");
        sb.Append("\t\tif(country==='AE'){\n");
        sb.Append("\t\t\tif(uae)uae.style.display='';\n");
        sb.Append("\t\t\tif(non)non.style.display='none';\n");
        sb.Append("\t\t\tif(emRow)emRow.style.display='';\n");
        sb.Append("\t\t\tif(help)help.textContent='UAE B2B: 15-digit TRN is mandatory for e-invoicing.';\n");
        sb.Append("\t\t}else{\n");
        sb.Append("\t\t\tif(uae)uae.style.display='none';\n");
        sb.Append("\t\t\tif(non)non.style.display='';\n");
        sb.Append("\t\t\tif(emRow)emRow.style.display='none';\n");
        sb.Append("\t\t\tif(help)help.textContent='Enter your TRN/VAT number if you have one, or select Not available.';\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\tepcRegPhoneHint('epc_wholesale_country','epc_wholesale_mobile');\n");
        sb.Append("\t\tepcRegSyncCountryHidden();\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegTrnOptionalToggle(){\n");
        sb.Append("\t\tvar mode=document.querySelector('input[name=\"epc_reg_trn_mode\"]:checked');\n");
        sb.Append("\t\tvar inp=document.getElementById('epc_wholesale_trn_optional');\n");
        sb.Append("\t\tif(!inp)return;\n");
        sb.Append("\t\tinp.style.display=(mode&&mode.value==='has_trn')?'':'none';\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegSetPaneEnabled(pane, enabled){\n");
        sb.Append("\t\tif(!pane)return;\n");
        sb.Append("\t\tvar nodes=pane.querySelectorAll('input, select, textarea, button');\n");
        sb.Append("\t\tfor(var i=0;i<nodes.length;i++){\n");
        sb.Append("\t\t\tvar el=nodes[i];\n");
        sb.Append("\t\t\tif(enabled){\n");
        sb.Append("\t\t\t\tif(el.getAttribute('data-epc-was-disabled')==='1'){\n");
        sb.Append("\t\t\t\t\tel.removeAttribute('disabled');\n");
        sb.Append("\t\t\t\t\tel.removeAttribute('data-epc-was-disabled');\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t}else{\n");
        sb.Append("\t\t\t\tif(!el.disabled){\n");
        sb.Append("\t\t\t\t\tel.setAttribute('data-epc-was-disabled','1');\n");
        sb.Append("\t\t\t\t\tel.disabled=true;\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t}\n");
        sb.Append("\t\t}\n");
        sb.Append("\t}\n");
        sb.Append("\tfunction epcRegSyncTabFields(){\n");
        sb.Append("\t\tvar t=epcRegActiveType();\n");
        sb.Append("\t\tvar retail=document.getElementById('epc_reg_tab_retail');\n");
        sb.Append("\t\tvar wholesale=document.getElementById('epc_reg_tab_wholesale');\n");
        sb.Append("\t\tepcRegSetPaneEnabled(retail, t==='retail');\n");
        sb.Append("\t\tepcRegSetPaneEnabled(wholesale, t==='wholesale');\n");
        sb.Append("\t\tepcRegSyncCountryHidden();\n");
        sb.Append("\t}\n");
        sb.Append("\t(function(){\n");
        sb.Append("\t\tvar form=document.getElementById('regform');\n");
        sb.Append("\t\tif(form){ form.setAttribute('novalidate','novalidate'); }\n");
        sb.Append("\t\tvar tabs=document.querySelectorAll('.epc-reg-type-tabs a[data-epc-type]');\n");
        sb.Append("\t\ttabs.forEach(function(tab){\n");
        sb.Append("\t\t\ttab.addEventListener('shown.bs.tab',function(){\n");
        sb.Append("\t\t\t\tvar hf=document.getElementById('epc_customer_type_field');\n");
        sb.Append("\t\t\t\tif(hf)hf.value=tab.getAttribute('data-epc-type')||'retail';\n");
        sb.Append("\t\t\t\tepcRegSyncTabFields();\n");
        sb.Append("\t\t\t});\n");
        sb.Append("\t\t});\n");
        sb.Append("\t\tvar wc=document.getElementById('epc_wholesale_country');\n");
        sb.Append("\t\tif(wc){wc.addEventListener('change',epcRegWholesaleTrnUi);}\n");
        sb.Append("\t\tdocument.querySelectorAll('input[name=\"epc_reg_trn_mode\"]').forEach(function(r){\n");
        sb.Append("\t\t\tr.addEventListener('change',epcRegTrnOptionalToggle);\n");
        sb.Append("\t\t});\n");
        sb.Append("\t\tvar rc=document.getElementById('epc_retail_country');\n");
        sb.Append("\t\tif(rc){rc.addEventListener('change',epcRegRetailCountryUi);}\n");
        sb.Append("\t\tepcRegRetailCountryUi();\n");
        sb.Append("\t\tepcRegWholesaleTrnUi();\n");
        sb.Append("\t\tepcRegTrnOptionalToggle();\n");
        sb.Append("\t\tepcRegSyncTabFields();\n");
        sb.Append("\t})();\n");
        sb.Append("\tfunction epcRegClientValidate(){\n");
        sb.Append("\t\tepcRegSyncTabFields();\n");
        sb.Append("\t\tvar t=epcRegActiveType();\n");
        sb.Append("\t\tvar labels={epc_retail_first_name:'First name',epc_retail_last_name:'Last name',epc_retail_mobile:'Mobile phone',epc_retail_city:'City',epc_retail_address:'Delivery address',epc_wholesale_company:'Company name',epc_wholesale_legal_name:'Legal entity name',epc_wholesale_first_name:'Contact first name',epc_wholesale_last_name:'Contact last name',epc_wholesale_job_title:'Contact job title',epc_wholesale_mobile:'Mobile phone',epc_wholesale_city:'City',epc_wholesale_address:'Business address',epc_wholesale_business_type:'Business type',epc_wholesale_trade_licence:'Trade licence no.'};\n");
        sb.Append("\t\tvar ids=t==='wholesale'?['epc_wholesale_company','epc_wholesale_legal_name','epc_wholesale_first_name','epc_wholesale_last_name','epc_wholesale_job_title','epc_wholesale_mobile','epc_wholesale_city','epc_wholesale_address','epc_wholesale_business_type','epc_wholesale_trade_licence']:['epc_retail_first_name','epc_retail_last_name','epc_retail_mobile','epc_retail_city','epc_retail_address'];\n");
        sb.Append("\t\tfor(var i=0;i<ids.length;i++){\n");
        sb.Append("\t\t\tvar el=document.getElementById(ids[i]);\n");
        sb.Append("\t\t\tif(!el||!String(el.value||'').trim()){alert('Please fill in: '+(labels[ids[i]]||ids[i]));if(el){try{el.focus();}catch(e){}}return false;}\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\tvar countryEl=document.getElementById(t==='wholesale'?'epc_wholesale_country':'epc_retail_country');\n");
        sb.Append("\t\tif(!countryEl||!countryEl.value){alert('Please select your country.');if(countryEl){try{countryEl.focus();}catch(e){}}return false;}\n");
        sb.Append("\t\tif(t==='wholesale'){\n");
        sb.Append("\t\t\tif(countryEl.value==='AE'){\n");
        sb.Append("\t\t\t\tvar trn=(document.getElementById('epc_wholesale_trn')||{}).value||'';\n");
        sb.Append("\t\t\t\ttrn=trn.replace(/\\D/g,'');\n");
        sb.Append("\t\t\t\tif(trn.length!==15){alert('UAE TRN must be 15 digits.');return false;}\n");
        sb.Append("\t\t\t}else{\n");
        sb.Append("\t\t\t\tvar mode=document.querySelector('#epc_reg_tab_wholesale input[name=\"epc_reg_trn_mode\"]:checked');\n");
        sb.Append("\t\t\t\tif(!mode){alert('Please choose TRN status: enter TRN or Not available.');return false;}\n");
        sb.Append("\t\t\t\tif(mode.value==='has_trn'){\n");
        sb.Append("\t\t\t\t\tvar opt=(document.getElementById('epc_wholesale_trn_optional')||{}).value||'';\n");
        sb.Append("\t\t\t\t\tif(!opt.trim()){alert('Please enter your TRN / VAT number.');return false;}\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t}\n");
        sb.Append("\t\t\tvar pep=document.getElementById('epc_pep_declaration');\n");
        sb.Append("\t\t\tif(!pep||!String(pep.value||'').trim()){alert('Please complete the PEP declaration.');if(pep){try{pep.focus();}catch(e){}}return false;}\n");
        sb.Append("\t\t\tvar tlFile=document.getElementById('epc_doc_trade_licence');\n");
        sb.Append("\t\t\tvar eidFile=document.getElementById('epc_doc_emirates_id');\n");
        sb.Append("\t\t\tif(!tlFile||!tlFile.files||!tlFile.files.length){alert('Please upload your trade licence scan.');if(tlFile){try{tlFile.focus();}catch(e){}}return false;}\n");
        sb.Append("\t\t\tif(!eidFile||!eidFile.files||!eidFile.files.length){alert('Please upload your Emirates ID copy.');if(eidFile){try{eidFile.focus();}catch(e){}}return false;}\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\tepcRegSyncCountryHidden();\n");
        sb.Append("\t\treturn true;\n");
        sb.Append("\t}\n");
        sb.Append("\t</script>\n");
        sb.Append("\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_reg_render_uae_panel()</c>: a no-op since the UAE fields moved into the wholesale tab.</summary>
    public static string UaePanel() => string.Empty;

    private static string H(string value) => StorefrontSupplierLpoNotifier.H(value);

    private static string Json(string value) => OAuthStart.PhpJsonString(value);

    private static string DialCodesJson()
        => "{" + string.Join(",", EpcCountries.DialCodes.Select(d => Json(d.Key) + ":" + Json(d.Value))) + "}";

    private static string AddressMetaJson()
        => "{" + string.Join(",", new[] { ("AE", "AE"), ("US", "US"), ("GB", "GB"), ("default", string.Empty) }
            .Select(p => Json(p.Item1) + ":" + MetaJson(EpcCountries.AddressMeta(p.Item2)))) + "}";

    private static string MetaJson(EpcCountryAddressMeta meta)
        => "{\"state_label\":" + Json(meta.StateLabel)
            + ",\"postal_label\":" + Json(meta.PostalLabel)
            + ",\"postal_required\":" + (meta.PostalRequired ? "true" : "false")
            + ",\"use_emirate_select\":" + (meta.UseEmirateSelect ? "true" : "false")
            + ",\"emirates\":[" + string.Join(",", meta.Emirates.Select(Json)) + "]}";
}
