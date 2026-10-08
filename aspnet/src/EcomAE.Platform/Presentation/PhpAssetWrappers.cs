using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// The PHP files that only serve one CSS or JS file of the repository at a <c>/content/</c> or <c>/cp/</c> URL nginx
/// lets through (<c>epc_*_css.php</c>, <c>epc_*_js.php</c>, the portal <c>*_config.php</c> loaders). Each answers like
/// its PHP file: the first source that exists, its content type and cache header, and, where the PHP sends one, the
/// ETag <c>"md5(mtime|size|version)"</c> (no version part where the PHP leaves it out) with a 304 when
/// <c>If-None-Match</c> equals it; without a source, the PHP's 404 "… missing" text (the bare loaders answer empty).
/// </summary>
public static class PhpAssetWrappers
{
    public sealed record Wrapper(string Url, string[] Sources, string ContentType, string? CacheControl, bool ETag, string? Version, string? Missing);

    public static readonly IReadOnlyList<Wrapper> All =
    [
        new("/content/general_pages/epc_accessories_cp_css.php", ["cp/content/shop/accessories/epc_accessories_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721accPhotos1", "epc_accessories_cp.css missing"),
        new("/content/general_pages/epc_agent_cp_css.php", ["cp/content/shop/parts_agent/epc_agent_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260722pacFix3", "epc_agent_cp.css missing"),
        new("/content/general_pages/epc_agent_cp_js.php", ["cp/content/shop/parts_agent/parts_agent_chats_cp.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260722pacFix3", "parts_agent_chats_cp.js missing"),
        new("/content/general_pages/epc_auto_price_engine_css.php", ["cp/content/control/portal/epc_auto_price_engine.css"], "text/css; charset=utf-8", "public, max-age=604800, immutable", true, "20260722disc1", "epc_auto_price_engine.css missing"),
        new("/content/general_pages/epc_auto_price_shell_js.php", ["cp/content/control/portal/epc_auto_price_shell.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260606apai2", "epc_auto_price_shell.js missing"),
        new("/content/general_pages/epc_comms_notify_css.php", ["cp/content/control/epc_comms_notify.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721cn1", "epc_comms_notify.css missing"),
        new("/content/general_pages/epc_config_edit_css.php", ["cp/content/control/epc_config_edit.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721cfg1", "epc_config_edit.css missing"),
        new("/content/general_pages/epc_cp_guideline_css.php", ["cp/content/control/cp_guideline.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721cpg1", "cp_guideline.css missing"),
        new("/content/general_pages/epc_cp_homer.php", ["content/general_pages/epc_cp_homer.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, null, "epc_cp_homer.js missing"),
        new("/content/general_pages/epc_cp_style_css.php", ["cp/templates/bootstrap_admin/styles/style.css"], "text/css; charset=utf-8", "public, max-age=604800, immutable", true, "20260606cpframe1", "style.css missing"),
        new("/content/general_pages/epc_cp_topnav_js.php", ["cp/js/epc_cp_topnav.js"], "application/javascript; charset=utf-8", "public, max-age=3600", true, "20260720tenantnav1", "epc_cp_topnav.js missing"),
        new("/content/general_pages/epc_crosses_cp_css.php", ["cp/content/shop/crosses/epc_crosses_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721cx1", "epc_crosses_cp.css missing"),
        new("/content/general_pages/epc_document_control_cp_css.php", ["cp/content/shop/document_control/epc_document_control.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260608dc1", "epc_document_control.css missing"),
        new("/content/general_pages/epc_erp_ai_assistant_js.php", ["cp/js/epc_erp_ai_assistant.js"], "application/javascript; charset=utf-8", "public, max-age=604800, immutable", true, "20260621ai1", "epc_erp_ai_assistant.js missing"),
        new("/content/general_pages/epc_erp_seed_form_js.php", ["cp/js/epc_erp_seed_form.js"], "application/javascript; charset=utf-8", "public, max-age=604800, immutable", true, "20260621seed1", "epc_erp_seed_form.js missing"),
        new("/content/general_pages/epc_erp_shell_nav_js.php", ["cp/js/epc_erp_shell_nav.js"], "application/javascript; charset=utf-8", "public, max-age=604800, immutable", true, "20260720topnavmerge3", "epc_erp_shell_nav.js missing"),
        new("/content/general_pages/epc_erp_voice_command_js.php", ["cp/js/epc_erp_voice_command.js"], "application/javascript; charset=utf-8", "public, max-age=604800, immutable", true, "20260621voice1", "epc_erp_voice_command.js missing"),
        new("/content/general_pages/epc_marketing_broadcast_js.php", ["cp/content/control/portal/epc_marketing_broadcast.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260722mb2", "epc_marketing_broadcast.js missing"),
        new("/content/general_pages/epc_marketing_hub_css.php", ["cp/content/shop/marketing/epc_marketing_hub.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260722mkt1", "epc_marketing_hub.css missing"),
        new("/content/general_pages/epc_marketing_hub_js.php", ["cp/content/shop/marketing/epc_marketing_hub.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260722mkt1", "epc_marketing_hub.js missing"),
        new("/content/general_pages/epc_orders_cp_css.php", ["cp/content/shop/order_process/epc_orders_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260717orders1", "epc_orders_cp.css missing"),
        new("/content/general_pages/epc_payments_cp_js.php", ["cp/content/shop/payments/epc_payments_cp.js"], "application/javascript; charset=utf-8", "public, max-age=86400", false, null, "epc_payments_cp.js missing"),
        new("/content/general_pages/epc_portal_module_pages_css.php", ["cp/content/control/portal/epc_portal_module_pages.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260606cpframe1", "epc_portal_module_pages.css missing"),
        new("/content/general_pages/epc_portal_settings_css.php", ["cp/content/control/portal/portal_settings.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260606cpframe1", "portal_settings.css missing"),
        new("/content/general_pages/epc_prices_cp_css.php", ["cp/content/shop/prices_upload/epc_prices_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260607pricesedit1", "epc_prices_cp.css missing"),
        new("/content/general_pages/epc_prices_edit_css.php", ["cp/content/shop/prices_edit/epc_prices_edit.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260607pricesedit1", "epc_prices_edit.css missing"),
        new("/content/general_pages/epc_social_media_hub_js.php", ["cp/content/control/portal/epc_social_media_hub.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260608social1", "epc_social_media_hub.js missing"),
        new("/content/general_pages/epc_statuses_cp_css.php", ["cp/content/shop/order_process/epc_statuses_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260717statuses1", "epc_statuses_cp.css missing"),
        new("/content/general_pages/epc_tax_toolkit_manage_css.php", ["cp/content/control/portal/epc_tax_toolkit_manage.css"], "text/css; charset=utf-8", "public, max-age=604800, immutable", true, null, "epc_tax_toolkit_manage.css missing"),
        new("/content/general_pages/epc_visual_page_editor_css.php", ["cp/content/control/portal/epc_visual_page_editor.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260722vpe1", "epc_visual_page_editor.css missing"),
        new("/content/general_pages/epc_web_tracker_cp_css.php", ["content/general_pages/epc_web_tracker_cp.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260719wt1", "epc_web_tracker_cp.css missing"),
        new("/content/general_pages/epc_workshop_css.php", ["cp/content/shop/workshop/epc_workshop.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260721ws1", "epc_workshop.css missing"),
        new("/content/shop/finance/epc_erp_dashboard_premium_css.php", ["content/shop/finance/epc_erp_dashboard_premium.css", "content/shop/finance/erp/theme/erp_dashboard_premium.css", "cp/content/shop/finance/erp/theme/erp_dashboard_premium.css"], "text/css; charset=utf-8", "public, max-age=604800, immutable", true, "20260720colors2", "erp_dashboard_premium.css missing"),
        new("/content/shop/pos/epc_pos_css.php", ["content/shop/pos/epc_pos.css"], "text/css; charset=utf-8", "public, max-age=86400", true, "20260722posui1", "epc_pos.css missing"),
        new("/content/shop/pos/epc_pos_tenant_manage_js.php", ["content/shop/pos/epc_pos_tenant_manage.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, null, "epc_pos_tenant_manage.js missing"),
        new("/content/shop/pos/epc_pos_terminal_js.php", ["content/shop/pos/epc_pos_terminal.js"], "application/javascript; charset=utf-8", "public, max-age=86400", true, "20260722posui1", "epc_pos_terminal.js missing"),
        new("/cp/content/control/portal/epc_integrations_hub_config.php", ["cp/content/control/portal/epc_integrations_hub.js"], "application/javascript; charset=utf-8", null, false, null, null),
        new("/cp/content/control/portal/epc_mobile_apps_config.php", ["cp/content/control/portal/epc_integrations_hub.js"], "application/javascript; charset=utf-8", null, false, null, null),
        new("/cp/content/control/portal/epc_tenant_email_settings_config.php", ["cp/content/control/portal/epc_integrations_hub.js"], "application/javascript; charset=utf-8", null, false, null, null),
        new("/cp/content/control/portal/epc_tenant_features_config.php", ["cp/content/control/portal/epc_integrations_hub.js"], "application/javascript; charset=utf-8", null, false, null, null),
    ];

    private static readonly HashSet<string> Urls = All.Select(w => w.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsWrapperPath(string? path) => path is not null && Urls.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints, string repoRoot)
    {
        foreach (var wrapper in All)
        {
            endpoints.MapMethods(wrapper.Url, ["GET", "HEAD", "POST"], (HttpContext context) => WriteAsync(context, wrapper, repoRoot))
                .DisableAntiforgery()
                .AllowAnonymous();
        }
    }

    /// <summary>PHP's <c>'"' . md5($mtime . '|' . filesize($path) [. '|' . $ver]) . '"'</c>.</summary>
    public static string ETagFor(long mtime, long size, string? version)
    {
        var text = mtime.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + size.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + (version is null ? string.Empty : "|" + version);
        return "\"" + Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text))) + "\"";
    }

    public static async Task WriteAsync(HttpContext context, Wrapper wrapper, string repoRoot)
    {
        var response = context.Response;
        var path = wrapper.Sources
            .Select(source => Path.GetFullPath(Path.Combine(repoRoot, source)))
            .FirstOrDefault(full => full.StartsWith(repoRoot, StringComparison.Ordinal) && File.Exists(full));
        if (path is null)
        {
            if (wrapper.Missing is null)
            {
                response.ContentType = wrapper.ContentType;
                return;
            }

            response.StatusCode = StatusCodes.Status404NotFound;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(wrapper.Missing, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        response.ContentType = wrapper.ContentType;
        if (wrapper.CacheControl is not null)
        {
            response.Headers.CacheControl = wrapper.CacheControl;
        }

        if (wrapper.ETag)
        {
            var info = new FileInfo(path);
            var etag = ETagFor(new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds(), info.Length, wrapper.Version);
            response.Headers.ETag = etag;
            var sent = context.Request.Headers.IfNoneMatch.ToString();
            if (sent.Length > 0 && sent.Trim(' ', '\t', '\n', '\r', '\0', '\x0B') == etag)
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }
        }

        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }
}
