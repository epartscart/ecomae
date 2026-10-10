using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-fjord marketing broadcast panel. PHP identifiers kept for the inventory:
/// <c>epc_mb_render_hub</c>, <c>epc_mb_render_email_tab</c>,
/// <c>epc_mb_render_whatsapp_tab</c>, <c>epc_mb_render_history_tab</c>,
/// <c>epc_mb_render_guide_tab</c>.
/// Path: <c>cp/content/control/portal/epc_marketing_broadcast_panel.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Fjord
{
    public const string MarketingBroadcastPanelPath = "cp/content/control/portal/epc_marketing_broadcast_panel.php";

    public static Func<bool>? IsAdmin { get; set; }
    public static Func<int>? UserId { get; set; }
    public static Func<object?>? TenantPdo { get; set; }
    public static Func<Dictionary<string, object?>>? ShopContext { get; set; }
    public static Func<Dictionary<string, object?>>? DashboardStats { get; set; }
    public static Func<int, List<Dictionary<string, object?>>>? ListCampaigns { get; set; }
    public static Func<List<Dictionary<string, object?>>>? ListGroups { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? EmailTemplates { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? WhatsappTemplates { get; set; }
    public static Func<string>? CsrfToken { get; set; }
    public static Func<bool>? VerifyCsrf { get; set; }
    public static Func<string, string>? HubUrl { get; set; }
    public static Func<string>? Backend { get; set; }
    public static Func<Dictionary<string, object?>>? SmtpDiagnose { get; set; }
    public static Func<bool>? WaApiEnabled { get; set; }
    public static Func<Dictionary<string, string>, int, Dictionary<string, object?>>? SendEmail { get; set; }
    public static Func<Dictionary<string, string>, int, Dictionary<string, object?>>? SendWhatsapp { get; set; }
    public static Action<Dictionary<string, object?>>? FrameOpen { get; set; }
    public static Action? FrameClose { get; set; }
    public static string RequestMethod { get; set; } = "GET";
    public static Dictionary<string, string> Query { get; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Post { get; } = new(StringComparer.Ordinal);
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsAdmin = null;
        UserId = null;
        TenantPdo = null;
        ShopContext = null;
        DashboardStats = null;
        ListCampaigns = null;
        ListGroups = null;
        EmailTemplates = null;
        WhatsappTemplates = null;
        CsrfToken = null;
        VerifyCsrf = null;
        HubUrl = null;
        Backend = null;
        SmtpDiagnose = null;
        WaApiEnabled = null;
        SendEmail = null;
        SendWhatsapp = null;
        FrameOpen = null;
        FrameClose = null;
        RequestMethod = "GET";
        Query.Clear();
        Post.Clear();
        LastOutput = "";
    }

    public static void EpcMbRenderHub()
    {
        if (IsAdmin?.Invoke() != true)
        {
            Echo("<div class=\"alert alert-warning\">Admin login required for Marketing broadcast.</div>");
            return;
        }

        if (TenantPdo?.Invoke() is null)
        {
            Echo("<div class=\"alert alert-danger\">Database unavailable.</div>");
            return;
        }

        var tab = Sanitize(Query.GetValueOrDefault("tab") ?? "email");
        if (tab == "")
        {
            tab = "email";
        }

        var shop = ShopContext?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["shop_name"] = "O'Reilly Parts" };
        var stats = DashboardStats?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["email_recipients"] = 4, ["whatsapp_recipients"] = 3, ["emails_sent"] = 2, ["whatsapp_sent"] = 1, ["campaigns"] = 2
        };
        var campaigns = ListCampaigns?.Invoke(15) ?? [];
        var groups = ListGroups?.Invoke() ?? [new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 7, ["name"] = "O'Reilly trade" }];
        var emailTemplates = EmailTemplates?.Invoke() ?? DefaultEmail();
        var waTemplates = WhatsappTemplates?.Invoke() ?? DefaultWa();
        var csrf = CsrfToken?.Invoke() ?? "tok-1";
        var guideUrl = Url("guide");
        var backend = Backend?.Invoke() ?? "cp";
        var emailSettingsUrl = "/" + backend + "/control/portal/epc_tenant_email_settings";
        var integrationsUrl = "/" + backend + "/control/portal/epc_integrations_hub";
        var smtpDiag = SmtpDiagnose?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = 1, ["issues"] = new List<string>() };
        var waApi = WaApiEnabled?.Invoke() == true;
        Dictionary<string, object?>? flash = null;
        if (RequestMethod == "POST" && Post.ContainsKey("epc_mb_action"))
        {
            if (VerifyCsrf?.Invoke() != true)
            {
                flash = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "CSRF validation failed. Refresh and try again." };
            }
            else
            {
                var action = Post.GetValueOrDefault("epc_mb_action") ?? "";
                var operatorId = UserId?.Invoke() ?? 21;
                if (action == "send_email")
                {
                    flash = SendEmail?.Invoke(Post, operatorId) ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "email:" + operatorId };
                }
                else if (action == "send_whatsapp")
                {
                    flash = SendWhatsapp?.Invoke(Post, operatorId) ?? new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["ok"] = true,
                        ["message"] = "wa:" + operatorId,
                        ["wa_links"] = new List<Dictionary<string, object?>>
                        {
                            new(StringComparer.Ordinal) { ["link"] = "https://wa.me/97150", ["name"] = "O'Reilly" },
                            new(StringComparer.Ordinal) { ["link"] = "", ["name"] = "skip" }
                        }
                    };
                }

                if (flash is not null)
                {
                    stats = DashboardStats?.Invoke() ?? stats;
                    campaigns = ListCampaigns?.Invoke(15) ?? campaigns;
                }
            }
        }

        if (FrameOpen is not null)
        {
            FrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal) { ["class"] = "epc-mb-hub" });
        }
        else
        {
            Echo("FRAME_OPEN:epc-mb-hub");
        }

        Echo("<header class=\"epc-mb-brandbar\">");
        Echo("<div class=\"epc-mb-brandbar__mark\"><i class=\"fa fa-bullhorn\" aria-hidden=\"true\"></i></div>");
        Echo("<div><div class=\"epc-mb-brandbar__name\">Marketing broadcast</div>");
        Echo("<div class=\"epc-mb-brandbar__sub\">" + H(shop.GetValueOrDefault("shop_name")) + " — Email &amp; WhatsApp · audience → template → preview → send</div></div>");
        Echo("<div class=\"epc-mb-brandbar__actions\">");
        Echo("<a class=\"epc-mb-chip-link\" href=\"" + H(guideUrl) + "\"><i class=\"fa fa-book\"></i> Guide</a>");
        Echo("<a class=\"epc-mb-chip-link\" href=\"" + H(emailSettingsUrl) + "\"><i class=\"fa fa-envelope\"></i> Email SMTP</a>");
        Echo("<a class=\"epc-mb-chip-link\" href=\"" + H(integrationsUrl) + "\"><i class=\"fa fa-plug\"></i> Integrations</a>");
        Echo("</div></header>");
        if (flash is not null)
        {
            Echo("<div class=\"alert alert-" + (!PhpEmpty(flash.GetValueOrDefault("ok")) ? "success" : "danger") + "\">" + H(flash.GetValueOrDefault("message")) + "</div>");
            if (!PhpEmpty(flash.GetValueOrDefault("wa_links")) && flash["wa_links"] is IEnumerable<object?> links)
            {
                Echo("<div class=\"epc-mb-wa-links\"><h4><i class=\"fa fa-whatsapp\"></i> wa.me links — open each to send</h4>");
                foreach (var item in links)
                {
                    var wl = item as Dictionary<string, object?> ?? [];
                    if (PhpEmpty(wl.GetValueOrDefault("link")))
                    {
                        continue;
                    }

                    Echo("<a class=\"btn btn-success btn-sm\" target=\"_blank\" rel=\"noopener\" href=\"" + H(wl.GetValueOrDefault("link")) + "\">"
                         + "<i class=\"fa fa-whatsapp\"></i> " + H(wl.GetValueOrDefault("name") ?? wl.GetValueOrDefault("phone") ?? "Customer") + "</a>");
                }

                Echo("</div>");
            }
        }

        var kpis = new (object? Val, string Label, string Icon, string Cls)[]
        {
            (stats.GetValueOrDefault("email_recipients"), "Customers with email", "fa-envelope", "mail"),
            (stats.GetValueOrDefault("whatsapp_recipients"), "Customers with phone", "fa-whatsapp", "wa"),
            (stats.GetValueOrDefault("emails_sent"), "Emails sent", "fa-paper-plane", "mail"),
            (stats.GetValueOrDefault("whatsapp_sent"), "WhatsApp sent", "fa-comments", "wa"),
            (stats.GetValueOrDefault("campaigns"), "Campaigns", "fa-flag", "")
        };
        Echo("<div class=\"epc-mb-kpi\" role=\"group\" aria-label=\"Broadcast metrics\">");
        foreach (var kpi in kpis)
        {
            var iconCls = kpi.Cls != "" ? " epc-mb-kpi__icon--" + kpi.Cls : "";
            Echo("<div class=\"epc-mb-kpi__item\"><div class=\"epc-mb-kpi__icon" + iconCls + "\"><i class=\"fa " + H(kpi.Icon) + "\"></i></div>");
            Echo("<div><div class=\"epc-mb-kpi__val\">" + ToInt(kpi.Val) + "</div><div class=\"epc-mb-kpi__label\">" + H(kpi.Label) + "</div></div></div>");
        }

        Echo("</div>");
        var tabs = new (string Key, string Label, string Icon)[]
        {
            ("email", "Email", "fa-envelope"),
            ("whatsapp", "WhatsApp", "fa-whatsapp"),
            ("history", "History", "fa-history"),
            ("guide", "Guide", "fa-book")
        };
        Echo("<nav class=\"epc-mb-tabs\" aria-label=\"Broadcast sections\">");
        foreach (var (key, label, icon) in tabs)
        {
            var active = tab == key ? " is-active" : "";
            Echo("<a class=\"" + active + "\" href=\"" + H(Url(key)) + "\"><i class=\"fa " + H(icon) + "\"></i> " + H(label) + "</a>");
        }

        Echo("</nav>");
        if (tab == "email")
        {
            EpcMbRenderEmailTab(emailTemplates, groups, csrf, smtpDiag, emailSettingsUrl);
        }
        else if (tab == "whatsapp")
        {
            EpcMbRenderWhatsappTab(waTemplates, groups, csrf, waApi);
        }
        else if (tab == "history")
        {
            EpcMbRenderHistoryTab(campaigns);
        }
        else
        {
            EpcMbRenderGuideTab(shop, emailSettingsUrl, guideUrl, waApi, integrationsUrl);
        }

        if (FrameClose is not null)
        {
            FrameClose();
        }
        else
        {
            Echo("FRAME_CLOSE");
        }
    }

    public static void EpcMbRenderEmailTab(Dictionary<string, Dictionary<string, object?>> templates, List<Dictionary<string, object?>> groups, string csrf, Dictionary<string, object?> smtpDiag, string emailSettingsUrl)
    {
        var smtpOk = !PhpEmpty(smtpDiag.GetValueOrDefault("ok"));
        Echo("<div class=\"epc-mb-status-row\">");
        if (smtpOk)
        {
            Echo("<span class=\"epc-mb-badge epc-mb-badge--ok\"><i class=\"fa fa-check-circle\"></i> SMTP ready</span>");
        }
        else
        {
            Echo("<span class=\"epc-mb-badge epc-mb-badge--warn\"><i class=\"fa fa-exclamation-triangle\"></i> SMTP not ready</span>");
            Echo("<a class=\"btn btn-xs btn-default\" href=\"" + H(emailSettingsUrl) + "\">Configure SMTP</a>");
        }

        Echo("<span class=\"epc-mb-badge epc-mb-badge--info\"><i class=\"fa fa-info-circle\"></i> Rate-limited ~5/sec · max 100 / batch</span>");
        Echo("</div>");
        if (!smtpOk)
        {
            var issues = smtpDiag.GetValueOrDefault("issues") as IEnumerable<object?> ?? [];
            Echo("<div class=\"alert alert-warning\"><i class=\"fa fa-exclamation-triangle\"></i> SMTP not ready: "
                 + H(string.Join(" ", issues.Select(i => Str(i))))
                 + ". Configure under <a href=\"" + H(emailSettingsUrl) + "\">Email / SMTP</a> and run a test send first.</div>");
        }

        Echo("<form method=\"post\" class=\"epc-mb-form\" id=\"epc-mb-email-form\">");
        Echo("<input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
        Echo("<input type=\"hidden\" name=\"epc_mb_action\" value=\"send_email\">");
        var emailTplDefault = templates.Keys.FirstOrDefault() ?? "promo_sale";
        Echo("<input type=\"hidden\" name=\"template_key\" id=\"epc-mb-email-template-key\" value=\"" + H(emailTplDefault) + "\">");
        Echo("<div class=\"epc-mb-compose\"><div>");
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-users\"></i> Audience</h4><span class=\"epc-mb-panel__hint\">Who receives this email</span></div><div class=\"epc-mb-panel__body\">");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">1</span> Send to</div>");
        Echo("<div class=\"epc-mb-modes\">");
        var first = true;
        foreach (var (val, title, hint) in new[]
                 {
                     ("all", "All with email", "Every customer with an email on file"),
                     ("with_orders", "With orders", "Customers who have placed orders"),
                     ("group", "Group / segment", "Price or access group"),
                     ("manual", "Manual list", "Paste emails, one per line")
                 })
        {
            Echo("<label class=\"epc-mb-mode" + (first ? " is-active" : "") + "\"><input type=\"radio\" name=\"audience_mode\" class=\"epc-mb-audience-mode\" data-channel=\"email\" value=\"" + H(val) + "\"" + (first ? " checked" : "") + ">");
            Echo("<strong>" + H(title) + "</strong><span>" + H(hint) + "</span></label>");
            first = false;
        }

        Echo("</div>");
        Echo("<div class=\"form-group epc-mb-group-select\" style=\"display:none;margin-top:10px\"><label>Group</label><select name=\"audience_meta_group\" class=\"form-control\"><option value=\"\">— Select —</option>");
        foreach (var g in groups)
        {
            Echo("<option value=\"" + ToInt(g.GetValueOrDefault("id")) + "\">" + H(g.GetValueOrDefault("name") ?? ("Group #" + g.GetValueOrDefault("id"))) + "</option>");
        }

        Echo("</select></div>");
        Echo("<div class=\"form-group epc-mb-manual-input\" style=\"display:none;margin-top:10px\"><label>Paste emails (one per line)</label>");
        Echo("<textarea name=\"audience_meta_manual\" class=\"form-control\" rows=\"4\" placeholder=\"customer@example.com\"></textarea></div>");
        Echo("<input type=\"hidden\" name=\"audience_meta\" id=\"epc-mb-email-audience-meta\" value=\"\">");
        Echo("<div class=\"epc-mb-count\"><i class=\"fa fa-user\"></i> <span id=\"epc-mb-email-count\">—</span></div>");
        Echo("</div></div></div>");
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-file-text-o\"></i> Template &amp; message</h4><span class=\"epc-mb-panel__hint\">Step 2–3</span></div><div class=\"epc-mb-panel__body\">");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">2</span> Brochure template</div>");
        Echo("<div class=\"epc-mb-templates\" id=\"epc-mb-email-templates\">");
        var ti = 0;
        foreach (var (key, tpl) in templates)
        {
            Echo("<button type=\"button\" class=\"epc-mb-tpl" + (ti == 0 ? " is-active" : "") + "\" data-template=\"" + H(key) + "\" data-channel=\"email\">");
            Echo("<div class=\"epc-mb-tpl__icon\"><i class=\"fa fa-file-text-o\"></i></div>");
            Echo("<div class=\"epc-mb-tpl__label\">" + H(tpl.GetValueOrDefault("label")) + "</div></button>");
            ti++;
        }

        Echo("</div>");
        Echo("<select class=\"epc-mb-template-select form-control\" data-channel=\"email\" style=\"position:absolute;left:-9999px;width:1px;height:1px;opacity:0\" tabindex=\"-1\" aria-hidden=\"true\">");
        foreach (var (key, tpl) in templates)
        {
            Echo("<option value=\"" + H(key) + "\">" + H(tpl.GetValueOrDefault("label")) + "</option>");
        }

        Echo("</select></div>");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">3</span> Subject &amp; body</div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-email-subject\">Subject</label><input type=\"text\" name=\"subject\" id=\"epc-mb-email-subject\" class=\"form-control\" placeholder=\"Email subject\"></div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-email-preview\">Inbox preview text</label><input type=\"text\" name=\"preview\" id=\"epc-mb-email-preview\" class=\"form-control\" placeholder=\"Short snippet under the subject\"></div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-email-body\">HTML body</label>");
        Echo("<textarea name=\"body_html\" id=\"epc-mb-email-body\" class=\"form-control epc-mb-html-body\" rows=\"12\" placeholder=\"Leave blank to use template HTML\"></textarea>");
        Echo("<div class=\"epc-mb-vars\"><code>{{customer_name}}</code><code>{{shop_name}}</code><code>{{shop_url}}</code></div></div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-email-batch\">Batch limit</label>");
        Echo("<input type=\"number\" name=\"batch_limit\" id=\"epc-mb-email-batch\" class=\"form-control\" value=\"50\" min=\"1\" max=\"100\" style=\"max-width:140px\">");
        Echo("<p class=\"help-block\" style=\"margin:6px 0 0\">Max recipients this send (1–100). Sending is paced to protect SMTP reputation.</p></div>");
        Echo("</div>");
        Echo("<div class=\"epc-mb-actions\">");
        Echo("<button type=\"submit\" class=\"btn btn-primary\"" + (smtpOk ? "" : " disabled") + " data-confirm=\"Send this email campaign to the selected audience?\"><i class=\"fa fa-paper-plane\"></i> Send email campaign</button>");
        Echo("<button type=\"button\" class=\"btn btn-default\" id=\"epc-mb-email-reload-tpl\"><i class=\"fa fa-refresh\"></i> Reload template</button>");
        Echo("</div></div></div></div>");
        Echo("<aside class=\"epc-mb-panel epc-mb-preview\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-eye\"></i> Live email preview</h4><span class=\"epc-mb-panel__hint\">Updates as you type</span></div>");
        Echo("<div class=\"epc-mb-panel__body\"><div class=\"epc-mb-preview__frame\"><iframe id=\"epc-mb-email-preview-frame\" title=\"Email preview\" sandbox=\"\"></iframe></div>");
        Echo("<p class=\"help-block\" style=\"margin:10px 0 0\">Preview uses sample name “Customer”. Merge tags resolve per recipient on send.</p></div></aside>");
        Echo("</div></form>");
    }

    public static void EpcMbRenderWhatsappTab(Dictionary<string, Dictionary<string, object?>> templates, List<Dictionary<string, object?>> groups, string csrf, bool waApi)
    {
        Echo("<div class=\"epc-mb-status-row\">");
        if (waApi)
        {
            Echo("<span class=\"epc-mb-badge epc-mb-badge--ok\"><i class=\"fa fa-check-circle\"></i> WhatsApp Cloud API enabled</span>");
        }
        else
        {
            Echo("<span class=\"epc-mb-badge epc-mb-badge--info\"><i class=\"fa fa-link\"></i> wa.me mode — prepare links, then open in WhatsApp</span>");
        }

        Echo("<span class=\"epc-mb-badge epc-mb-badge--info\"><i class=\"fa fa-language\"></i> Bilingual EN + AR recommended (UAE)</span>");
        Echo("</div>");
        if (!waApi)
        {
            Echo("<div class=\"alert alert-info\"><i class=\"fa fa-info-circle\"></i> Phase 1: we generate one <strong>wa.me</strong> link per customer. Open each to send from WhatsApp Web/desktop. Enable WhatsApp API in Configuration for automatic send.</div>");
        }
        else
        {
            Echo("<div class=\"alert alert-success\"><i class=\"fa fa-check\"></i> WhatsApp Cloud API is enabled — messages will be sent automatically.</div>");
        }

        Echo("<form method=\"post\" class=\"epc-mb-form\" id=\"epc-mb-wa-form\">");
        Echo("<input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
        Echo("<input type=\"hidden\" name=\"epc_mb_action\" value=\"send_whatsapp\">");
        var waTplDefault = templates.Keys.FirstOrDefault() ?? "promo_bilingual";
        Echo("<input type=\"hidden\" name=\"template_key\" id=\"epc-mb-wa-template-key\" value=\"" + H(waTplDefault) + "\">");
        Echo("<div class=\"epc-mb-compose\"><div>");
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-users\"></i> Audience</h4><span class=\"epc-mb-panel__hint\">Who receives WhatsApp</span></div><div class=\"epc-mb-panel__body\">");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">1</span> Send to</div>");
        Echo("<div class=\"epc-mb-modes\">");
        var first = true;
        foreach (var (val, title, hint) in new[]
                 {
                     ("all", "All with phone", "Every customer with a phone number"),
                     ("with_orders", "With orders", "Customers who have placed orders"),
                     ("group", "Group / segment", "Price or access group"),
                     ("manual", "Manual list", "Paste phones, one per line")
                 })
        {
            Echo("<label class=\"epc-mb-mode" + (first ? " is-active" : "") + "\"><input type=\"radio\" name=\"audience_mode\" class=\"epc-mb-audience-mode\" data-channel=\"whatsapp\" value=\"" + H(val) + "\"" + (first ? " checked" : "") + ">");
            Echo("<strong>" + H(title) + "</strong><span>" + H(hint) + "</span></label>");
            first = false;
        }

        Echo("</div>");
        Echo("<div class=\"form-group epc-mb-group-select\" style=\"display:none;margin-top:10px\"><label>Group</label><select name=\"audience_meta_group\" class=\"form-control\"><option value=\"\">— Select —</option>");
        foreach (var g in groups)
        {
            Echo("<option value=\"" + ToInt(g.GetValueOrDefault("id")) + "\">" + H(g.GetValueOrDefault("name") ?? ("Group #" + g.GetValueOrDefault("id"))) + "</option>");
        }

        Echo("</select></div>");
        Echo("<div class=\"form-group epc-mb-manual-input\" style=\"display:none;margin-top:10px\"><label>Paste phone numbers</label>");
        Echo("<textarea name=\"audience_meta_manual\" class=\"form-control\" rows=\"4\" placeholder=\"+971501234567\"></textarea></div>");
        Echo("<input type=\"hidden\" name=\"audience_meta\" id=\"epc-mb-wa-audience-meta\" value=\"\">");
        Echo("<div class=\"epc-mb-count\"><i class=\"fa fa-mobile\"></i> <span id=\"epc-mb-wa-count\">—</span></div>");
        Echo("</div></div></div>");
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-whatsapp\"></i> Template &amp; message</h4></div><div class=\"epc-mb-panel__body\">");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">2</span> Message template</div>");
        Echo("<div class=\"epc-mb-templates\" id=\"epc-mb-wa-templates\">");
        var ti = 0;
        foreach (var (key, tpl) in templates)
        {
            Echo("<button type=\"button\" class=\"epc-mb-tpl" + (ti == 0 ? " is-active" : "") + "\" data-template=\"" + H(key) + "\" data-channel=\"whatsapp\">");
            Echo("<div class=\"epc-mb-tpl__icon\"><i class=\"fa fa-whatsapp\"></i></div>");
            Echo("<div class=\"epc-mb-tpl__label\">" + H(tpl.GetValueOrDefault("label")) + "</div></button>");
            ti++;
        }

        Echo("</div>");
        Echo("<select class=\"epc-mb-template-select form-control\" data-channel=\"whatsapp\" style=\"position:absolute;left:-9999px;width:1px;height:1px;opacity:0\" tabindex=\"-1\" aria-hidden=\"true\">");
        foreach (var (key, tpl) in templates)
        {
            Echo("<option value=\"" + H(key) + "\">" + H(tpl.GetValueOrDefault("label")) + "</option>");
        }

        Echo("</select></div>");
        Echo("<div class=\"epc-mb-step\"><div class=\"epc-mb-step__label\"><span class=\"epc-mb-step__num\">3</span> Message body</div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-wa-body\">WhatsApp text</label>");
        Echo("<textarea name=\"body_text\" id=\"epc-mb-wa-body\" class=\"form-control epc-mb-wa-body\" rows=\"10\" placeholder=\"Leave blank to use template\"></textarea>");
        Echo("<div class=\"epc-mb-vars\"><code>{{customer_name}}</code><code>{{shop_name}}</code><code>{{shop_url}}</code></div></div>");
        Echo("<div class=\"epc-mb-field\"><label for=\"epc-mb-wa-batch\">Batch limit</label>");
        Echo("<input type=\"number\" name=\"batch_limit\" id=\"epc-mb-wa-batch\" class=\"form-control\" value=\"50\" min=\"1\" max=\"100\" style=\"max-width:140px\"></div>");
        Echo("</div>");
        Echo("<div class=\"epc-mb-actions\">");
        Echo("<button type=\"submit\" class=\"btn btn-success\" data-confirm=\"" + (waApi ? "Send this WhatsApp campaign now?" : "Prepare wa.me links for the selected audience?") + "\">");
        Echo("<i class=\"fa fa-whatsapp\"></i> " + (waApi ? "Send WhatsApp campaign" : "Prepare wa.me links") + "</button>");
        Echo("<button type=\"button\" class=\"btn btn-default\" id=\"epc-mb-wa-reload-tpl\"><i class=\"fa fa-refresh\"></i> Reload template</button>");
        Echo("</div></div></div></div>");
        Echo("<aside class=\"epc-mb-panel epc-mb-preview\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-comment\"></i> WhatsApp preview</h4><span class=\"epc-mb-panel__hint\">Chat-style</span></div>");
        Echo("<div class=\"epc-mb-panel__body\"><div class=\"epc-mb-wa-bubble-wrap\"><div class=\"epc-mb-wa-bubble\" id=\"epc-mb-wa-preview-bubble\">Select a template or type a message…</div>");
        Echo("<div class=\"epc-mb-wa-bubble__meta\">Preview · sample customer</div></div></div></aside>");
        Echo("</div></form>");
    }

    public static void EpcMbRenderHistoryTab(List<Dictionary<string, object?>> campaigns)
    {
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-history\"></i> Recent campaigns</h4><span class=\"epc-mb-panel__hint\">Last 15</span></div><div class=\"epc-mb-panel__body\">");
        if (campaigns.Count == 0)
        {
            Echo("<div class=\"epc-mb-empty\"><div class=\"epc-mb-empty__icon\"><i class=\"fa fa-paper-plane\"></i></div>");
            Echo("<div class=\"epc-mb-empty__title\">No campaigns yet</div>");
            Echo("<p>Send your first email or WhatsApp broadcast — results appear here with OK / failed counts.</p>");
            Echo("<p><a class=\"btn btn-primary btn-sm\" href=\"" + H(Url("email")) + "\"><i class=\"fa fa-envelope\"></i> Compose email</a> ");
            Echo("<a class=\"btn btn-default btn-sm\" href=\"" + H(Url("whatsapp")) + "\"><i class=\"fa fa-whatsapp\"></i> Compose WhatsApp</a></p></div>");
            Echo("</div></div>");
            return;
        }

        Echo("<div class=\"epc-mb-history-list\">");
        foreach (var c in campaigns)
        {
            var ch = Str(c.GetValueOrDefault("channel"), "email");
            var isWa = ch == "whatsapp";
            Echo("<div class=\"epc-mb-campaign\">");
            Echo("<div class=\"epc-mb-campaign__icon" + (isWa ? " is-wa" : "") + "\"><i class=\"fa " + (isWa ? "fa-whatsapp" : "fa-envelope") + "\"></i></div>");
            Echo("<div><div class=\"epc-mb-campaign__title\">" + H(UcFirst(ch)) + " · " + H(c.GetValueOrDefault("audience_mode")) + "</div>");
            Echo("<div class=\"epc-mb-campaign__meta\">#" + ToInt(c.GetValueOrDefault("id")) + " · " + H(Fmt(ToLong(c.GetValueOrDefault("created_at"))))
                 + " · " + ToInt(c.GetValueOrDefault("total_targets")) + " targets · <span class=\"epc-mb-status-pill\">" + H(c.GetValueOrDefault("status")) + "</span></div></div>");
            Echo("<div class=\"epc-mb-campaign__stats\"><span class=\"ok\">" + ToInt(c.GetValueOrDefault("sent_ok")) + " OK</span><span class=\"fail\">" + ToInt(c.GetValueOrDefault("sent_fail")) + " fail</span></div>");
            Echo("</div>");
        }

        Echo("</div></div></div>");
    }

    public static void EpcMbRenderGuideTab(Dictionary<string, object?> shop, string emailSettingsUrl, string guideUrl, bool waApi, string integrationsUrl)
    {
        var steps = new (string Title, string Icon, string Body)[]
        {
            ("Configure email (SMTP)", "fa-envelope",
                "<p>Open <a href=\"" + H(emailSettingsUrl) + "\">Email / SMTP settings</a> (also linked from <a href=\"" + H(integrationsUrl) + "\">Integrations Hub</a>). Enter your tenant mailbox (Gmail App Password, Hostinger, Microsoft 365, etc.).</p><p>Run <strong>Test send</strong> and wait for the SMTP ready badge on the Email tab before bulk campaigns.</p>"),
            ("Choose your audience", "fa-users",
                "<p>On Email or WhatsApp, pick who receives the message:</p><ul>"
                + "<li><strong>All with email/phone</strong> — every customer with contact data</li>"
                + "<li><strong>With orders</strong> — buyers only</li>"
                + "<li><strong>Group / segment</strong> — price or access group</li>"
                + "<li><strong>Manual list</strong> — paste emails or phones (one per line)</li></ul>"
                + "<p>The live recipient count updates as you change the audience. Each tenant uses its own customer database.</p>"),
            ("Pick a brochure template", "fa-th-large",
                "<p>Click a template card to load that brochure into the composer and live preview. Switching cards replaces subject / body so you always see the selected design.</p>"
                + "<p><strong>Email:</strong> HTML brochures (sale, new arrivals, service reminder, blank).<br>"
                + "<strong>WhatsApp:</strong> bilingual EN+AR messages (promo, brochure share, follow-up, event, blank).</p>"
                + "<p>Merge tags: <code>{{customer_name}}</code>, <code>{{shop_name}}</code>, <code>{{shop_url}}</code>. After you edit by hand, use <em>Reload template</em> to restore the selected card.</p>"),
            ("Preview, then send email", "fa-paper-plane",
                "<p>Watch the <strong>Live email preview</strong> while you edit subject and HTML. Set a batch limit (1–100). Click <strong>Send email campaign</strong> — sending is paced (~5/sec) to protect SMTP reputation. Results land in <strong>History</strong>.</p>"),
            ("Send WhatsApp marketing", "fa-whatsapp",
                waApi
                    ? "<p>WhatsApp Cloud API is <strong>on</strong> for this tenant — messages send automatically via Meta Graph API. Review the chat-style preview, then send. Delivery details are logged by the WhatsApp notify module.</p>"
                    : "<p><strong>wa.me mode (Phase 1):</strong> Prepare links for each recipient, then open the green buttons to send from WhatsApp Web/desktop.</p>"
                      + "<p>For automatic bulk send, enable WhatsApp API in Configuration (<code>epc_whatsapp_api_enabled</code>) via Integrations / WhatsApp settings.</p>"),
            ("UAE compliance — opt-in &amp; consent", "fa-balance-scale",
                "<div class=\"epc-mb-guide-compliance\"><strong>UAE / TRA / DIFC &amp; Meta best practice</strong><ul>"
                + "<li>Only message customers who <strong>opted in</strong> (registration, order, or explicit consent).</li>"
                + "<li>Show your trade name and a clear opt-out (reply STOP or contact email).</li>"
                + "<li>Commercial WhatsApp needs valid opt-in under Meta Business Policy.</li>"
                + "<li>Respect quiet hours; avoid misleading promotions (UAE Consumer Protection).</li>"
                + "<li>Keep <strong>Campaign history</strong> for audit trails.</li></ul></div>")
        };
        Echo("<div class=\"epc-mb-panel\"><div class=\"epc-mb-panel__head\"><h4><i class=\"fa fa-book\"></i> Operator guide</h4>");
        Echo("<span class=\"epc-mb-panel__hint\"><a href=\"" + H(guideUrl) + "\">" + H(guideUrl) + "</a></span></div>");
        Echo("<div class=\"epc-mb-panel__body epc-mb-guide\">");
        Echo("<div class=\"epc-mb-guide__intro\"><strong>" + H(shop.GetValueOrDefault("shop_name")) + "</strong> — bulk email brochures and WhatsApp from tenant CP. "
             + "Follow the steps below for a reliable, compliant campaign.</div>");
        for (var i = 0; i < steps.Length; i++)
        {
            Echo("<div class=\"epc-mb-guide-step\">");
            Echo("<div class=\"epc-mb-guide-step__num\">" + (i + 1) + "</div>");
            Echo("<div><h5><i class=\"fa " + H(steps[i].Icon) + "\"></i> " + H(steps[i].Title) + "</h5>");
            Echo("<div>" + steps[i].Body + "</div></div></div>");
        }

        Echo("</div></div>");
    }

    public static string Capture(Action render)
    {
        LastOutput = "";
        render();
        return LastOutput;
    }

    private static Dictionary<string, Dictionary<string, object?>> DefaultEmail()
        => new(StringComparer.Ordinal)
        {
            ["promo_sale"] = new(StringComparer.Ordinal) { ["label"] = "Sale" },
            ["blank"] = new(StringComparer.Ordinal) { ["label"] = "Blank" }
        };

    private static Dictionary<string, Dictionary<string, object?>> DefaultWa()
        => new(StringComparer.Ordinal) { ["promo_bilingual"] = new(StringComparer.Ordinal) { ["label"] = "Promo EN+AR" } };

    private static string Url(string tab)
    {
        if (HubUrl is not null)
        {
            return HubUrl(tab);
        }

        var baseUrl = "/cp/control/portal/epc_marketing_broadcast";
        return tab != "" && tab != "email" ? baseUrl + "?tab=" + Uri.EscapeDataString(tab) : baseUrl;
    }

    private static string Sanitize(string raw)
        => Regex.Replace((raw ?? "").ToLowerInvariant(), "[^a-z_]", "");

    private static void Echo(string html)
        => LastOutput += html;

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string Fmt(long ts)
        => DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string UcFirst(string text)
        => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static bool PhpEmpty(object? value)
    {
        if (value is null or false)
        {
            return true;
        }

        return value switch
        {
            string s => s is "" or "0",
            int i => i == 0,
            long l => l == 0,
            IEnumerable<object?> e => !e.Any(),
            _ => false
        };
    }

    private static int ToInt(object? value)
    {
        if (value is null or false or "")
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static long ToLong(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
