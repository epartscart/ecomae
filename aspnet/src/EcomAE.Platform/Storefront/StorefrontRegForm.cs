using System.Text;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The storefront registration page (<c>/users/registration</c>): the e-mail code and social panel, the variant
/// selector, contact and password fields, the Retail / Wholesale tabs, the CMS additional fields script, the captcha,
/// the user agreement, the e-mail code modal and the submit checks. The form posts to <c>{lang}/users/register</c>.
/// The output is PHP's byte for byte; <see cref="StorefrontRegFormLoader"/> reads its inputs.
/// </summary>
public static class StorefrontRegForm
{
    /// <summary>A <c>reg_fields</c> row with <c>main_flag = 0</c>, echoed raw as PHP does; caption and example already translated.</summary>
    public sealed record AdditionalField(
        string MainFlag,
        string Name,
        string Caption,
        string ShowFor,
        string RequiredFor,
        string Maxlen,
        string Regexp,
        string WidgetType,
        string WidgetOptions,
        string Example);

    /// <summary>A <c>reg_variants</c> row; the caption is translated only when there is more than one variant.</summary>
    public sealed record Variant(string Id, string Caption);

    public sealed record Input
    {
        public bool LoggedIn { get; init; }

        public IReadOnlyList<AdditionalField> AdditionalFields { get; init; } = [];

        public IReadOnlyList<Variant> Variants { get; init; } = [];

        public StorefrontPhpAjax.Communications Communications { get; init; } = new(false, false);

        public string LangHref { get; init; } = string.Empty;

        public string CsrfGuardKey { get; init; } = string.Empty;

        public string SocialBlock { get; init; } = string.Empty;

        public bool Enhanced { get; init; } = true;

        public string UsersAgreement { get; init; } = string.Empty;

        public string OtpModal { get; init; } = string.Empty;

        public string MinPasswordLen { get; init; } = string.Empty;

        public string DomainPath { get; init; } = string.Empty;

        public StorefrontAuthPartials.AuthLayout Layout { get; init; } = new();
    }

    /// <summary>The <c>translate_str_by_id()</c> ids the page prints.</summary>
    public static IReadOnlyList<int> StringIds { get; } =
        [4740, 4716, 3925, 4741, 4742, 1312, 2318, 1311, 3927, 3928, 4067, 4743, 3933, 3934, 3935, 3885, 3930, 3893, 3931, 4744, 4070];

    /// <summary>The <c>on_success</c> script of the registration e-mail code modal: mark verified, then submit the form.</summary>
    public const string OtpOnSuccess = "\n\t\t\twindow.epcRegOtpVerified = true;\n\t\t\twindow.epcRegOtpEmail = data.verified_email || currentEmail;\n\t\t\tvar hf = document.getElementById(\"epc_reg_otp_verified_field\");\n\t\t\tif (hf) hf.value = \"1\";\n\t\t\tvar rf = document.getElementById(\"regform\");\n\t\t\tif (rf) rf.submit();\n\t\t";

