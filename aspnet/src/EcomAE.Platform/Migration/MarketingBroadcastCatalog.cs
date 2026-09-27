namespace EcomAE.Platform.Migration;

/// <summary>PHP <c>epc_mb_email_templates()</c> brochure.</summary>
public sealed record MarketingEmailTemplate(string Key, string Label, string Subject, string Preview, string Html);

/// <summary>PHP <c>epc_mb_whatsapp_templates()</c> message.</summary>
public sealed record MarketingWhatsappTemplate(string Key, string Label, string Body);

/// <summary>PHP hub tab (<c>epc_mb_render_hub</c> <c>$tabs</c>).</summary>
public sealed record MarketingBroadcastTab(string Key, string Label, string Icon);

/// <summary>PHP audience radio card (<c>$modes</c> in the email / WhatsApp composer).</summary>
public sealed record MarketingAudienceMode(string Key, string Label, string Hint);

/// <summary>PHP guide step (<c>epc_mb_render_guide_tab</c>).</summary>
public sealed record MarketingGuideStep(string Title, string Icon, string BodyHtml);

/// <summary>
/// Typed twin of the PHP marketing broadcast content
/// (content/shop/marketing/epc_marketing_broadcast_templates.php plus the composer/guide copy in
/// cp/content/control/portal/epc_marketing_broadcast_panel.php).
/// </summary>
public static class MarketingBroadcastCatalog
{
    public static IReadOnlyList<MarketingBroadcastTab> Tabs { get; } =
    [
        new("email", "Email", "fa-envelope"),
        new("whatsapp", "WhatsApp", "fa-whatsapp"),
        new("history", "History", "fa-history"),
        new("guide", "Guide", "fa-book"),
    ];

    public static IReadOnlyList<MarketingAudienceMode> EmailAudienceModes { get; } =
    [
        new("all", "All with email", "Every customer with an email on file"),
        new("with_orders", "With orders", "Customers who have placed orders"),
        new("group", "Group / segment", "Price or access group"),
        new("manual", "Manual list", "Paste emails, one per line"),
    ];

    public static IReadOnlyList<MarketingAudienceMode> WhatsappAudienceModes { get; } =
    [
        new("all", "All with phone", "Every customer with a phone number"),
        new("with_orders", "With orders", "Customers who have placed orders"),
        new("group", "Group / segment", "Price or access group"),
        new("manual", "Manual list", "Paste phones, one per line"),
    ];

