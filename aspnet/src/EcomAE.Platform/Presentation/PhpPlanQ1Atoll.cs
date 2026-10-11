using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-atoll social media hub panel. PHP identifiers kept for the inventory:
/// <c>epc_social_media_explore_hint_html</c>, <c>epc_social_render_video_card</c>,
/// <c>epc_social_media_render_hub</c>, <c>epc_social_render_pack_tab</c>,
/// <c>epc_social_render_tiktok_tab</c>, <c>epc_social_render_instagram_tab</c>,
/// <c>epc_social_render_accounts_tab</c>, <c>epc_social_render_ai_tab</c>,
/// <c>epc_social_render_drafts_tab</c>, <c>epc_social_render_guide_tab</c>.
/// Path: <c>cp/content/control/portal/epc_social_media_hub_panel.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected
/// and is not written as a leftover basename.
/// </summary>
public static class PhpPlanQ1Atoll
{
    public const string SocialMediaHubPanelPath = "cp/content/control/portal/epc_social_media_hub_panel.php";

    public static Func<bool>? IsAdmin { get; set; }
    public static Func<bool>? IsSuperCpHost { get; set; }
    public static Func<object?>? SocialPdo { get; set; }
    public static Func<string>? ResolveSiteKey { get; set; }
    public static Func<string, Dictionary<string, object?>>? BrandContext { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? ListAccounts { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? ListDrafts { get; set; }
    public static Func<List<Dictionary<string, object?>>>? TrendingFormats { get; set; }
    public static Func<string, Dictionary<string, object?>, List<string>>? IndustryHooks { get; set; }
    public static Func<string>? Backend { get; set; }
    public static Func<string, string?, string>? HubUrl { get; set; }
    public static Func<string, string>? TenantHubUrl { get; set; }
    public static Func<string>? CsrfToken { get; set; }
    public static Func<bool>? VerifyCsrf { get; set; }
    public static Func<string, Dictionary<string, string>, Dictionary<string, object?>>? SaveAccount { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? TestAccount { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? DeleteAccount { get; set; }
    public static Func<string, Dictionary<string, string>, Dictionary<string, object?>>? SaveDraft { get; set; }
    public static Func<string, int, Dictionary<string, object?>>? PublishDraft { get; set; }
    public static Func<string, Dictionary<string, string>, Dictionary<string, object?>>? PublishNow { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? PackPosts { get; set; }
    public static Func<string, Dictionary<string, object?>, List<Dictionary<string, object?>>>? PackPostsForBrand { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? PackPlatforms { get; set; }
    public static Func<string, Dictionary<string, object?>, string>? AdaptText { get; set; }
    public static Func<string>? XThreadStarter { get; set; }
    public static Func<Dictionary<string, string>>? TiktokSpecs { get; set; }
    public static Func<List<Dictionary<string, object?>>>? VideoLibrary { get; set; }
    public static Func<List<Dictionary<string, object?>>>? InstagramReelsIdeas { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Platforms { get; set; }
    public static Func<string, string, Dictionary<string, object?>>? AccountPublicMeta { get; set; }
    public static Action<Dictionary<string, object?>>? FrameOpen { get; set; }
    public static Action? FrameClose { get; set; }
    public static Func<List<Dictionary<string, object?>>>? ListTenants { get; set; }
    public static Func<long>? Clock { get; set; }
    public static string RequestMethod { get; set; } = "GET";
    public static Dictionary<string, string> Query { get; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Post { get; } = new(StringComparer.Ordinal);
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsAdmin = null;
        IsSuperCpHost = null;
        SocialPdo = null;
        ResolveSiteKey = null;
        BrandContext = null;
        ListAccounts = null;
        ListDrafts = null;
        TrendingFormats = null;
        IndustryHooks = null;
        Backend = null;
        HubUrl = null;
        TenantHubUrl = null;
        CsrfToken = null;
        VerifyCsrf = null;
        SaveAccount = null;
        TestAccount = null;
        DeleteAccount = null;
        SaveDraft = null;
        PublishDraft = null;
        PublishNow = null;
        PackPosts = null;
        PackPostsForBrand = null;
        PackPlatforms = null;
        AdaptText = null;
        XThreadStarter = null;
        TiktokSpecs = null;
        VideoLibrary = null;
        InstagramReelsIdeas = null;
        Platforms = null;
        AccountPublicMeta = null;
        FrameOpen = null;
        FrameClose = null;
        ListTenants = null;
        Clock = null;
        RequestMethod = "GET";
        Query.Clear();
        Post.Clear();
        LastOutput = "";
    }

    public static string EpcSocialMediaExploreHintHtml()
        => "<div class=\"epc-social-explore-hint\" role=\"status\">"
           + "<span class=\"epc-social-explore-hint__icon\" aria-hidden=\"true\"><i class=\"fa fa-hand-o-down\"></i></span>"
           + "<span class=\"epc-social-explore-hint__text\">Use the tabs — ready captions, sample reels, encrypted accounts, AI advisor, and a short video guide</span>"
           + "</div>";

    public static void EpcSocialRenderVideoCard(Dictionary<string, object?> video, bool vertical = false)
    {
        var url = Str(video.GetValueOrDefault("url"));
        if (url == "")
        {
            return;
        }

        var title = Str(video.GetValueOrDefault("title"), "Video");
        var blurb = Str(video.GetValueOrDefault("blurb"));
        var cls = vertical ? "epc-social-video epc-social-video--vertical" : "epc-social-video";
        Echo("<div class=\"" + cls + "\">");
        Echo("<div class=\"epc-social-video__player\"><video controls playsinline preload=\"metadata\" src=\"" + H(url) + "\">");
        Echo("Your browser does not support HTML5 video.</video></div>");
        Echo("<div class=\"epc-social-video__meta\"><strong>" + H(title) + "</strong>");
        if (blurb != "")
        {
            Echo("<p class=\"text-muted small\" style=\"margin:6px 0 8px\">" + H(blurb) + "</p>");
        }

        Echo("<div class=\"epc-social-video__actions\">");
        Echo("<a class=\"btn btn-xs btn-default\" href=\"" + H(url) + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-external-link\"></i> Open</a> ");
        Echo("<button type=\"button\" class=\"btn btn-xs btn-primary epc-social-use-video\" data-url=\"" + H(url) + "\"><i class=\"fa fa-link\"></i> Use as media URL</button> ");
        Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-copy\" data-caption=\"" + H(url) + "\"><i class=\"fa fa-copy\"></i> Copy URL</button>");
        Echo("</div></div></div>");
    }

    public static void EpcSocialMediaRenderHub(Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var isSuper = !PhpEmpty(opts.GetValueOrDefault("is_super")) || IsSuperCpHost?.Invoke() == true;
        var embed = !PhpEmpty(opts.GetValueOrDefault("embed_tenant_hub"));
        if (IsAdmin?.Invoke() != true)
        {
            Echo(isSuper
                ? "<div class=\"alert alert-warning\">Please log in to Super CP to use Social media hub.</div>"
                : "<div class=\"alert alert-warning\">Admin login required.</div>");
            return;
        }

        if (SocialPdo?.Invoke() is null)
        {
            Echo("<div class=\"alert alert-danger\">Database unavailable.</div>");
            return;
        }

        var siteKey = ResolveSiteKey?.Invoke() ?? "acme_parts";
        var brand = BrandOf(siteKey);
        var tab = SanitizeTab(Query.GetValueOrDefault("tab") ?? Str(opts.GetValueOrDefault("tab"), "pack"));
        if (tab == "")
        {
            tab = "pack";
        }

        var accounts = ListAccounts?.Invoke(siteKey) ?? [];
        var drafts = ListDrafts?.Invoke(siteKey) ?? [];
        var trends = TrendingFormats?.Invoke() ?? [Trend("Reel", "IG/TT", "Keep it under 15s")];
        var hooks = IndustryHooks?.Invoke(Str(brand.GetValueOrDefault("industry")), brand) ?? ["Same-day delivery in " + Str(brand.GetValueOrDefault("industry"))];
        var backend = Backend?.Invoke() ?? "cp";
        var csrf = CsrfToken?.Invoke() ?? "tok-1";
        var integrationsUrl = "/" + backend + "/control/portal/epc_integrations_hub";
        var guideUrl = Url("guide", isSuper ? siteKey : null);
        Dictionary<string, object?>? flash = null;
        if (RequestMethod == "POST" && Post.ContainsKey("epc_social_action"))
        {
            if (VerifyCsrf?.Invoke() != true)
            {
                flash = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "CSRF validation failed. Refresh and try again." };
            }
            else
            {
                var action = Post.GetValueOrDefault("epc_social_action") ?? "";
                flash = action switch
                {
                    "save_account" => SaveAccount?.Invoke(siteKey, Post) ?? Ok("saved:" + siteKey + ":" + (Post.GetValueOrDefault("platform") ?? "")),
                    "test_account" => TestAccount?.Invoke(siteKey, Post.GetValueOrDefault("platform") ?? "") ?? Ok("tested:" + (Post.GetValueOrDefault("platform") ?? "")),
                    "delete_account" => DeleteAccount?.Invoke(siteKey, Post.GetValueOrDefault("platform") ?? "") ?? Ok("deleted:" + (Post.GetValueOrDefault("platform") ?? "")),
                    "save_draft" => SaveDraft?.Invoke(siteKey, Post) ?? Ok("draft"),
                    "publish_draft" => PublishDraft?.Invoke(siteKey, ToInt(Post.GetValueOrDefault("draft_id"))) ?? Ok("pub:" + ToInt(Post.GetValueOrDefault("draft_id"))),
                    "publish_now" => PublishNow?.Invoke(siteKey, Post) ?? Ok("now"),
                    _ => null
                };
                if (action is "save_account" or "test_account" or "delete_account")
                {
                    accounts = ListAccounts?.Invoke(siteKey) ?? accounts;
                }
                else if (action is "save_draft" or "publish_draft" or "publish_now")
                {
                    drafts = ListDrafts?.Invoke(siteKey) ?? drafts;
                }
            }
        }

        var accountMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var acc in accounts)
        {
            accountMap[Str(acc.GetValueOrDefault("platform"))] = acc;
        }

        if (!embed)
        {
            OpenFrame(isSuper, brand);
            Echo(EpcSocialMediaExploreHintHtml());
        }
        else
        {
            Echo("<div class=\"epc-social-hub\">");
            Echo("<div class=\"epc-th-hero epc-social-hub__hero-tenant\" style=\"margin-bottom:16px\"><span class=\"epc-th-hero__badge\">Social media</span>");
            Echo("<h4><i class=\"fa fa-share-alt\"></i> Marketing hub — " + H(brand.GetValueOrDefault("brand_name")) + "</h4>");
            Echo("<p class=\"epc-th-hero__sub\">Captions, sample videos, connected accounts, and AI advisor. ");
            Echo("<a href=\"" + H(Url("", "platform")) + "\">Open full hub</a></p></div>");
            Echo(EpcSocialMediaExploreHintHtml());
        }

        if (flash is not null)
        {
            Echo("<div class=\"alert alert-" + (!PhpEmpty(flash.GetValueOrDefault("ok")) ? "success" : "danger") + "\">" + H(flash.GetValueOrDefault("message")) + "</div>");
        }

        if (isSuper && !embed)
        {
            Echo("<div class=\"alert alert-info\"><i class=\"fa fa-cloud\"></i> Platform scope: <strong>" + H(siteKey) + "</strong>. ");
            Echo("Tenant credentials are isolated per <code>site_key</code>. Switch: ");
            var tenants = ListTenants?.Invoke() ??
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme_parts" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "beta" }
            ];
            Echo("<a href=\"" + H(Url(tab, "platform")) + "\">platform</a>");
            foreach (var t in tenants.Take(8))
            {
                var sk = Str(t.GetValueOrDefault("site_key"));
                if (sk == "")
                {
                    continue;
                }

                Echo(" · <a href=\"" + H(Url(tab, sk)) + "\">" + H(sk) + "</a>");
            }

            Echo("</div>");
        }

        var tabs = new (string Key, string Label, string Icon)[]
        {
            ("pack", "Marketing pack", "fa-file-text-o"),
            ("tiktok", "TikTok", "fa-music"),
            ("instagram", "Instagram", "fa-instagram"),
            ("accounts", "Connected accounts", "fa-link"),
            ("ai", "AI advisor", "fa-magic"),
            ("drafts", "Drafts", "fa-pencil"),
            ("guide", "Guide", "fa-book")
        };
        Echo("<div class=\"epc-social-tabs\">");
        foreach (var (tk, label, icon) in tabs)
        {
            var url = embed
                ? TenantUrl("social") + "&amp;sub=" + Uri.EscapeDataString(tk)
                : Url(tk, isSuper && siteKey != "platform" ? siteKey : null);
            var active = tab == tk ? " btn-primary" : " btn-default";
            Echo("<a class=\"btn btn-sm" + active + "\" href=\"" + url + "\"><i class=\"fa " + H(icon) + "\"></i> " + H(label) + "</a>");
        }

        Echo("</div>");
        if (embed && Query.ContainsKey("sub"))
        {
            tab = SanitizeTab(Query.GetValueOrDefault("sub") ?? "");
        }

        var ready = CountPack("linkedin") + CountPack("instagram") + CountPack("facebook") + CountPack("x");
        Echo("<div class=\"epc-social-kpi\">");
        Echo("<div class=\"epc-social-kpi__item\"><div class=\"epc-social-kpi__val\">" + ready + "</div><div class=\"epc-social-kpi__label\">Ready posts</div></div>");
        Echo("<div class=\"epc-social-kpi__item\"><div class=\"epc-social-kpi__val\">" + accounts.Count + "</div><div class=\"epc-social-kpi__label\">Connected</div></div>");
        Echo("<div class=\"epc-social-kpi__item\"><div class=\"epc-social-kpi__val\">" + drafts.Count + "</div><div class=\"epc-social-kpi__label\">Drafts</div></div>");
        Echo("<div class=\"epc-social-kpi__item\"><div class=\"epc-social-kpi__val\">" + H(brand.GetValueOrDefault("industry")) + "</div><div class=\"epc-social-kpi__label\">Industry</div></div>");
        Echo("</div>");
        if (tab == "pack")
        {
            EpcSocialRenderPackTab(brand);
        }
        else if (tab == "tiktok")
        {
            EpcSocialRenderTiktokTab(brand);
        }
        else if (tab == "instagram")
        {
            EpcSocialRenderInstagramTab(brand);
        }
        else if (tab == "accounts")
        {
            EpcSocialRenderAccountsTab(brand, siteKey, accountMap, integrationsUrl, csrf);
        }
        else if (tab == "ai")
        {
            EpcSocialRenderAiTab(brand, trends, hooks, csrf);
        }
        else if (tab == "drafts")
        {
            EpcSocialRenderDraftsTab(brand, drafts, csrf);
        }
        else
        {
            EpcSocialRenderGuideTab(brand, integrationsUrl, guideUrl);
        }

        if (!embed)
        {
            if (FrameClose is not null)
            {
                FrameClose();
            }
            else
            {
                Echo("FRAME_CLOSE");
            }
        }
        else
        {
            Echo("</div>");
        }
    }

    public static void EpcSocialRenderPackTab(Dictionary<string, object?> brand)
    {
        foreach (var plat in new[] { "linkedin", "instagram", "facebook", "x" })
        {
            var meta = PackMeta()[plat];
            var posts = BrandPosts(plat, brand);
            Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong><i class=\"fa fa-share-alt\"></i> " + H(meta["label"]) + "</strong></div>");
            Echo("<div class=\"panel-body\"><p class=\"text-muted\">" + H(meta["intro"]) + "</p>");
            Echo("<div class=\"epc-social-grid\">");
            foreach (var post in posts)
            {
                var caption = Str(post.GetValueOrDefault("caption"));
                Echo("<div class=\"epc-social-post\">");
                Echo("<div class=\"epc-social-post__head\">" + H(post.GetValueOrDefault("title")) + "</div>");
                Echo("<div class=\"epc-social-post__body\">" + H(caption) + "</div>");
                Echo("<div class=\"epc-social-post__bar\"><span class=\"text-muted small\">" + H(meta["label"]) + "</span>");
                Echo("<button type=\"button\" class=\"btn btn-xs btn-primary epc-social-copy\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-copy\"></i> Copy</button> ");
                if (plat is "instagram" or "facebook")
                {
                    Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-save-draft\" data-platform=\"" + H(plat) + "\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-save\"></i> Draft</button> ");
                    Echo("<button type=\"button\" class=\"btn btn-xs btn-success epc-social-publish-now\" data-platform=\"" + H(plat) + "\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-share\"></i> Publish</button>");
                }

                Echo("</div>");
                Echo("</div>");
            }

            Echo("</div>");
            if (meta.GetValueOrDefault("hashtags") is IEnumerable<object?> tags)
            {
                var list = tags.Select(t => Str(t)).Where(t => t != "").ToList();
                if (list.Count > 0)
                {
                    Echo("<div style=\"margin-top:14px\"><strong class=\"small\">Hashtag bank</strong><br>");
                    foreach (var tag in list)
                    {
                        var adapted = Adapt(tag, brand);
                        Echo("<span class=\"epc-social-tag epc-social-copy\" data-caption=\"" + H(adapted) + "\">" + H(adapted) + "</span>");
                    }

                    Echo("</div>");
                }
            }

            Echo("</div></div>");
        }

        var thread = Adapt(XThreadStarter?.Invoke() ?? "Thread for {brand}\n1/ Why stock", brand);
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>X thread starter</strong></div><div class=\"panel-body\">");
        Echo("<pre style=\"white-space:pre-line;font-size:12px;background:#f8fafc;padding:12px;border-radius:8px\">" + H(thread) + "</pre>");
        Echo("<button type=\"button\" class=\"btn btn-sm btn-primary epc-social-copy\" data-caption=\"" + H(thread) + "\"><i class=\"fa fa-copy\"></i> Copy thread</button>");
        Echo("</div></div>");
    }