    /// <summary>The modal's tenant key: the site key lower-cased (ASCII), keeping only <c>a-z 0-9 _</c>.</summary>
    public static string OtpTenantKey(string? siteKey)
        => new(Auth.AuthEmailOtp.PhpLower(siteKey ?? string.Empty).Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_').ToArray());

    public static StorefrontOtpModal.Options OtpModalOptions(string tenantKey, string logoUrl) => new()
    {
        ModalId = "epc_reg_otp",
        Context = "storefront",
        TenantKey = tenantKey,
        SendUrl = StorefrontOtpModal.DefaultSendUrl,
        VerifyUrl = StorefrontOtpModal.VerifyOnlyUrl,
        LogoUrl = logoUrl,
        Label = "epartscart",
        VerifyOnly = true,
        OnSuccess = OtpOnSuccess,
    };

    public static string Render(Input input, Func<int, string> t)
    {
        var sb = new StringBuilder();
        if (input.LoggedIn)
        {
            return t(4740);
        }

        sb.Append(input.Layout.Open("wide"));
        sb.Append("\n");
        sb.Append("    ");
        sb.Append("    <script>\n");
        sb.Append("    var reg_fields = new Array();//Массив с объектами всех полей\n");
        sb.Append("    ");
        foreach (var f in input.AdditionalFields)
        {
            sb.Append("        reg_fields[reg_fields.length] = new Object();//Создаем новый объект поля. И инициализируем его поля:\n");
            sb.Append("        reg_fields[reg_fields.length - 1].main_flag = ");
            sb.Append(f.MainFlag);
            sb.Append(";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].name = \"");
            sb.Append(f.Name);
            sb.Append("\";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].caption = \"");
            sb.Append(f.Caption);
            sb.Append("\";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].show_for = ");
            sb.Append(f.ShowFor);
            sb.Append(";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].required_for = ");
            sb.Append(f.RequiredFor);
            sb.Append(";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].maxlen = ");
            sb.Append(f.Maxlen);
            sb.Append(";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].regexp = \"");
            sb.Append(f.Regexp);
            sb.Append("\";\n");
            sb.Append("\t\treg_fields[reg_fields.length - 1].widget_type = \"");
            sb.Append(f.WidgetType);
            sb.Append("\";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].widget_options = ");
            sb.Append(f.WidgetOptions);
            sb.Append(";\n");
            sb.Append("        reg_fields[reg_fields.length - 1].value_buffer = \"\";//Текущее значения - для сохранения при переключении регистрационных вариантов\n");
            sb.Append("\t\treg_fields[reg_fields.length - 1].example = \"");
            sb.Append(f.Example);
            sb.Append("\";\n");
            sb.Append("        ");
        }
        sb.Append("    </script>\n");
        sb.Append("    \n");
        sb.Append("\n");
        sb.Append("    ");
        sb.Append(input.SocialBlock);
        sb.Append("\n");
        sb.Append("    <!-- Start ФОРМА РЕГИСТРАЦИИ -->\n");
        sb.Append("    <form action=\"");
        sb.Append(input.LangHref);
        sb.Append("/users/register\" id=\"regform\" onsubmit=\"return onSubmitCheck();\" method=\"post\" enctype=\"multipart/form-data\" novalidate=\"novalidate\">\n");
        sb.Append("\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"");
        sb.Append(input.CsrfGuardKey);
        sb.Append("\" />\n");
        sb.Append("        <!--Блок для выбора Регистрационного Варианта-->\n");
        sb.Append("        <div id=\"RegVariantsSelector\">\n");
        sb.Append("    \t\t");
        if (input.Variants.Count == 1)
        {
            var variant = input.Variants[0];
            sb.Append("                <select id=\"reg_variant_selector\" name=\"reg_variant\" style=\"display:none\" onchange=\"regenerateFields();\">\n");
            sb.Append("                    <option value=\"");
            sb.Append(variant.Id);
            sb.Append("\">");
            sb.Append(variant.Caption);
            sb.Append("</option>\n");
            sb.Append("                </select>\n");
            sb.Append("                ");
        }
        else
        {
            sb.Append("\t\t\t\t<div class=\"panel panel-primary\">\n");
            sb.Append("                    <div class=\"panel-heading\">");
            sb.Append(t(4716));
            sb.Append("</div>\n");
            sb.Append("                    <div class=\"panel-body\">\n");
            sb.Append("\t\t\t\t\t\t  <div class=\"form-group\">\n");
            sb.Append("\t\t\t\t\t\t\t<select id=\"reg_variant_selector\" name=\"reg_variant\" onchange=\"regenerateFields();\" class=\"form-control\" />\n");
            sb.Append("\t\t\t\t\t\t\t");
            foreach (var variant in input.Variants)
            {
                sb.Append("\t\t\t\t\t\t\t\t<option value=\"");
                sb.Append(variant.Id);
                sb.Append("\">");
                sb.Append(variant.Caption);
                sb.Append("</option>\n");
                sb.Append("\t\t\t\t\t\t\t\t");
            }
            sb.Append("\t\t\t\t\t\t\t</select>\n");
            sb.Append("\t\t\t\t\t\t  </div>\n");
            sb.Append("                    </div>\n");
            sb.Append("                </div> <!-- panel panel-primary -->\n");
            sb.Append("                ");
        }
        sb.Append("        </div>\n");
        sb.Append("        \n");
        sb.Append("        \n");
        sb.Append("        ");
        sb.Append("\t\t<div class=\"panel panel-primary\">\n");
        sb.Append("\t\t\t<div class=\"panel-heading\">");
        sb.Append(t(3925));
        sb.Append("</div>\n");
        sb.Append("\t\t\t<div class=\"panel-body\">\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t");
        var displayContactSelect = " style=\"display:none;\" ";
        var contactOptions = "<option value=\"phone\">Телефон</option> <option value=\"email\">E-mail</option>";
        if (input.Communications.All)
        {
            displayContactSelect = string.Empty;
        }
        else if (input.Communications.Sms)
        {
            contactOptions = "<option value=\"phone\">Телефон</option>";
        }
        else
        {
            contactOptions = "<option value=\"email\">E-mail</option>";
        }

        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t<!-- Селектор контакта для регистрации -->\n");
        sb.Append("\t\t\t\t<div class=\"form-group\" ");
        sb.Append(displayContactSelect);
        sb.Append(">\n");
        sb.Append("\t\t\t\t\t<label for=\"reg_contact_select\" class=\"col-sm-4 col-lg-3 control-label\">");
        sb.Append(t(4741));
        sb.Append("</label>\n");
        sb.Append("\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t<select name=\"reg_contact_type\" class=\"form-control\" id=\"reg_contact_select\" onchange=\"on_reg_contact_select_changed();\">\n");
        sb.Append("\t\t\t\t\t\t\t");
        sb.Append(contactOptions);
        sb.Append("\t\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<div class=\"col-sm-12\"></div>\n");
        sb.Append("\t\t\t\t<!-- Поле для контакта -->\n");
        sb.Append("\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t<label for=\"reg_contact_input\" class=\"col-sm-4 col-lg-3 control-label\" id=\"reg_contact_label\"></label>\n");
        sb.Append("\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t<input type=\"text\" name=\"reg_contact\" class=\"form-control\" id=\"reg_contact_input\" placeholder=\"\" />\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<script>\n");
        sb.Append("\t\t\t\t//Обработка выбора контакта\n");
        sb.Append("\t\t\t\tfunction on_reg_contact_select_changed()\n");
        sb.Append("\t\t\t\t{\n");
        sb.Append("\t\t\t\t\tif( document.getElementById(\"reg_contact_select\").value == \"email\" )\n");
        sb.Append("\t\t\t\t\t{\n");
        sb.Append("\t\t\t\t\t\tdocument.getElementById(\"reg_contact_label\").innerHTML = \"E-mail*\";\n");
        sb.Append("\t\t\t\t\t\tdocument.getElementById(\"reg_contact_input\").setAttribute(\"placeholder\", \"");
        sb.Append(t(4742));
        sb.Append("\");\n");
        sb.Append("\t\t\t\t\t}\n");
        sb.Append("\t\t\t\t\telse\n");
        sb.Append("\t\t\t\t\t{\n");
        sb.Append("\t\t\t\t\t\tdocument.getElementById(\"reg_contact_label\").innerHTML = \"");
        sb.Append(t(1312));
        sb.Append("*\";\n");
        sb.Append("\t\t\t\t\t\tdocument.getElementById(\"reg_contact_input\").setAttribute(\"placeholder\", \"");
        sb.Append(t(2318));
        sb.Append(", 9005556677\");\n");
        sb.Append("\t\t\t\t\t}\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t\ton_reg_contact_select_changed();\n");
        sb.Append("\t\t\t\t</script>\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\n");
        sb.Append("\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t<label for=\"password\" class=\"col-sm-4 col-lg-3 control-label\">");
        sb.Append(t(1311));
        sb.Append("*</label>\n");
        sb.Append("\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t\t<input type=\"password\" name=\"password\" class=\"form-control\" id=\"password\" placeholder=\"");
        sb.Append(t(1311));
        sb.Append("\" />\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t<div class=\"form-group\">\n");
        sb.Append("\t\t\t\t\t<label for=\"password_repeat\" class=\"col-sm-4 col-lg-3 control-label\">");
        sb.Append(t(3927));
        sb.Append("*</label>\n");
        sb.Append("\t\t\t\t\t<div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">\n");
        sb.Append("\t\t\t\t\t  <input type=\"password\" class=\"form-control\" name=\"password_repeat\" id=\"password_repeat\" value=\"\" placeholder=\"");
        sb.Append(t(3927));
        sb.Append("\">\n");
        sb.Append("\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t\t\n");
        sb.Append("\t\t\n");
        sb.Append("\t\t");
        if (input.Enhanced)
        {
            sb.Append(EpcRegistrationEnhancedRender.AccountTabs());
        }
        else
        {
            sb.Append("\t\t<div class=\"panel panel-primary\">\n");
            sb.Append("\t\t\t<div class=\"panel-heading\">Account type</div>\n");
            sb.Append("\t\t\t<div class=\"panel-body\">\n");
            sb.Append("\t\t\t\t<div class=\"form-group\">\n");
            sb.Append("\t\t\t\t\t<label class=\"radio-inline\" style=\"margin-right:18px;\">\n");
            sb.Append("\t\t\t\t\t\t<input type=\"radio\" name=\"epc_customer_type\" value=\"retail\" checked=\"checked\" /> Retail customer\n");
            sb.Append("\t\t\t\t\t</label>\n");
            sb.Append("\t\t\t\t\t<label class=\"radio-inline\">\n");
            sb.Append("\t\t\t\t\t\t<input type=\"radio\" name=\"epc_customer_type\" value=\"wholesale\" /> Wholesale customer (subject to approval only)\n");
            sb.Append("\t\t\t\t\t</label>\n");
            sb.Append("\t\t\t\t\t<p class=\"help-block\" style=\"margin:8px 0 0;\">Wholesale accounts are subject to approval only — a manager must approve trade pricing before checkout unlocks.</p>\n");
            sb.Append("\t\t\t\t</div>\n");
            sb.Append("\t\t\t</div>\n");
            sb.Append("\t\t</div>\n");
            sb.Append("\t\t");
        }
        sb.Append("\t\t<input type=\"hidden\" name=\"epc_reg_country\" id=\"epc_reg_country_sync\" value=\"\" />\n");
        sb.Append("\t\t<input type=\"hidden\" name=\"epc_email_otp_verified\" id=\"epc_reg_otp_verified_field\" value=\"\" />\n");
        sb.Append("\t\t\n");
        sb.Append("\t\t\n");
        sb.Append("        \n");
        sb.Append("        <!-- Блок для дополнительных полей (hidden for retail when enhanced tabs are used; wholesale KYC lives in the Wholesale tab) -->\n");
        sb.Append("        <div id=\"additional_fields_div\"");
        sb.Append(input.Enhanced ? " style=\"display:none;\" data-epc-enhanced=\"1\"" : string.Empty);
        sb.Append(">\n");
        sb.Append("        </div>\n");
        sb.Append("        <script>\n");
        sb.Append("        //Перегенировать поля\n");
        sb.Append("        function epcRegUsesEnhancedTabs(){\n");
        sb.Append("            var box=document.getElementById(\"additional_fields_div\");\n");
        sb.Append("            return !!(box && box.getAttribute(\"data-epc-enhanced\") === \"1\");\n");
        sb.Append("        }\n");
        sb.Append("        function regenerateFields()\n");
        sb.Append("        {\n");
        sb.Append("            var wrap=document.getElementById(\"additional_fields_div\");\n");
        sb.Append("            // Enhanced Retail/Wholesale tabs already collect profile + KYC/docs.\n");
        sb.Append("            // Additional information is wholesale-only there — do not render CMS duplicates for retail (or wholesale).\n");
        sb.Append("            if(epcRegUsesEnhancedTabs()){\n");
        sb.Append("                if(wrap){\n");
        sb.Append("                    wrap.innerHTML = \"\";\n");
        sb.Append("                    wrap.style.display = \"none\";\n");
        sb.Append("                }\n");
        sb.Append("                return;\n");
        sb.Append("            }\n");
        sb.Append("            if( reg_fields.length == 0 )\n");
        sb.Append("            {\n");
        sb.Append("                return;\n");
        sb.Append("            }\n");
        sb.Append("            var current_reg_variant = document.getElementById(\"reg_variant_selector\").value;\n");
        sb.Append("            \n");
        sb.Append("            var additional_html = \"\";//HTML для дополнительных полей регистрации\n");
        sb.Append("            for(var i=0; i < reg_fields.length; i++)\n");
        sb.Append("            {\n");
        sb.Append("                //Обработка show_for:\n");
        sb.Append("                if(reg_fields[i].show_for.indexOf(parseInt(current_reg_variant)) < 0)\n");
        sb.Append("                {\n");
        sb.Append("                    continue;//Это поле не показываем\n");
        sb.Append("                }\n");
        sb.Append("                \n");
        sb.Append("                //Обработка required_for\n");
        sb.Append("                var required_for = \"\";//Для звездочки\n");
        sb.Append("                if(reg_fields[i].required_for.indexOf(parseInt(current_reg_variant)) >= 0)\n");
        sb.Append("                {\n");
        sb.Append("                    required_for = \"*\";//Это поле не показываем\n");
        sb.Append("                }\n");
        sb.Append("                \n");
        sb.Append("\t\t\t\tvar example = reg_fields[i].caption;//Пример для заполнения\n");
        sb.Append("\t\t\t\tif(reg_fields[i].example != \"\")\n");
        sb.Append("\t\t\t\t{\n");
        sb.Append("\t\t\t\t\texample = reg_fields[i].example;\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\tadditional_html += \"<div class=\\\"form-group\\\"><label for=\\\"\"+reg_fields[i].name+\"\\\" class=\\\"col-sm-4 col-lg-3 control-label\\\">\"+reg_fields[i].caption+required_for+\"</label><div class=\\\"col-sm-8 col-lg-9\\\" style=\\\"padding:5px;\\\">\";\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t//Виджет:\n");
        sb.Append("                switch(reg_fields[i].widget_type)\n");
        sb.Append("                {\n");
        sb.Append("                    case \"text\":\n");
        sb.Append("                        additional_html += \"<input onKeyUp=\\\"dynamicApplying('\"+reg_fields[i].name+\"');\\\" type=\\\"text\\\" name=\\\"\"+reg_fields[i].name+\"\\\" id=\\\"\"+reg_fields[i].name+\"\\\" value='\"+reg_fields[i].value_buffer.replace('/([\"\\'\\])/g', \"\\\\$1\")+\"' class=\\\"form-control\\\" placeholder=\\\"\"+example+\"\\\" />\";\n");
        sb.Append("                        break;\n");
        sb.Append("                    case \"file\":\n");
        sb.Append("                        additional_html += \"<input type=\\\"file\\\" name=\\\"\"+reg_fields[i].name+\"\\\" id=\\\"\"+reg_fields[i].name+\"\\\" class=\\\"form-control\\\" accept=\\\".pdf,.jpg,.jpeg,.png,.webp\\\" />\";\n");
        sb.Append("                        if (example) {\n");
        sb.Append("                            additional_html += \"<p class=\\\"help-block\\\" style=\\\"margin:4px 0 0;\\\">\"+example+\"</p>\";\n");
        sb.Append("                        }\n");
        sb.Append("                        break;\n");
        sb.Append("                    case \"select\":\n");
        sb.Append("                        additional_html += \"<input onKeyUp=\\\"dynamicApplying('\"+reg_fields[i].name+\"');\\\" type=\\\"text\\\" name=\\\"\"+reg_fields[i].name+\"\\\" id=\\\"\"+reg_fields[i].name+\"\\\" value='\"+reg_fields[i].value_buffer.replace('/([\"\\'\\])/g', \"\\\\$1\")+\"' class=\\\"form-control\\\" placeholder=\\\"\"+example+\"\\\" />\";\n");
        sb.Append("                        break;\n");
        sb.Append("                };\n");
        sb.Append("                \n");
        sb.Append("                \n");
        sb.Append("                additional_html += \"</div></div><div class=\\\"row\\\"></div>\";\n");
        sb.Append("            }\n");
        sb.Append("            \n");
        sb.Append("            \n");
        sb.Append("            additional_html = \"<div class=\\\"panel panel-primary\\\"><div class=\\\"panel-heading\\\">");
        sb.Append(t(3928));
        sb.Append("</div><div class=\\\"panel-body\\\">\" + additional_html + \"</div></div>\";\n");
        sb.Append("            \n");
        sb.Append("            \n");
        sb.Append("            document.getElementById(\"additional_fields_div\").innerHTML = additional_html;\n");
        sb.Append("            if(wrap){ wrap.style.display = \"\"; }\n");
        sb.Append("        }//~function regenerateFields()\n");
        sb.Append("        \n");
        sb.Append("        \n");
        sb.Append("        \n");
        sb.Append("        // --------------------------------------------------------------------------\n");
        sb.Append("        //Функция динамическиго применния значений для текстовых строк\n");
        sb.Append("    \tfunction dynamicApplying(attribute)\n");
        sb.Append("    \t{\n");
        sb.Append("        \tvar str_value = document.getElementById(attribute).value;//Текущее значение\n");
        sb.Append("        \t//Ищем поле\n");
        sb.Append("        \tfor(var i=0; i < reg_fields.length; i++)\n");
        sb.Append("        \t{\n");
        sb.Append("        \t    if(reg_fields[i].name == attribute)\n");
        sb.Append("        \t    {\n");
        sb.Append("        \t        reg_fields[i].value_buffer = str_value;\n");
        sb.Append("        \t        //console.log(reg_fields[i].value_buffer);\n");
        sb.Append("        \t        break;\n");
        sb.Append("        \t    }\n");
        sb.Append("        \t}\n");
        sb.Append("    \t}\n");
        sb.Append("        \n");
        sb.Append("        \n");
        sb.Append("        regenerateFields();//Генерируем после загрузки страницы\n");
        sb.Append("        </script>\n");
        sb.Append("        \n");
        sb.Append("        <!--Captcha-->\n");
        sb.Append("        <div id=\"captcha\">\n");
        sb.Append("        \t<img src=\"/lib/captcha/captcha.php\" id=\"capcha-image\">\n");
        sb.Append("            <a href=\"javascript:void(0);\" onclick=\"document.getElementById('capcha-image').src='/lib/captcha/captcha.php?rid=' + Math.random();\"><img src=\"/lib/captcha/refresh.png\" border=\"0\"/></a><br>\n");
        sb.Append("            ");
        sb.Append(t(4067));
        sb.Append(": <input type=\"text\" name=\"capcha_input\" id=\"capcha_input\">\n");
        sb.Append("        </div>\n");
        sb.Append("        \n");
        sb.Append("\t\t\n");
        sb.Append("\t\t");
        sb.Append(input.UsersAgreement);
        sb.Append("\t\t\n");
        sb.Append("\t\t\n");
        sb.Append("        <button class=\"btn btn-ar btn-primary\" type=\"submit\">");
        sb.Append(t(4743));
        sb.Append("</button>\n");
        sb.Append("    </form>\n");
        sb.Append("    <!-- Start ФОРМА РЕГИСТРАЦИИ -->\n");
        sb.Append("\n");
        sb.Append(StorefrontAuthPartials.AuthLayout.Close());
        sb.Append(input.OtpModal);
        sb.Append("    <script>\n");
        sb.Append("    // ------------------------------------------------------------------------------------\n");
        sb.Append("    //ПРОВЕРКА КОРРЕКСТНОСТИ ЗАПОЛНЕНИЯ ФОРМЫ:\n");
        sb.Append("    //Флаги для реализации синхронных проверочных запросов\n");
        sb.Append("    var reg_contact_check = false;//Флаг проверки контакта (уникальность и корректность)\n");
        sb.Append("    var captcha_correct = false;//Флаг корректности captcha\n");
        sb.Append("    function onSubmitCheck()\n");
        sb.Append("    {\n");
        sb.Append("\t\tif(typeof epcRegUsesEnhancedTabs !== \"function\"){\n");
        sb.Append("\t\t\twindow.epcRegUsesEnhancedTabs = function(){ return !!(document.getElementById(\"additional_fields_div\") && document.getElementById(\"additional_fields_div\").getAttribute(\"data-epc-enhanced\")===\"1\"); };\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\tif(typeof epcRegSyncTabFields === \"function\"){\n");
        sb.Append("\t\t\ttry{ epcRegSyncTabFields(); }catch(e){}\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\tif( !check_user_agreement() )\n");
        sb.Append("\t\t{\n");
        sb.Append("\t\t\treturn false;\n");
        sb.Append("\t\t}\n");
        sb.Append("\t\t\n");
        sb.Append("\t\t\n");
        sb.Append("    \t//1. ПРОВЕРКА КОРРЕКТНОСТИ ЗАПОЛНЕНИЯ\n");
        sb.Append("        //1.1 Текущий регистрационный вариант\n");
        sb.Append("        var currentRegVariant = document.getElementById(\"reg_variant_selector\").value;\n");
        sb.Append("        \n");
        sb.Append("        //1.2 Проверка факта заполнения полей какими-либо значениями\n");
        sb.Append("        // Enhanced Retail/Wholesale tabs own these fields — skip CMS \"Additional information\" required checks for retail.\n");
        sb.Append("    \tif(!epcRegUsesEnhancedTabs())\n");
        sb.Append("    \t{\n");
        sb.Append("\t    \tfor(var i=0; i<reg_fields.length; i++)\n");
        sb.Append("\t    \t{\n");
        sb.Append("\t    \t\tif(reg_fields[i].required_for.indexOf(parseInt(currentRegVariant)) != -1)//Заполнение требуется для данного Регистрационного Варианта\n");
        sb.Append("\t    \t\t{\n");
        sb.Append("\t    \t\t\tvar reqEl=document.getElementById(reg_fields[i].name);\n");
        sb.Append("\t    \t\t\tif(!reqEl){ continue; }\n");
        sb.Append("\t    \t\t\tif(reg_fields[i].widget_type === \"file\"){\n");
        sb.Append("\t    \t\t\t\tif(!reqEl.files || !reqEl.files.length){\n");
        sb.Append("\t    \t\t\t\t\talert(\"Заполните поле \"+reg_fields[i].caption);\n");
        sb.Append("\t    \t\t\t\t\treturn false;\n");
        sb.Append("\t    \t\t\t\t}\n");
        sb.Append("\t    \t\t\t}else if(reqEl.value == \"\"){//Но поле не заполнено\n");
        sb.Append("\t    \t\t\t\talert(\"Заполните поле \"+reg_fields[i].caption);\n");
        sb.Append("\t    \t\t\t\treturn false;\n");
        sb.Append("\t    \t\t\t}\n");
        sb.Append("\t    \t\t}\n");
        sb.Append("\t    \t}//for(i)\n");
        sb.Append("    \t}\n");
        sb.Append("        \n");
        sb.Append("        \n");
        sb.Append("        //1.3 Обработка заполнения пароля:\n");
        sb.Append("    \tif(document.getElementById(\"password\").value != document.getElementById(\"password_repeat\").value)//Пароли должны совпадать\n");
        sb.Append("    \t{\n");
        sb.Append("    \t\talert(\"");
        sb.Append(t(3933));
        sb.Append("\");\n");
        sb.Append("    \t\treturn false;\n");
        sb.Append("    \t}\n");
        sb.Append("    \t//Проверям минимально допустимую длину пароля\n");
        sb.Append("\t    if(document.getElementById(\"password\").value.length < ");
        sb.Append(input.MinPasswordLen);
        sb.Append(")\n");
        sb.Append("    \t{\n");
        sb.Append("\t\t    alert(\"");
        sb.Append(t(3934));
        sb.Append(" ");
        sb.Append(input.MinPasswordLen);
        sb.Append(" ");
        sb.Append(t(3935));
        sb.Append("\");\n");
        sb.Append("\t\t    return false;\n");
        sb.Append("    \t}\n");
        sb.Append("    \t\n");
        sb.Append("    \t\n");
        sb.Append("    \t\n");
        sb.Append("    \t\n");
        sb.Append("    \t\n");
        sb.Append("    \t//1.4 Проверка соответствия заполненных значений регулярным выражениям\n");
        sb.Append("    \t//Если поле пустое - значит его можно было не заполнять (проверка на факт заполнения следует раньше). Но есть там есть значение, то оно обязательно должно соответствовать RegExp, даже если оно не обязательно к заполнению\n");
        sb.Append("    \tif(!epcRegUsesEnhancedTabs())\n");
        sb.Append("    \t{\n");
        sb.Append("\t    \tfor(var i=0; i<reg_fields.length; i++)\n");
        sb.Append("\t    \t{\n");
        sb.Append("\t    \t\tif(reg_fields[i].show_for.indexOf(parseInt(currentRegVariant)) == -1)//У этого поля не указан текущий Регистрационный Вариант - его нет в форме\n");
        sb.Append("\t    \t\t{\n");
        sb.Append("\t    \t\t\tcontinue;\n");
        sb.Append("\t    \t\t}\n");
        sb.Append("\t    \t\tif(reg_fields[i].widget_type === \"file\")\n");
        sb.Append("\t    \t\t{\n");
        sb.Append("\t    \t\t\tcontinue;\n");
        sb.Append("\t    \t\t}\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t//Если регулярное выражение пустое - значит пропускаем, т.к. требований к содержимому нет\n");
        sb.Append("\t\t\t\tif(reg_fields[i].regexp == \"\")\n");
        sb.Append("\t\t\t\t{\n");
        sb.Append("\t\t\t\t\tcontinue;\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t    \t\t\n");
        sb.Append("\t    \t\tvar fldEl=document.getElementById(reg_fields[i].name);\n");
        sb.Append("\t    \t\tif(!fldEl){ continue; }\n");
        sb.Append("\t    \t\tif(String(fldEl.value) != \"\")\n");
        sb.Append("\t    \t\t{\n");
        sb.Append("\t    \t\t\tvar current_value = String(fldEl.value);//Заполненное значение\n");
        sb.Append("\t    \t\t\tvar regex = new RegExp(reg_fields[i].regexp);//Регулярное выражение для поля\n");
        sb.Append("\t    \t\t\t//Далее ищем подстроку по регулярному выражению\n");
        sb.Append("\t    \t\t\tvar match = regex.exec(String(current_value));\n");
        sb.Append("\t    \t\t\tif(match == null)\n");
        sb.Append("\t    \t\t\t{\n");
        sb.Append("\t    \t\t\t\talert(\"");
        sb.Append(t(3885));
        sb.Append(" \"+reg_fields[i].caption+\" ");
        sb.Append(t(3930));
        sb.Append("\");\n");
        sb.Append("\t    \t\t\t\treturn false;\n");
        sb.Append("\t    \t\t\t}\n");
        sb.Append("\t    \t\t\telse\n");
        sb.Append("\t    \t\t\t{\n");
        sb.Append("\t    \t\t\t\tvar match_value = String(match[0]);//Подходящая подстрока\n");
        sb.Append("\t    \t\t\t\tif(match_value != current_value)\n");
        sb.Append("\t    \t\t\t\t{\n");
        sb.Append("\t    \t\t\t\t\talert(\"");
        sb.Append(t(3893));
        sb.Append(" \"+reg_fields[i].caption+\" ");
        sb.Append(t(3931));
        sb.Append("\");\n");
        sb.Append("\t    \t\t\t\t\treturn false;\n");
        sb.Append("\t    \t\t\t\t}\n");
        sb.Append("\t    \t\t\t}\n");
        sb.Append("\t    \t\t\t//Заполнено правильно, если: есть подстрока по регулярному выражению и она полностью равна самой строке\n");
        sb.Append("\t    \t\t}\n");
        sb.Append("\t    \t}\n");
        sb.Append("    \t}\n");
        sb.Append("    \t\n");
        sb.Append("    \t\n");
        sb.Append("    \t//1.5 Проверка уникальности и корректности reg_contact синхронным запросом\n");
        sb.Append("    \tvar reg_contact = document.getElementById(\"reg_contact_input\").value;//Введеный reg_contact\n");
        sb.Append("\t\tvar reg_contact_type = document.getElementById(\"reg_contact_select\").value;\n");
        sb.Append("\t\t//Сама проверка\n");
        sb.Append("\t\tjQuery.ajax({\n");
        sb.Append("\t\t\ttype: \"POST\",\n");
        sb.Append("\t\t\tasync: false, //Запрос синхронный\n");
        sb.Append("\t\t\turl: \"");
        sb.Append(input.DomainPath);
        sb.Append("content/users/check_reg_contact.php\",\n");
        sb.Append("\t\t\tdataType: \"text\",//Тип возвращаемого значения\n");
        sb.Append("\t\t\tdata: \"reg_contact=\"+reg_contact+\"&reg_contact_type=\"+reg_contact_type+\"&csrf_guard_key=");
        sb.Append(input.CsrfGuardKey);
        sb.Append("\",\n");
        sb.Append("\t\t\tsuccess: function(answer)\n");
        sb.Append("\t\t\t{\n");
        sb.Append("\t\t\t\tconsole.log(answer);\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\tvar answer_ob = JSON.parse(answer);\n");
        sb.Append("\t\t\t\t\n");
        sb.Append("\t\t\t\t//Если некорректный парсинг ответа\n");
        sb.Append("\t\t\t\tif( typeof answer_ob.status === \"undefined\" )\n");
        sb.Append("\t\t\t\t{\n");
        sb.Append("\t\t\t\t\treg_contact_check = false;\n");
        sb.Append("\t\t\t\t\talert(\"");
        sb.Append(t(4744));
        sb.Append("\");\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t\telse\n");
        sb.Append("\t\t\t\t{\n");
        sb.Append("\t\t\t\t\t//Корректный парсинг ответа\n");
        sb.Append("\t\t\t\t\tif(answer_ob.status == true)\n");
        sb.Append("\t\t\t\t\t{\n");
        sb.Append("\t\t\t\t\t\treg_contact_check = true;\n");
        sb.Append("\t\t\t\t\t}\n");
        sb.Append("\t\t\t\t\telse\n");
        sb.Append("\t\t\t\t\t{\n");
        sb.Append("\t\t\t\t\t\treg_contact_check = false;\n");
        sb.Append("\t\t\t\t\t\talert(answer_ob.message);\n");
        sb.Append("\t\t\t\t\t}\n");
        sb.Append("\t\t\t\t}\n");
        sb.Append("\t\t\t}\n");
        sb.Append("\t\t});\n");
        sb.Append("    \tif(reg_contact_check == false)\n");
        sb.Append("    \t{\n");
        sb.Append("    \t\treturn false;\n");
        sb.Append("    \t}\n");
        sb.Append("    \t\n");
        sb.Append("\t\t\n");
        sb.Append("\t\t//alert(\"ok\");\n");
        sb.Append("\t\t//return false;\n");
        sb.Append("    \t\n");
        sb.Append("    \t//Проверка Captcha синхронным запросом\n");
        sb.Append("    \tvar capcha_input = document.getElementById(\"capcha_input\").value;\n");
        sb.Append("    \tjQuery.ajax({\n");
        sb.Append("    \t   type: \"POST\",\n");
        sb.Append("    \t   async: false, //Запрос синхронный\n");
        sb.Append("    \t   url: \"/lib/captcha/check_captcha.php\",\n");
        sb.Append("    \t   dataType: \"json\",//Тип возвращаемого значения\n");
        sb.Append("    \t   data: \"captcha_check=\"+capcha_input,\n");
        sb.Append("    \t   success: function(is_captcha_correct){\n");
        sb.Append("    \t\t   captcha_correct = is_captcha_correct;\n");
        sb.Append("    \t   }\n");
        sb.Append("    \t });\n");
        sb.Append("    \tif(captcha_correct == false)\n");
        sb.Append("    \t{\n");
        sb.Append("    \t\talert(\"");
        sb.Append(t(4070));
        sb.Append("\");\n");
        sb.Append("    \t\tdocument.getElementById('capcha-image').src='/lib/captcha/captcha.php?rid=' + Math.random();\n");
        sb.Append("    \t\treturn false;\n");
        sb.Append("    \t}\n");
        sb.Append("\t\t");
        if (input.Enhanced)
        {
            sb.Append("\t\tif(typeof epcRegClientValidate === 'function' && !epcRegClientValidate()){\n");
            sb.Append("\t\t\treturn false;\n");
            sb.Append("\t\t}\n");
            sb.Append("\t\t");
        }
        else
        {
            sb.Append("\t\tvar tradeType = document.querySelector('input[name=\"epc_customer_type\"]:checked');\n");
            sb.Append("\t\tif(!tradeType || (tradeType.value !== 'retail' && tradeType.value !== 'wholesale')){\n");
            sb.Append("\t\t\talert('Please choose Retail customer or Wholesale customer.');\n");
            sb.Append("\t\t\treturn false;\n");
            sb.Append("\t\t}\n");
            sb.Append("\t\t");
        }
        sb.Append("\n");
        sb.Append("\t\t// ── Email OTP verification step ──\n");
        sb.Append("\t\tvar _regContactType = (document.getElementById('reg_contact_select')||{}).value||'email';\n");
        sb.Append("\t\tif(_regContactType === 'email' && !window.epcRegOtpVerified){\n");
        sb.Append("\t\t\tvar _regEmail = (document.getElementById('reg_contact_input')||{}).value||'';\n");
        sb.Append("\t\t\t_regEmail = _regEmail.trim();\n");
        sb.Append("\t\t\tif(_regEmail && window.EpcOtpModal && window.EpcOtpModal['epc_reg_otp']){\n");
        sb.Append("\t\t\t\twindow.EpcOtpModal['epc_reg_otp'].open(_regEmail);\n");
        sb.Append("\t\t\t\treturn false; // Hold submit — modal on_success will re-submit\n");
        sb.Append("\t\t\t}\n");
        sb.Append("\t\t}\n");
        sb.Append("\n");
        sb.Append("    \treturn true;\n");
        sb.Append("    }\n");
        sb.Append("    </script>\n");
        sb.Append("\n");
        return sb.ToString();
    }
}