    public static IReadOnlyList<MarketingEmailTemplate> EmailTemplates { get; } =
    [
        new(
            "promo_sale",
            "Seasonal sale brochure",
            "{{shop_name}} — Limited-time offers inside",
            "Exclusive deals on parts & accessories — open to see your savings.",
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"font-family:Arial,sans-serif;background:#f8fafc;padding:24px\">"
            + "<div style=\"max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e2e8f0\">"
            + "<div style=\"background:linear-gradient(135deg,#2563eb,#7c3aed);color:#fff;padding:28px 24px\">"
            + "<h1 style=\"margin:0;font-size:24px\">{{shop_name}}</h1>"
            + "<p style=\"margin:8px 0 0;opacity:.9\">Seasonal sale — limited stock</p></div>"
            + "<div style=\"padding:24px;color:#334155;line-height:1.6\">"
            + "<p>Hello {{customer_name}},</p>"
            + "<p>We selected top offers for you this week. Browse our catalogue and use code <strong>SALE10</strong> at checkout.</p>"
            + "<p style=\"text-align:center;margin:28px 0\"><a href=\"{{shop_url}}\" style=\"background:#2563eb;color:#fff;padding:12px 28px;border-radius:8px;text-decoration:none;font-weight:700\">Shop now</a></p>"
            + "<p>Questions? Reply to this email or WhatsApp us.</p>"
            + "<p>— {{shop_name}} team</p></div>"
            + "<div style=\"background:#f1f5f9;padding:14px 24px;font-size:11px;color:#64748b\">You received this because you are a registered customer. Unsubscribe by contacting us.</div>"
            + "</div></body></html>"),
        new(
            "new_arrivals",
            "New arrivals brochure",
            "{{shop_name}} — New stock just landed",
            "Fresh inventory — see what's new in our warehouse.",
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"font-family:Arial,sans-serif;background:#f0fdf4;padding:24px\">"
            + "<div style=\"max-width:600px;margin:0 auto;background:#fff;border-radius:12px;border:1px solid #bbf7d0;padding:28px\">"
            + "<h2 style=\"color:#166534;margin:0 0 12px\">{{shop_name}} — New arrivals</h2>"
            + "<p style=\"color:#334155\">Hi {{customer_name}},</p>"
            + "<p style=\"color:#334155\">New parts and accessories are now in stock. Visit our shop to see the latest additions.</p>"
            + "<p><a href=\"{{shop_url}}\" style=\"color:#059669;font-weight:700\">Browse new arrivals →</a></p>"
            + "</div></body></html>"),
        new(
            "service_reminder",
            "Service reminder",
            "{{shop_name}} — Time for your next service?",
            "Keep your vehicle running smoothly — book parts or service today.",
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"font-family:Arial,sans-serif;background:#fff7ed;padding:24px\">"
            + "<div style=\"max-width:600px;margin:0 auto;background:#fff;border-radius:12px;border:1px solid #fed7aa;padding:28px\">"
            + "<h2 style=\"color:#c2410c;margin:0 0 12px\">Service reminder</h2>"
            + "<p>Dear {{customer_name}},</p>"
            + "<p>Regular maintenance keeps your vehicle safe. {{shop_name}} has filters, oils, and wear parts ready to ship.</p>"
            + "<p><a href=\"{{shop_url}}\">Order online</a> or call our team.</p></div></body></html>"),
        new(
            "blank",
            "Blank HTML brochure",
            "{{shop_name}} — Message for you",
            "A message from {{shop_name}}.",
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"font-family:Arial,sans-serif;padding:24px\">"
            + "<div style=\"max-width:600px;margin:0 auto\"><h2>{{shop_name}}</h2>"
            + "<p>Hello {{customer_name}},</p><p>Your message here…</p></div></body></html>"),
    ];

    public static IReadOnlyList<MarketingWhatsappTemplate> WhatsappTemplates { get; } =
    [
        new(
            "promo_bilingual",
            "Promo offer (EN + AR)",
            "Hello {{customer_name}}! 🎉\n\n{{shop_name}} has special offers this week.\nVisit: {{shop_url}}\n\n"
            + "مرحباً {{customer_name}}! عروض خاصة من {{shop_name}} هذا الأسبوع.\n{{shop_url}}"),
        new(
            "brochure_share",
            "Brochure / catalogue share",
            "Hi {{customer_name}},\n\nHere is our latest brochure from {{shop_name}}.\nBrowse: {{shop_url}}\n\n"
            + "مرحباً، إليك أحدث كتالوج من {{shop_name}}.\n{{shop_url}}"),
        new(
            "follow_up",
            "Order follow-up",
            "Hello {{customer_name}},\n\nThank you for shopping with {{shop_name}}. Need anything else? Reply here or visit {{shop_url}}.\n\n"
            + "شكراً لتسوقكم مع {{shop_name}}. للمساعدة ردّوا على هذه الرسالة."),
        new(
            "event_invite",
            "Event / open day invitation",
            "You're invited! {{shop_name}} open day — visit us or shop online: {{shop_url}}\n\n"
            + "دعوة من {{shop_name}} — زورونا أو تسوقوا أونلاين: {{shop_url}}"),
        new(
            "blank",
            "Blank WhatsApp message",
            "Hello {{customer_name}},\n\nMessage from {{shop_name}}.\n{{shop_url}}"),
    ];

    public static MarketingEmailTemplate EmailTemplate(string? key)
        => EmailTemplates.FirstOrDefault(t => t.Key == (key ?? string.Empty).Trim())
           ?? EmailTemplates.First(t => t.Key == "blank");

    public static MarketingWhatsappTemplate WhatsappTemplate(string? key)
        => WhatsappTemplates.FirstOrDefault(t => t.Key == (key ?? string.Empty).Trim())
           ?? WhatsappTemplates.First(t => t.Key == "blank");

    /// <summary>PHP <c>epc_mb_apply_template_vars()</c>.</summary>
    public static string ApplyVars(string text, string customerName, string shopName, string shopUrl)
        => (text ?? string.Empty)
            .Replace("{{customer_name}}", customerName, StringComparison.Ordinal)
            .Replace("{{shop_name}}", shopName, StringComparison.Ordinal)
            .Replace("{{shop_url}}", shopUrl, StringComparison.Ordinal);