    public static void EpcSocialRenderTiktokTab(Dictionary<string, object?> brand)
    {
        var meta = PackMeta()["tiktok"];
        var posts = BrandPosts("tiktok", brand);
        var specs = TiktokSpecs?.Invoke() ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["Ratio"] = "9:16", ["Length"] = "15-30s" };
        var reels = Videos().Where(v => Str(v.GetValueOrDefault("kind")) == "reel").ToList();
        Echo("<div class=\"epc-social-intro\"><strong>" + H(meta["label"]) + "</strong> — " + H(meta["intro"]) + "</div>");
        Echo("<div class=\"row\"><div class=\"col-md-4\"><div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Video specs</strong></div><div class=\"panel-body\"><dl class=\"epc-social-specs\">");
        foreach (var (k, v) in specs)
        {
            Echo("<dt>" + H(k) + "</dt><dd>" + H(v) + "</dd>");
        }

        Echo("</dl></div></div>");
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Upload / link</strong></div><div class=\"panel-body\">");
        Echo("<p class=\"text-muted small\">Public HTTPS <code>.mp4</code> URL required for live Publish (TikTok PULL_FROM_URL — verify domain in TikTok Developer Portal). Use a sample reel below or paste your CDN URL. Connect token under <em>Connected accounts</em>, then Publish.</p>");
        Echo("<input type=\"url\" class=\"form-control input-sm\" id=\"epc_social_video_url\" placeholder=\"https://…/your-reel.mp4\">");
        Echo("</div></div></div>");
        Echo("<div class=\"col-md-8\">");
        if (reels.Count > 0)
        {
            Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong><i class=\"fa fa-film\"></i> Sample reels (built-in)</strong></div><div class=\"panel-body\"><div class=\"epc-social-video-grid epc-social-video-grid--reels\">");
            foreach (var video in reels)
            {
                EpcSocialRenderVideoCard(video, true);
            }

            Echo("</div></div></div>");
        }

        Echo("<div class=\"epc-social-grid\">");
        foreach (var post in posts)
        {
            var caption = Str(post.GetValueOrDefault("caption"));
            Echo("<div class=\"epc-social-post\"><div class=\"epc-social-post__head\">" + H(post.GetValueOrDefault("title")) + "</div>");
            Echo("<div class=\"epc-social-post__body\">" + H(caption) + "</div>");
            Echo("<div class=\"epc-social-post__bar\"><button type=\"button\" class=\"btn btn-xs btn-primary epc-social-copy\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-copy\"></i> Copy caption</button>");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-save-draft\" data-platform=\"tiktok\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-save\"></i> Save draft</button> ");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-success epc-social-publish-now\" data-platform=\"tiktok\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-share\"></i> Publish</button></div></div>");
        }

        Echo("</div></div></div>");
    }

    public static void EpcSocialRenderInstagramTab(Dictionary<string, object?> brand)
    {
        var posts = BrandPosts("instagram", brand);
        Echo("<div class=\"epc-social-intro\"><strong>Instagram</strong> — Posts, Reels &amp; carousels for " + H(brand.GetValueOrDefault("brand_name")) + ".</div>");
        Echo("<div class=\"epc-social-grid\">");
        foreach (var post in posts)
        {
            var caption = Str(post.GetValueOrDefault("caption"));
            Echo("<div class=\"epc-social-post\"><div class=\"epc-social-post__head\">" + H(post.GetValueOrDefault("title")) + "</div>");
            Echo("<div class=\"epc-social-post__body\">" + H(caption) + "</div>");
            Echo("<div class=\"epc-social-post__bar\"><button type=\"button\" class=\"btn btn-xs btn-primary epc-social-copy\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-copy\"></i> Copy</button> ");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-save-draft\" data-platform=\"instagram\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-save\"></i> Save draft</button> ");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-success epc-social-publish-now\" data-platform=\"instagram\" data-title=\"" + H(post.GetValueOrDefault("title")) + "\" data-caption=\"" + H(caption) + "\"><i class=\"fa fa-share\"></i> Publish</button></div></div>");
        }

        Echo("</div>");
        Echo("<div class=\"panel panel-default\" style=\"margin-top:18px\"><div class=\"panel-heading\"><strong>Reels &amp; carousel ideas</strong></div><div class=\"panel-body\"><div class=\"epc-social-grid\">");
        foreach (var idea in InstagramReelsIdeas?.Invoke() ?? [new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Unbox", ["caption"] = "New {brand} drop" }])
        {
            var cap = Adapt(Str(idea.GetValueOrDefault("caption")), brand);
            Echo("<div class=\"epc-social-post\"><div class=\"epc-social-post__head\">" + H(idea.GetValueOrDefault("title")) + "</div>");
            Echo("<div class=\"epc-social-post__body\">" + H(cap) + "</div>");
            Echo("<div class=\"epc-social-post__bar\"><button type=\"button\" class=\"btn btn-xs btn-primary epc-social-copy\" data-caption=\"" + H(cap) + "\"><i class=\"fa fa-copy\"></i> Copy</button> ");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-save-draft\" data-platform=\"instagram\" data-title=\"" + H(idea.GetValueOrDefault("title")) + "\" data-caption=\"" + H(cap) + "\"><i class=\"fa fa-save\"></i> Save draft</button></div></div>");
        }

        Echo("</div></div></div>");
    }

    public static void EpcSocialRenderAccountsTab(Dictionary<string, object?> brand, string siteKey, Dictionary<string, Dictionary<string, object?>> accountMap, string integrationsUrl, string csrf)
    {
        Echo("<div class=\"alert alert-info\"><i class=\"fa fa-bullhorn\"></i> <strong>Live publish</strong> works for <strong>Facebook, Instagram, and TikTok</strong> once a valid token is saved and <em>Test connection</em> passes. "
             + "LinkedIn and X store credentials in the vault for ops handoff — post natively until those APIs are enabled. "
             + "Publish from <strong>Drafts</strong> with a public HTTPS media URL (sample reels are under the TikTok tab).</div>");
        Echo("<div class=\"alert alert-warning\"><i class=\"fa fa-lock\"></i> <strong>Secure vault</strong> — tokens are AES-256 encrypted per tenant and never shown after save. "
             + "Meta: Page token + Page ID (Facebook) or IG Business user ID. TikTok: user token with <code>video.publish</code>; unaudited apps stay <code>SELF_ONLY</code>. "
             + "Register apps in <a href=\"" + H(integrationsUrl) + "\">Integrations hub</a>, then return here to paste tokens and test.</div>");
        foreach (var (key, plat) in Plat())
        {
            accountMap.TryGetValue(key, out var existing);
            var status = existing is not null ? Str(existing.GetValueOrDefault("status"), "pending") : "not_connected";
            var meta = SocialPdo?.Invoke() is not null && existing is not null
                ? AccountPublicMeta?.Invoke(siteKey, key) ?? Meta()
                : new Dictionary<string, object?>(StringComparer.Ordinal) { ["page_id"] = "", ["ig_user_id"] = "", ["open_id"] = "", ["privacy_level"] = "SELF_ONLY", ["has_token"] = false };
            var idLabel = "Page / Business ID";
            var idHint = "";
            if (key == "instagram")
            {
                idLabel = "Instagram Business user ID";
                idHint = "Graph: Page → instagram_business_account.id";
            }
            else if (key == "facebook")
            {
                idLabel = "Facebook Page ID";
            }
            else if (key == "tiktok")
            {
                idLabel = "Open ID (optional)";
                idHint = "Filled by TikTok OAuth when available";
            }
            else if (key is "linkedin" or "x")
            {
                idHint = "Vault only — live publish not enabled yet";
            }

            Echo("<div class=\"epc-social-account-card\">");
            Echo("<div class=\"epc-social-account-card__head\">");
            Echo("<span class=\"epc-social-platform-icon\" style=\"background:" + H(plat["color"]) + "\"><i class=\"fa " + H(plat["icon"]) + "\"></i></span>");
            Echo("<div><strong>" + H(plat["label"]) + "</strong><br><span class=\"label label-" + (status == "verified" ? "success" : status == "error" ? "danger" : "default") + "\">" + H(status) + "</span>");
            if (!PhpEmpty(meta.GetValueOrDefault("has_token")))
            {
                Echo(" <span class=\"label label-info\">token saved</span>");
            }

            Echo("</div>");
            if (existing is not null && !PhpEmpty(existing.GetValueOrDefault("last_test_at")))
            {
                Echo("<span class=\"text-muted small pull-right\">Last test: " + H(Fmt(ToLong(existing.GetValueOrDefault("last_test_at")))) + "</span>");
            }

            Echo("</div>");
            Echo("<form method=\"post\" class=\"form-horizontal\">");
            Echo("<input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
            Echo("<input type=\"hidden\" name=\"epc_social_action\" value=\"save_account\">");
            Echo("<input type=\"hidden\" name=\"platform\" value=\"" + H(key) + "\">");
            Echo("<div class=\"row\"><div class=\"col-md-3 form-group\"><label class=\"control-label\">Account label</label>");
            Echo("<input class=\"form-control input-sm\" name=\"account_label\" value=\"" + H(existing?.GetValueOrDefault("account_label") ?? brand.GetValueOrDefault("brand_name")) + "\"></div>");
            Echo("<div class=\"col-md-3 form-group\"><label class=\"control-label\">Username / page</label>");
            Echo("<input class=\"form-control input-sm\" name=\"username\" value=\"" + H(existing?.GetValueOrDefault("username") ?? brand.GetValueOrDefault("handle")) + "\" autocomplete=\"off\"></div>");
            Echo("<div class=\"col-md-3 form-group\"><label class=\"control-label\">Access token</label>");
            Echo("<input class=\"form-control input-sm\" name=\"access_token\" type=\"password\" placeholder=\"" + (existing is not null ? "•••••••• (unchanged if empty)" : "Paste token") + "\" autocomplete=\"new-password\"></div>");
            Echo("<div class=\"col-md-3 form-group\"><label class=\"control-label\">API key / App ID</label>");
            Echo("<input class=\"form-control input-sm\" name=\"api_key\" type=\"password\" placeholder=\"Optional\" autocomplete=\"new-password\"></div></div>");
            Echo("<div class=\"row\"><div class=\"col-md-3 form-group\"><label class=\"control-label\">API secret</label>");
            Echo("<input class=\"form-control input-sm\" name=\"api_secret\" type=\"password\" placeholder=\"Optional\" autocomplete=\"new-password\"></div>");
            Echo("<div class=\"col-md-3 form-group\"><label class=\"control-label\">" + H(idLabel) + "</label>");
            var pageVal = key == "instagram" && Str(meta.GetValueOrDefault("ig_user_id")) != ""
                ? Str(meta.GetValueOrDefault("ig_user_id"))
                : Str(meta.GetValueOrDefault("page_id"));
            if (key == "tiktok" && pageVal == "" && Str(meta.GetValueOrDefault("open_id")) != "")
            {
                pageVal = Str(meta.GetValueOrDefault("open_id"));
            }

            Echo("<input class=\"form-control input-sm\" name=\"page_id\" value=\"" + H(pageVal) + "\" autocomplete=\"off\" placeholder=\"" + H(idHint) + "\"></div>");
            if (key == "instagram")
            {
                var ig = Str(meta.GetValueOrDefault("ig_user_id"));
                Echo("<input type=\"hidden\" name=\"ig_user_id\" value=\"" + H(ig != "" ? ig : pageVal) + "\">");
            }

            if (key == "tiktok")
            {
                Echo("<div class=\"col-md-3 form-group\"><label class=\"control-label\">TikTok privacy</label>");
                var priv = Str(meta.GetValueOrDefault("privacy_level"), "SELF_ONLY");
                Echo("<select class=\"form-control input-sm\" name=\"privacy_level\">");
                foreach (var (pv, plab) in new[]
                         {
                             ("SELF_ONLY", "Private (SELF_ONLY — unaudited apps)"),
                             ("FOLLOWER_OF_CREATOR", "Followers"),
                             ("MUTUAL_FOLLOW_FRIENDS", "Friends"),
                             ("PUBLIC_TO_EVERYONE", "Public (needs TikTok audit)")
                         })
                {
                    Echo("<option value=\"" + H(pv) + "\"" + (priv == pv ? " selected" : "") + ">" + H(plab) + "</option>");
                }

                Echo("</select></div>");
            }

            Echo("<div class=\"col-md-3 form-group\" style=\"padding-top:24px\">");
            Echo("<button type=\"submit\" class=\"btn btn-sm btn-primary\"><i class=\"fa fa-save\"></i> Save securely</button>");
            Echo("</div></div></form>");
            Echo("<form method=\"post\" style=\"display:inline\"><input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
            Echo("<input type=\"hidden\" name=\"epc_social_action\" value=\"test_account\"><input type=\"hidden\" name=\"platform\" value=\"" + H(key) + "\">");
            Echo("<button type=\"submit\" class=\"btn btn-sm btn-default\"><i class=\"fa fa-plug\"></i> Test connection (live API)</button></form> ");
            if (existing is not null)
            {
                Echo("<form method=\"post\" style=\"display:inline\" onsubmit=\"return confirm('Remove credentials?')\"><input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
                Echo("<input type=\"hidden\" name=\"epc_social_action\" value=\"delete_account\"><input type=\"hidden\" name=\"platform\" value=\"" + H(key) + "\">");
                Echo("<button type=\"submit\" class=\"btn btn-sm btn-danger\"><i class=\"fa fa-trash\"></i></button></form>");
            }

            Echo("</div></div>");
        }
    }

    public static void EpcSocialRenderAiTab(Dictionary<string, object?> brand, List<Dictionary<string, object?>> trends, List<string> hooks, string csrf)
    {
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong><i class=\"fa fa-line-chart\"></i> Trending formats this week</strong></div><div class=\"panel-body\">");
        foreach (var t in trends)
        {
            Echo("<div class=\"epc-social-ai-card\"><strong>" + H(t.GetValueOrDefault("name")) + "</strong> <span class=\"text-muted\">(" + H(t.GetValueOrDefault("platforms")) + ")</span><br>" + H(t.GetValueOrDefault("tip")) + "</div>");
        }

        Echo("</div></div>");
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Industry hooks — " + H(brand.GetValueOrDefault("industry")) + " (" + H(brand.GetValueOrDefault("country")) + ")</strong></div><div class=\"panel-body\"><ul>");
        foreach (var hook in hooks)
        {
            Echo("<li>" + H(hook) + "</li>");
        }

        Echo("</ul></div></div>");
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Generate caption</strong></div><div class=\"panel-body\">");
        Echo("<div class=\"row\"><div class=\"col-md-3\"><select class=\"form-control\" id=\"epc_social_gen_platform\"><option value=\"instagram\">Instagram</option><option value=\"tiktok\">TikTok</option><option value=\"linkedin\">LinkedIn</option><option value=\"facebook\">Facebook</option><option value=\"x\">X</option></select></div>");
        Echo("<div class=\"col-md-5\"><input class=\"form-control\" id=\"epc_social_gen_product\" placeholder=\"Product line or promo (optional)\"></div>");
        Echo("<div class=\"col-md-4\"><button type=\"button\" class=\"btn btn-primary\" id=\"epc_social_gen_btn\"><i class=\"fa fa-magic\"></i> Generate</button></div></div>");
        Echo("<div id=\"epc_social_gen_result\" style=\"margin-top:14px;display:none\"><pre class=\"epc-social-post__body\" style=\"max-height:none;background:#f8fafc;padding:12px;border-radius:8px\" id=\"epc_social_gen_caption\"></pre>");
        Echo("<p id=\"epc_social_gen_tags\" class=\"text-muted\"></p>");
        Echo("<button type=\"button\" class=\"btn btn-sm btn-primary epc-social-copy\" id=\"epc_social_gen_copy\"><i class=\"fa fa-copy\"></i> Copy</button></div>");
        Echo("<p class=\"text-muted small\" style=\"margin-top:10px\">AI advisor uses industry + country rules for fast captions. Sample reels live under the TikTok tab; connect Meta/TikTok apps via Integrations for live publish.</p>");
        Echo("</div></div>");
    }

    public static void EpcSocialRenderDraftsTab(Dictionary<string, object?> brand, List<Dictionary<string, object?>> drafts, string csrf)
    {
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Compose &amp; publish</strong></div><div class=\"panel-body\">");
        Echo("<p class=\"text-muted\">Facebook / Instagram / TikTok post live from CP. Media URL must be public HTTPS (Meta/TikTok fetch it). Tip: open TikTok tab → <em>Use as media URL</em> on a sample reel, then return here.</p>");
        Echo("<form method=\"post\" class=\"form-horizontal\" id=\"epc_social_publish_form\">");
        Echo("<input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
        Echo("<div class=\"row\">");
        Echo("<div class=\"col-md-3 form-group\"><label>Platform</label><select class=\"form-control input-sm\" name=\"platform\" required>");
        foreach (var (pk, pl) in new[] { ("instagram", "Instagram"), ("facebook", "Facebook"), ("tiktok", "TikTok") })
        {
            Echo("<option value=\"" + H(pk) + "\">" + H(pl) + "</option>");
        }

        Echo("</select></div>");
        Echo("<div class=\"col-md-5 form-group\"><label>Title</label><input class=\"form-control input-sm\" name=\"title\" value=\"" + H(Str(brand.GetValueOrDefault("brand_name")) + " post") + "\"></div>");
        Echo("<div class=\"col-md-4 form-group\"><label>Public media URL</label><input class=\"form-control input-sm\" name=\"media_url\" id=\"epc_social_compose_media\" placeholder=\"https://…/image.jpg or video.mp4\"></div>");
        Echo("</div>");
        Echo("<div class=\"form-group\"><label>Caption</label><textarea class=\"form-control\" name=\"caption\" id=\"epc_social_compose_caption\" rows=\"4\" placeholder=\"Post caption…\"></textarea></div>");
        Echo("<div class=\"form-group\"><label>Hashtags</label><input class=\"form-control input-sm\" name=\"hashtags\" placeholder=\"#AutoParts #UAEBusiness\"></div>");
        Echo("<button type=\"submit\" class=\"btn btn-primary\" name=\"epc_social_action\" value=\"publish_now\" onclick=\"return confirm('Publish this post to the selected platform now?')\"><i class=\"fa fa-share\"></i> Publish now</button> ");
        Echo("<button type=\"submit\" class=\"btn btn-default\" name=\"epc_social_action\" value=\"save_draft\"><i class=\"fa fa-save\"></i> Save as draft only</button>");
        Echo("</form></div></div>");
        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>Saved drafts</strong></div><div class=\"panel-body\">");
        if (drafts.Count == 0)
        {
            Echo("<p class=\"text-muted\">No drafts yet. Save from Marketing pack / TikTok, or use Compose above.</p>");
        }

        Echo("<table class=\"table table-striped\"><thead><tr><th>Title</th><th>Platform</th><th>Status</th><th>Updated</th><th></th></tr></thead><tbody>");
        foreach (var d in drafts)
        {
            var status = Str(d.GetValueOrDefault("status"), "draft");
            var statusClass = status == "published" ? "success" : status == "error" ? "danger" : "default";
            var plat = Str(d.GetValueOrDefault("platform"));
            var canPublish = plat is "facebook" or "instagram" or "tiktok" && status != "published";
            Echo("<tr><td>" + H(d.GetValueOrDefault("title")));
            if (!PhpEmpty(d.GetValueOrDefault("media_url")))
            {
                Echo("<br><small class=\"text-muted\">" + H(MbSub(Str(d.GetValueOrDefault("media_url")), 60)) + "</small>");
            }

            if (status == "error" && !PhpEmpty(d.GetValueOrDefault("last_error")))
            {
                Echo("<br><small class=\"text-danger\">" + H(d.GetValueOrDefault("last_error")) + "</small>");
            }

            if (status == "published" && !PhpEmpty(d.GetValueOrDefault("external_post_id")))
            {
                Echo("<br><small class=\"text-success\">ID: " + H(d.GetValueOrDefault("external_post_id")) + "</small>");
            }

            Echo("</td><td>" + H(plat) + "</td>");
            Echo("<td><span class=\"label label-" + statusClass + "\">" + H(status) + "</span></td>");
            Echo("<td>" + H(Fmt(ToLong(d.GetValueOrDefault("updated_at")))) + "</td>");
            Echo("<td style=\"white-space:nowrap\">");
            Echo("<button type=\"button\" class=\"btn btn-xs btn-default epc-social-copy\" data-caption=\"" + H(d.GetValueOrDefault("caption")) + "\"><i class=\"fa fa-copy\"></i></button> ");
            if (canPublish)
            {
                Echo("<form method=\"post\" style=\"display:inline\" onsubmit=\"return confirm('Publish this draft to " + H(plat) + "?')\">");
                Echo("<input type=\"hidden\" name=\"csrf_token\" value=\"" + H(csrf) + "\">");
                Echo("<input type=\"hidden\" name=\"epc_social_action\" value=\"publish_draft\">");
                Echo("<input type=\"hidden\" name=\"draft_id\" value=\"" + ToInt(d.GetValueOrDefault("id")) + "\">");
                Echo("<button type=\"submit\" class=\"btn btn-xs btn-primary\"><i class=\"fa fa-share\"></i> Publish</button></form>");
            }

            Echo("</td></tr>");
        }

        Echo("</tbody></table></div></div>");
    }

    public static void EpcSocialRenderGuideTab(Dictionary<string, object?> brand, string integrationsUrl, string guideUrl)
    {
        var steps = new (string Title, string Body)[]
        {
            ("Connect accounts",
                "Open <strong>Connected accounts</strong>. Paste access token + Page / IG Business user ID (encrypted vault). Click <em>Test connection (live API)</em> — Facebook/Instagram hit Meta Graph; TikTok hits creator_info. Register apps in <a href=\"" + H(integrationsUrl) + "\">Integrations hub</a> first."),
            ("Pick content from the pack",
                "Marketing pack ships 16 modern captions (LinkedIn, Instagram, Facebook, X) plus TikTok reel scripts. Tenant CP auto-adapts brand name, domain, and hashtags for <strong>" + H(brand.GetValueOrDefault("brand_name")) + "</strong>."),
            ("Publish from Drafts (with video)",
                "Open <strong>Drafts</strong> → Compose. Choose Facebook / Instagram / TikTok, paste a <em>public HTTPS</em> image or .mp4 URL (or click <em>Use as media URL</em> on a sample reel), then <strong>Publish now</strong>. Instagram: media → poll → media_publish. TikTok: PULL_FROM_URL (verify domain). Unaudited TikTok apps: SELF_ONLY only."),
            ("AI advisor",
                "Check weekly formats and industry hooks, then generate a caption for your product line. GCC: mix English + Arabic hashtags. Pakistan: Urdu/English on Facebook; English on LinkedIn."),
            ("Measure & handoff",
                "Published drafts store external post IDs. Track clicks via Web tracker / GA4. LinkedIn and X remain copy→native until those APIs are wired — vault still keeps tokens safe for your team.")
        };
        Echo("<div class=\"alert alert-info\"><i class=\"fa fa-book\"></i> Guide URL: <a href=\"" + H(guideUrl) + "\"><code>" + H(guideUrl) + "</code></a></div>");
        var guides = Videos().Where(v => Str(v.GetValueOrDefault("kind")) == "guide").ToList();
        if (guides.Count > 0)
        {
            Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong><i class=\"fa fa-play-circle\"></i> Watch the guide (built-in videos)</strong></div><div class=\"panel-body\"><div class=\"epc-social-video-grid\">");
            foreach (var video in guides)
            {
                EpcSocialRenderVideoCard(video);
            }

            Echo("</div></div></div>");
        }

        for (var i = 0; i < steps.Length; i++)
        {
            Echo("<div class=\"epc-social-guide-step\"><h5 style=\"margin:0 0 6px\">Step " + (i + 1) + " — " + H(steps[i].Title) + "</h5><div>" + steps[i].Body + "</div></div>");
        }

        Echo("<div class=\"panel panel-default\"><div class=\"panel-heading\"><strong>GCC &amp; Pakistan best practices</strong></div><div class=\"panel-body\"><ul>");
        Echo("<li><strong>UAE/GCC:</strong> Post Sun–Thu 10am–1pm GST; compliance + trade-pricing posts build B2B trust.</li>");
        Echo("<li><strong>Pakistan:</strong> Facebook + WhatsApp Status repurposing; Urdu captions for retail; English for B2B LinkedIn.</li>");
        Echo("<li><strong>All markets:</strong> Never post raw credentials; use the CP vault only. Enable 2FA on every social account.</li>");
        Echo("<li><strong>Video:</strong> Start from sample reels under TikTok, replace with your screen recording, host on public HTTPS, then Publish.</li>");
        Echo("</ul></div></div>");
    }

    public static string Capture(Action render)
    {
        LastOutput = "";
        render();
        return LastOutput;
    }

    private static void OpenFrame(bool isSuper, Dictionary<string, object?> brand)
    {
        if (FrameOpen is not null)
        {
            FrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["class"] = "epc-social-hub",
                ["hero"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["badge"] = isSuper ? "Super CP · Social Marketing" : "Social media hub",
                    ["title"] = Str(brand.GetValueOrDefault("brand_name")) + " — Social Media Marketing"
                }
            });
            return;
        }

        Echo("FRAME_OPEN:epc-social-hub:" + (isSuper ? "Super CP · Social Marketing" : "Social media hub") + ":" + Str(brand.GetValueOrDefault("brand_name")) + " — Social Media Marketing");
    }

    private static Dictionary<string, object?> BrandOf(string siteKey)
        => BrandContext?.Invoke(siteKey) ?? new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["brand_name"] = "Acme Parts",
            ["industry"] = "auto_parts",
            ["market"] = "O'Reilly UAE",
            ["country"] = "AE",
            ["handle"] = "@acme"
        };

    private static string Url(string tab, string? siteKey)
    {
        if (HubUrl is not null)
        {
            return HubUrl(tab, siteKey);
        }

        var backend = Backend?.Invoke() ?? "cp";
        var baseUrl = "/" + backend + "/control/portal/epc_social_media_hub";
        var q = new List<string>();
        if (tab != "" && tab != "pack")
        {
            q.Add("tab=" + WebUtility.UrlEncode(tab));
        }

        if (siteKey is not null && siteKey != "" && siteKey != "platform")
        {
            q.Add("site_key=" + WebUtility.UrlEncode(siteKey));
        }

        return q.Count == 0 ? baseUrl : baseUrl + "?" + string.Join("&", q);
    }

    private static string TenantUrl(string tab)
        => TenantHubUrl?.Invoke(tab) ?? "/" + (Backend?.Invoke() ?? "cp") + "/shop/tenant_hub/tenant_hub?tab=" + Uri.EscapeDataString(tab);

    private static Dictionary<string, Dictionary<string, object?>> PackMeta()
        => PackPlatforms?.Invoke() ?? new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["linkedin"] = MetaPlat("LinkedIn", "B2B", ["#Auto {brand}"]),
            ["instagram"] = MetaPlat("Instagram", "Visual", []),
            ["facebook"] = MetaPlat("Facebook", "Reach", []),
            ["x"] = MetaPlat("X", "Short", []),
            ["tiktok"] = MetaPlat("TikTok", "Reels", [])
        };

    private static Dictionary<string, object?> MetaPlat(string label, string intro, string[] tags)
        => new(StringComparer.Ordinal) { ["label"] = label, ["intro"] = intro, ["hashtags"] = tags.Cast<object?>().ToList() };

    private static List<Dictionary<string, object?>> BrandPosts(string plat, Dictionary<string, object?> brand)
        => PackPostsForBrand?.Invoke(plat, brand) ?? [];

    private static int CountPack(string plat)
        => (PackPosts?.Invoke(plat) ?? []).Count;

    private static List<Dictionary<string, object?>> Videos()
        => VideoLibrary?.Invoke() ??
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "O'Reilly reel", ["url"] = "https://cdn.test/a.mp4", ["kind"] = "reel", ["blurb"] = "Hook" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Guide clip", ["url"] = "https://cdn.test/g.mp4", ["kind"] = "guide", ["blurb"] = "" }
        ];

    private static IEnumerable<KeyValuePair<string, Dictionary<string, object?>>> Plat()
        => (Platforms?.Invoke() ?? new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["instagram"] = new(StringComparer.Ordinal) { ["label"] = "Instagram", ["icon"] = "fa-instagram", ["color"] = "#e1306c" },
            ["facebook"] = new(StringComparer.Ordinal) { ["label"] = "Facebook", ["icon"] = "fa-facebook", ["color"] = "#1877f2" }
        }).ToList();

    private static Dictionary<string, object?> Meta()
        => new(StringComparer.Ordinal) { ["page_id"] = "p1", ["ig_user_id"] = "ig1", ["open_id"] = "", ["privacy_level"] = "SELF_ONLY", ["has_token"] = true };

    private static Dictionary<string, object?> Ok(string message)
        => new(StringComparer.Ordinal) { ["ok"] = true, ["message"] = message };

    private static Dictionary<string, object?> Trend(string name, string platforms, string tip)
        => new(StringComparer.Ordinal) { ["name"] = name, ["platforms"] = platforms, ["tip"] = tip };

    private static string Adapt(string text, Dictionary<string, object?> brand)
        => AdaptText?.Invoke(text, brand) ?? text.Replace("{brand}", Str(brand.GetValueOrDefault("brand_name")), StringComparison.Ordinal);

    private static string SanitizeTab(string raw)
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

    private static string MbSub(string text, int chars)
    {
        var n = 0;
        var sb = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (n >= chars)
            {
                break;
            }

            sb.Append(rune);
            n++;
        }

        return sb.ToString();
    }

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
        if (value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