    /// <summary>PHP <c>epc_mb_render_hub()</c> tab normalisation (<c>[^a-z_]</c> stripped, default email).</summary>
    public static string NormalizeTab(string? tab)
    {
        var cleaned = new string((tab ?? string.Empty).ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or '_').ToArray());
        return Tabs.Any(t => t.Key == cleaned) ? cleaned : "email";
    }

    /// <summary>PHP guide steps; WhatsApp copy switches on the Cloud API flag.</summary>
    public static IReadOnlyList<MarketingGuideStep> GuideSteps(bool whatsappApiEnabled, string emailSettingsUrl, string integrationsUrl)
        =>
        [
            new(
                "Configure email (SMTP)",
                "fa-envelope",
                $"<p>Open <a href=\"{emailSettingsUrl}\">Email / SMTP settings</a> (also linked from <a href=\"{integrationsUrl}\">Integrations Hub</a>). "
                + "Enter your tenant mailbox (Gmail App Password, Hostinger, Microsoft 365, etc.).</p>"
                + "<p>Run <strong>Test send</strong> and wait for the SMTP ready badge on the Email tab before bulk campaigns.</p>"),
            new(
                "Choose your audience",
                "fa-users",
                "<p>On Email or WhatsApp, pick who receives the message:</p><ul>"
                + "<li><strong>All with email/phone</strong> — every customer with contact data</li>"
                + "<li><strong>With orders</strong> — buyers only</li>"
                + "<li><strong>Group / segment</strong> — price or access group</li>"
                + "<li><strong>Manual list</strong> — paste emails or phones (one per line)</li></ul>"
                + "<p>The live recipient count updates as you change the audience. Each tenant uses its own customer database.</p>"),
            new(
                "Pick a brochure template",
                "fa-th-large",
                "<p>Click a template card to load that brochure into the composer and live preview. Switching cards replaces subject / body so you always see the selected design.</p>"
                + "<p><strong>Email:</strong> HTML brochures (sale, new arrivals, service reminder, blank).<br>"
                + "<strong>WhatsApp:</strong> bilingual EN+AR messages (promo, brochure share, follow-up, event, blank).</p>"
                + "<p>Merge tags: <code>{{customer_name}}</code>, <code>{{shop_name}}</code>, <code>{{shop_url}}</code>. "
                + "After you edit by hand, use <em>Reload template</em> to restore the selected card.</p>"),
            new(
                "Preview, then send email",
                "fa-paper-plane",
                "<p>Watch the <strong>Live email preview</strong> while you edit subject and HTML. Set a batch limit (1–100). "
                + "Click <strong>Send email campaign</strong> — sending is paced (~5/sec) to protect SMTP reputation. Results land in <strong>History</strong>.</p>"),
            new(
                "Send WhatsApp marketing",
                "fa-whatsapp",
                whatsappApiEnabled
                    ? "<p>WhatsApp Cloud API is <strong>on</strong> for this tenant — messages send automatically via Meta Graph API. "
                      + "Review the chat-style preview, then send. Delivery details are logged by the WhatsApp notify module.</p>"
                    : "<p><strong>wa.me mode (Phase 1):</strong> Prepare links for each recipient, then open the green buttons to send from WhatsApp Web/desktop.</p>"
                      + "<p>For automatic bulk send, enable WhatsApp API in Configuration (<code>epc_whatsapp_api_enabled</code>) via Integrations / WhatsApp settings.</p>"),
            new(
                "UAE compliance — opt-in &amp; consent",
                "fa-balance-scale",
                "<div class=\"epc-mb-guide-compliance\"><strong>UAE / TRA / DIFC &amp; Meta best practice</strong><ul>"
                + "<li>Only message customers who <strong>opted in</strong> (registration, order, or explicit consent).</li>"
                + "<li>Show your trade name and a clear opt-out (reply STOP or contact email).</li>"
                + "<li>Commercial WhatsApp needs valid opt-in under Meta Business Policy.</li>"
                + "<li>Respect quiet hours; avoid misleading promotions (UAE Consumer Protection).</li>"
                + "<li>Keep <strong>Campaign history</strong> for audit trails.</li></ul></div>"),
        ];
}
