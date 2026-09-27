using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Migration;

/// <summary>PHP <c>epc_social_platforms()</c> entry (content/social_media/epc_social_media_helpers.php).</summary>
public sealed record SocialPlatform(string Key, string Label, string Icon, string Color);

/// <summary>PHP <c>epc_social_pack_platforms()</c> entry (content/social_media/epc_social_media_pack_data.php).</summary>
public sealed record SocialPackPlatform(string Key, string Label, string Intro, IReadOnlyList<string> Hashtags);

/// <summary>PHP <c>epc_social_pack_posts()</c> entry.</summary>
public sealed record SocialPackPost(string Title, string Caption);

/// <summary>PHP <c>epc_social_video_library()</c> entry.</summary>
public sealed record SocialVideo(string Id, string Title, string Kind, string Url, string Poster, string Aspect, string Blurb);

/// <summary>PHP <c>epc_social_trending_formats()</c> entry.</summary>
public sealed record SocialTrendFormat(string Name, string Platforms, string Tip);

/// <summary>PHP <c>epc_social_render_guide_tab()</c> step.</summary>
public sealed record SocialGuideStep(string Title, string BodyHtml);

/// <summary>PHP <c>epc_social_generate_caption()</c> result.</summary>
public sealed record SocialGeneratedCaption(string Platform, string Caption, string Hashtags);

/// <summary>PHP <c>epc_social_brand_context()</c> result.</summary>
public sealed record SocialBrandContext(
    string SiteKey,
    string BrandName,
    string Handle,
    string Website,
    string Domain,
    string Industry,
    string Country,
    string Market,
    bool IsPlatform);

/// <summary>
/// Typed twin of the PHP social marketing pack (content/social_media/epc_social_media_pack_data.php and the
/// pack/trend/hook/caption helpers in content/social_media/epc_social_media_helpers.php).
/// </summary>
public static class SocialMediaPackCatalog
{
    private static readonly Regex HandleSafe = new("[^a-z0-9._]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TagSafe = new("[^A-Z0-9]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>PHP <c>epc_social_platforms()</c> — vault order.</summary>
    public static IReadOnlyList<SocialPlatform> Platforms { get; } =
    [
        new("instagram", "Instagram", "fa-instagram", "#e1306c"),
        new("tiktok", "TikTok", "fa-music", "#010101"),
        new("facebook", "Facebook", "fa-facebook", "#1877f2"),
        new("linkedin", "LinkedIn", "fa-linkedin", "#0a66c2"),
        new("x", "X / Twitter", "fa-twitter", "#1d9bf0"),
    ];

    /// <summary>PHP <c>epc_social_pack_platforms()</c>.</summary>
    public static IReadOnlyList<SocialPackPlatform> PackPlatforms { get; } =
    [
        new(
            "linkedin",
            "LinkedIn",
            "Decision-maker feed. Lead with outcomes — stock accuracy, faster quotes, UAE compliance — then proof and a clear CTA.",
            ["#ECOMAE", "#UAEBusiness", "#Dubai", "#ERP", "#Ecommerce", "#DigitalTransformation", "#UAECompliance", "#UAEVAT", "#Einvoicing", "#FTA", "#SaaS", "#B2B", "#AutoParts", "#DubaiTech", "#CloudERP", "#SME", "#Wholesale", "#GCC", "#SpareParts", "#TradeTech"]),
        new(
            "instagram",
            "Instagram",
            "Hook in line one. Pair with bold visuals or Reels. Carousels for feature lists; Reels for 10–20s demos.",
            ["#ECOMAE", "#DubaiTech", "#UAEStartup", "#Ecommerce", "#DubaiBusiness", "#UAEBusiness", "#MadeForUAE", "#ERP", "#CloudPlatform", "#B2BUAE", "#UAERetail", "#AutoPartsUAE", "#SpareParts", "#OnlineBusiness", "#AI", "#GCCBusiness", "#WholesaleUAE", "#UAEVat", "#VINSearch", "#PartsLookup"]),
        new(
            "facebook",
            "Facebook",
            "Conversational, slightly longer. Strong for UAE trading communities. Ask a question + CTA; use link previews.",
            ["#UAEBusiness", "#DubaiSME", "#UAEEntrepreneur", "#DubaiTrading", "#B2BUAE", "#AutoPartsUAE", "#UAERetail", "#EcommerceUAE", "#ECOMAE", "#WholesaleUAE", "#GCCBusiness", "#UAECompliance", "#SparePartsUAE"]),
        new(
            "x",
            "X / Twitter",
            "Punchy and direct (≤280 chars). Bold claims + proof. Threads for deeper feature walks.",
            ["#UAECompliance", "#Ecommerce", "#ERP", "#AutoParts", "#UAE", "#B2B", "#FreeTrial", "#ECOMAE", "#SpareParts"]),
        new(
            "tiktok",
            "TikTok",
            "Vertical 9:16, 15–60s. Hook in 2 seconds. Burn-in captions; Arabic/Urdu subtitles lift GCC & Pakistan reach.",
            ["#B2B", "#EcommerceTips", "#SmallBusiness", "#UAEBusiness", "#ERP", "#AutoParts", "#DubaiBusiness", "#TechTok", "#LearnOnTikTok", "#SpareParts", "#VINSearch"]),
    ];

    /// <summary>Pack tab order in PHP <c>epc_social_render_pack_tab()</c>.</summary>
    public static IReadOnlyList<string> PackTabPlatforms { get; } = ["linkedin", "instagram", "facebook", "x"];

    public static SocialPackPlatform PackPlatform(string key)
        => PackPlatforms.FirstOrDefault(p => p.Key == key) ?? PackPlatforms[0];

    private static readonly Dictionary<string, IReadOnlyList<SocialPackPost>> PostsByPlatform = new(StringComparer.Ordinal)
    {
        ["linkedin"] =
        [
            new("Post 1 — One stack for spare parts", "Most UAE spare-parts teams still quote on WhatsApp and reconcile stock in Excel.\n\nECOM AE puts storefront, warehouse stock, crosses, ERP, and B2B accounts on one database:\n✅ Part search + cross references\n✅ Multi-warehouse availability\n✅ Retail & wholesale pricing profiles\n✅ UAE VAT & e-invoice ready\n✅ Orders that post straight into finance\n\nStop stitching five tools. Run one stack.\n\n👉 Live demo: ecomae.com · See it in market: epartscart.com\n\n#ECOMAE #AutoParts #UAEBusiness #ERP #B2B #SpareParts"),
            new("Post 2 — Prices only for approved buyers", "Guest browsers should see catalogues — not your margin.\n\nOn ECOM AE / eParts Cart:\n🔒 Guests see availability & price as ***\n✅ Retail customers unlock prices instantly\n⏳ Wholesale stays pending until CP approval\n\nProtect your trade pricing without hiding your catalogue.\n\nBuilt for UAE B2B auto parts — not a bolted-on plugin.\n\n🔗 ecomae.com | epartscart.com\n\n#B2B #Wholesale #UAEBusiness #AutoParts #TradePricing"),
            new("Post 3 — UAE compliance without the scramble", "E-invoicing and VAT workflows are expanding across the UAE.\n\nECOM AE ships compliance as core — not a retrofit:\n📋 Peppol / PINT-AE ready invoicing\n📋 FTA export paths\n📋 TRN-aware tax documents\n📋 VAT-aware pricing for retail & wholesale\n\nPrepare once. Operate every day.\n\nExplore: ecomae.com\n\n#UAECompliance #UAEVAT #Einvoicing #FTA #DubaiFintech"),
            new("Post 4 — Crosses, VIN, warehouse truth", "Buyers ask: “Do you have it — and which number fits?”\n\nECOM AE’s auto-parts layer answers in one flow:\n🔧 Article + brand search\n🔧 Cross-reference catalogue\n🔧 UAE warehouse stock signals\n🔧 Supplier / storage captions for ops\n🔧 B2B credit & approval workflows\n\nYour competitors are still typing into chat apps.\nYou can be publishing live offers from Control Panel.\n\nepartscart.com · ecomae.com\n\n#AutoParts #SparePartsUAE #VIN #B2B #Ecommerce"),
        ],
        ["instagram"] =
        [
            new("Post 1 — Search → stock → order", "Part number in. Offer out. 🔧\n\nCrosses · UAE warehouses · login for live prices.\n\nOne platform for spare parts trading — not five tabs.\n\n👉 Link in bio — epartscart.com\n\n#AutoPartsUAE #SpareParts #DubaiBusiness #ECOMAE #B2B"),
            new("Post 2 — *** until you log in", "Catalogue open. Margin closed. 🔒\n\nGuests see *** on qty, term, warehouse & price.\nRetail → instant. Wholesale → manager approve.\n\nThat’s how modern B2B parts shops protect pricing.\n\nLink in bio ↗\n\n#Wholesale #TradePricing #UAEBusiness #AutoParts"),
            new("Post 3 — Meet Layla", "Meet Layla 🤖 — your AI walkthrough for ECOM AE.\n\nIndustry template → ERP → UAE compliance. No waiting on a sales call.\n\nFree sandbox → link in bio\n\n#AI #DubaiTech #ECOMAE #Ecommerce"),
            new("Post 4 — Built for UAE traders", "Auto parts 🔧 Electronics 📱 Fashion 👗 Jewellery 💎\n\nIndustry templates. One cloud. Go live fast.\n\nMade for UAE traders & wholesalers. 🇦🇪\n\nStart free → ecomae.com\n\n#MadeForUAE #WholesaleUAE #ERP #CloudPlatform"),
        ],
        ["facebook"] =
        [
            new("Post 1 — Still quoting on WhatsApp?", "Still juggling Excel stock + WhatsApp quotes + a separate shop?\n\nThere’s a cleaner way:\n🛒 Storefront with part search & crosses\n📦 Multi-warehouse availability\n💰 Retail / wholesale pricing rules\n📋 UAE VAT & invoice workflows\n🤝 Trade accounts with approval\n\nOne login. One database.\n\n👇 Try the demo at ecomae.com — or browse epartscart.com"),
            new("Post 2 — Free sandbox, real workflow", "🎉 Free sandbox — no card required.\n\nWalk the full flow with Layla (AI guide):\n🔧 Spare parts catalogue & VIN-style lookup\n📦 Stock + crosses\n🧾 Orders into ERP\n🇦🇪 VAT-aware documents\n\n👉 Start at ecomae.com\n\nWhat do you sell? Drop it in the comments 👇"),
            new("Post 3 — Protect trade prices", "📢 For UAE wholesalers: don’t publish your margin to every visitor.\n\nECOM AE / eParts Cart protocol:\n✅ Guests → *** on qty, term, info, price\n✅ Retail → auto-approve, prices unlock\n✅ Wholesale → CP manager approval first\n\nCatalogue stays findable. Pricing stays controlled.\n\n🔗 ecomae.com"),
            new("Post 4 — B2B portal buyers expect", "B2B buyers expect more than a chat thread. 📣\n\nGive them:\n💳 Contract / profile pricing\n📋 Order history & re-order\n🏦 Clear account status\n✅ Approval workflows for wholesale\n\nTurn spare-parts wholesale into a modern self-service portal.\n\n👉 Book a look: ecomae.com"),
        ],
        ["x"] =
        [
            new("Post 1 — One cloud", "UAE spare-parts ops still run on WhatsApp + Excel + a separate shop.\n\nECOM AE replaces the stack.\n\nOne cloud. Search → stock → invoice.\n\necomae.com | epartscart.com"),
            new("Post 2 — Price protocol", "Guests: *** on qty, term, warehouse, price.\nRetail: unlock instantly.\nWholesale: CP approve first.\n\nThat’s B2B pricing done right.\n\nepartscart.com\n\n#AutoParts #B2B #UAE"),
            new("Post 3 — Crosses + stock", "Article in → crosses out → UAE warehouse signal.\n\nOEM / aftermarket in one search.\n\n#SpareParts #AutoParts #UAE #B2B\n\nepartscart.com"),
            new("Post 4 — Free demo", "Free sandbox. No card.\nMeet Layla — AI guide.\n\nSee your industry live, then decide.\n\necomae.com\n\n#FreeTrial #ECOMAE #UAE"),
        ],
        ["tiktok"] =
        [
            new("Reel 1 — WhatsApp quotes → one search", "POV: Customer sends 12 part numbers on WhatsApp 😅\n\nYou: open search → crosses → stock → quote.\n\nScreen-record the CP / storefront flow.\n\n#AutoParts #UAEBusiness #B2B #SpareParts"),
            new("Reel 2 — VIN / OEM → cart", "Stop mistyping part numbers. 🔧\n\nLookup → OEM/aftermarket → add to cart → ERP invoice.\n\nBuilt for UAE / GCC / Pakistan parts desks.\n\n#VIN #OEM #SpareParts #ERP"),
            new("Reel 3 — *** price lock", "Guest view: *** *** *** ***\n\nLogin retail → prices unlock.\nWholesale → wait for manager.\n\nProtect your margin on camera.\n\n#TradePricing #B2B #TechTok"),
            new("Reel 4 — 5 tools vs 1", "Things I cancelled after one platform:\n❌ Separate shop\n❌ Stock spreadsheet\n❌ Manual VAT scramble\n❌ CRM notepad\n\n✅ One login\n\n#DubaiBusiness #ERP #LearnOnTikTok"),
        ],
    };

    /// <summary>PHP <c>epc_social_pack_posts()</c>.</summary>
    public static IReadOnlyList<SocialPackPost> PackPosts(string platform)
        => PostsByPlatform.TryGetValue(platform, out var posts) ? posts : [];

    /// <summary>PHP <c>epc_social_pack_posts_for_brand()</c>.</summary>
    public static IReadOnlyList<SocialPackPost> PackPostsForBrand(string platform, SocialBrandContext brand)
        => [.. PackPosts(platform).Select(p => new SocialPackPost(AdaptText(p.Title, brand), AdaptText(p.Caption, brand)))];

    /// <summary>PHP <c>epc_social_instagram_reels_ideas()</c>.</summary>
    public static IReadOnlyList<SocialPackPost> InstagramReelIdeas { get; } =
    [
        new("Carousel — 5 slides", "1) WhatsApp quoting chaos\n2) One search box\n3) Crosses + UAE stock\n4) *** until approved login\n5) CTA → link in bio / epartscart.com"),
        new("Reel — Order to invoice", "15s screen record: offer → cart → stock move → invoice PDF. Text overlay each beat."),
        new("Reel — Guest vs login", "Split: guest *** mask vs logged-in retail price. End with wholesale “awaiting approval” note."),
        new("Story — Day in the parts desk", "Morning orders → pick → dispatch → bank rec. Poll sticker: “Still on Excel?”"),
    ];

    /// <summary>PHP <c>epc_social_tiktok_specs()</c>.</summary>
    public static IReadOnlyList<(string Label, string Value)> TikTokSpecs { get; } =
    [
        ("Aspect ratio", "9:16 vertical (1080×1920 recommended)"),
        ("Duration", "15–60 sec (hook in first 2 sec)"),
        ("Captions", "Burn-in text + platform auto-captions (Arabic/Urdu for GCC/PK)"),
        ("Safe zone", "Keep logos/text away from bottom 250px (UI overlap)"),
        ("Posting", "3–5× per week; repurpose Instagram Reels"),
        ("CTA", "Link in bio → storefront or demo URL"),
        ("Ready videos", "Use the sample reels below (host on your CDN / public HTTPS for Publish)"),
    ];

    /// <summary>PHP <c>epc_social_video_library()</c>.</summary>
    public static IReadOnlyList<SocialVideo> VideoLibrary { get; } =
    [
        new("guide-connect", "Guide — Connect accounts", "guide", "/content/social_media/videos/guide-connect-accounts.mp4", "", "16:9", "Paste Meta / TikTok tokens in Connected accounts, then Test connection."),
        new("guide-pack", "Guide — Marketing pack", "guide", "/content/social_media/videos/guide-marketing-pack.mp4", "", "16:9", "Copy ready captions, save drafts, or publish live where enabled."),
        new("guide-publish", "Guide — Publish from Drafts", "guide", "/content/social_media/videos/guide-publish-drafts.mp4", "", "16:9", "Public HTTPS media URL required for Facebook, Instagram, and TikTok."),
        new("reel-vin", "Sample Reel — VIN → Cart", "reel", "/content/social_media/videos/reel-vin-search.mp4", "", "9:16", "Vertical template: lookup → OEM → cart. Swap in your screen recording."),
        new("reel-stock", "Sample Reel — UAE stock", "reel", "/content/social_media/videos/reel-stock-parts.mp4", "", "9:16", "Warehouse + crosses narrative; pair with login-to-see-prices CTA."),
        new("reel-b2b", "Sample Reel — B2B portal", "reel", "/content/social_media/videos/reel-b2b-portal.mp4", "", "9:16", "Retail auto-approve vs wholesale CP approval — modern trade story."),
        new("reel-five", "Sample Reel — 5 tools vs 1", "reel", "/content/social_media/videos/reel-five-tools.mp4", "", "9:16", "Classic before/after: cancel the tool stack, keep one login."),
    ];

    /// <summary>PHP <c>epc_social_x_thread_starter()</c>.</summary>
    public const string XThreadStarter = "🧵 Why UAE spare-parts teams move to one stack (a thread):\n\n1/ Most desks still run WhatsApp quotes + Excel stock + a separate shop. Every week = reconciliation debt.\n\n2/ ECOM AE connects search → crosses → warehouse → cart → ERP. One database, not five exports.\n\n3/ Pricing protocol: guests see ***; retail unlocks instantly; wholesale needs CP approval. Protect margin without hiding the catalogue.\n\n4/ UAE compliance is core: VAT-aware docs and e-invoice paths — not a plugin afterthought.\n\n5/ Publish social from Control Panel (Facebook / Instagram / TikTok) with encrypted account vault.\n\n6/ Free sandbox + Layla AI guide. No card. See your industry live.\n\n→ ecomae.com · live example epartscart.com";

    private static readonly IReadOnlyList<SocialTrendFormat> AllTrendFormats =
    [
        new("Before / After workflow", "TikTok, Reels", "15s: WhatsApp quoting chaos → one search with crosses + stock."),
        new("POV parts desk", "TikTok, Stories", "Screen-record article → cross → warehouse → cart in real time."),
        new("Carousel feature list", "Instagram, LinkedIn", "5 slides: pain → search → *** price lock → wholesale approve → CTA."),
        new("Trade pricing alert", "LinkedIn, Facebook", "Explain guest *** masks + retail instant / wholesale CP approval."),
        new("Customer quote stitch", "TikTok, X", "Reply to “send price on WhatsApp” with B2B portal / login CTA."),
    ];

    /// <summary>PHP <c>epc_social_trending_formats()</c> — ISO week parity rotates the 4-format slice.</summary>
    public static IReadOnlyList<SocialTrendFormat> TrendingFormats(DateTimeOffset now)
    {
        var week = ISOWeek.GetWeekOfYear(now.UtcDateTime.Date);
        var offset = week % 2;
        var slice = AllTrendFormats.Skip(offset).Take(4).ToList();
        if (slice.Count < 4)
        {
            slice.AddRange(AllTrendFormats.Take(4 - slice.Count));
        }

        return slice;
    }

    private static readonly Dictionary<string, IReadOnlyList<string>> HooksByIndustry = new(StringComparer.Ordinal)
    {
        ["auto_parts"] =
        [
            "VIN / OEM lookup in under 10 seconds",
            "Cross-reference education for buyers",
            "Guest *** price lock vs retail unlock",
            "Wholesale pending → CP approve story",
            "Stop WhatsApp quoting — show search → cart",
            "UAE warehouse stock + term transparency (for logged-in buyers)",
        ],
        ["electronics"] =
        [
            "Spec comparison carousel",
            "RMA / warranty workflow reel",
            "Multi-warehouse stock accuracy",
            "Bundle deals for GCC retailers",
        ],
        ["fashion"] =
        [
            "Variant SKU lookbook Reel",
            "Size guide pinned post",
            "Ramadan / Eid collection drop",
            "Influencer unboxing with shop link",
        ],
        ["jewellery"] =
        [
            "Gallery product cards showcase",
            "Gold rate + making charge transparency",
            "Appointment booking CTA",
            "Gift season carousel",
        ],
        ["medical"] =
        [
            "Compliance-first supply chain post",
            "Batch traceability explainer",
            "B2B clinic ordering portal",
            "Cold chain / expiry alerts",
        ],
        ["platform"] =
        [
            "Multi-tenant Super CP demo",
            "Go live in 24 hours story",
            "UAE e-invoice compliance built-in",
            "Industry template showcase",
        ],
    };

    /// <summary>PHP <c>epc_social_industry_hooks()</c>.</summary>
    public static IReadOnlyList<string> IndustryHooks(string industry, SocialBrandContext brand)
    {
        var list = HooksByIndustry.TryGetValue(industry, out var hooks) ? hooks : HooksByIndustry["platform"];
        return [.. list.Select(h => AdaptText(h, brand))];
    }

    /// <summary>PHP <c>epc_social_hashtags_for_industry()</c>.</summary>
    public static IReadOnlyList<string> HashtagsForIndustry(string industry, string country)
    {
        var tags = new List<string> { "#Ecommerce", "#B2B", "#DigitalTransformation" };
        tags.AddRange(country == "Pakistan"
            ? ["#PakistanBusiness", "#Karachi", "#Lahore", "#SME"]
            : new[] { "#UAEBusiness", "#Dubai", "#GCC", "#SME" });

        var extra = industry switch
        {
            "auto_parts" => new[] { "#AutoParts", "#SpareParts", "#VIN" },
            "electronics" => ["#Electronics", "#TechRetail", "#ConsumerTech"],
            "fashion" => ["#FashionRetail", "#ModestFashion", "#StyleUAE"],
            "jewellery" => ["#Jewellery", "#Gold", "#LuxuryRetail"],
            "medical" => ["#Healthcare", "#MedicalSupplies", "#Pharma"],
            _ => ["#ECOMAE", "#CloudERP", "#SaaS"],
        };

        tags.AddRange(extra);
        return tags;
    }

    /// <summary>PHP <c>epc_social_generate_caption()</c>.</summary>
    public static SocialGeneratedCaption GenerateCaption(SocialBrandContext brand, string platform, string? productLine)
    {
        platform = (platform ?? string.Empty).Trim().ToLowerInvariant();
        var name = brand.BrandName;
        var industryWords = brand.Industry.Replace('_', ' ');
        var line = (productLine ?? string.Empty).Trim();
        var lines = new List<string>();

        if (platform == "tiktok")
        {
            lines.Add("POV: You run " + brand.Country + " " + industryWords + " without 5 apps.");
            lines.Add(line.Length > 0 ? "Today: " + line : "One platform. Orders → ERP → VAT. Automatically.");
            lines.Add("Link in bio → " + brand.Domain);
        }
        else if (platform == "instagram")
        {
            lines.Add(name + " — built for " + brand.Country + " traders.");
            lines.Add(line.Length > 0 ? line + " ✨" : "Storefront + ERP + CRM in one stack.");
            lines.Add("DM us or tap link in bio for a demo.");
        }
        else if (platform == "linkedin")
        {
            lines.Add("Most " + industryWords + " businesses in " + brand.Country + " still reconcile orders manually.");
            lines.Add(name + " connects commerce, inventory, and finance in one database.");
            lines.Add("Explore: " + brand.Website);
        }
        else
        {
            lines.Add(name + " — " + industryWords + " on one cloud.");
            lines.Add(line.Length > 0 ? line : "Go live faster. Operate smarter.");
            lines.Add(brand.Website);
        }

        return new SocialGeneratedCaption(
            platform,
            string.Join("\n\n", lines),
            string.Join(' ', HashtagsForIndustry(brand.Industry, brand.Country)));
    }

    /// <summary>PHP <c>epc_social_adapt_text()</c>.</summary>
    public static string AdaptText(string text, SocialBrandContext brand)
    {
        var brandTag = "#" + TagSafe.Replace(brand.BrandName.ToUpperInvariant(), string.Empty);
        return text
            .Replace("ECOM AE", brand.BrandName, StringComparison.Ordinal)
            .Replace("ecomae.official", brand.Handle, StringComparison.Ordinal)
            .Replace("https://www.ecomae.com", brand.Website, StringComparison.Ordinal)
            .Replace("www.ecomae.com", brand.Domain, StringComparison.Ordinal)
            .Replace("ecomae.com", brand.Domain, StringComparison.Ordinal)
            .Replace("#ECOMAE", brandTag, StringComparison.Ordinal);
    }

    /// <summary>PHP handle normalisation used by <c>epc_social_brand_context()</c>.</summary>
    public static string Handle(string brandName, string fallback)
    {
        var handle = HandleSafe.Replace(brandName.ToLowerInvariant(), string.Empty);
        return handle.Length > 0 ? handle : fallback;
    }

    /// <summary>PHP TikTok privacy levels accepted by <c>epc_social_save_account()</c>.</summary>
    public static IReadOnlyList<(string Value, string Label)> TikTokPrivacyLevels { get; } =
    [
        ("SELF_ONLY", "Private (SELF_ONLY — unaudited apps)"),
        ("FOLLOWER_OF_CREATOR", "Followers"),
        ("MUTUAL_FOLLOW_FRIENDS", "Friends"),
        ("PUBLIC_TO_EVERYONE", "Public (needs TikTok audit)"),
    ];

    /// <summary>PHP <c>epc_social_render_guide_tab()</c> steps.</summary>
    public static IReadOnlyList<SocialGuideStep> GuideSteps(SocialBrandContext brand, string integrationsUrl) =>
    [
        new("Connect accounts", "Open <strong>Connected accounts</strong>. Paste access token + Page / IG Business user ID (encrypted vault). Click <em>Test connection (live API)</em> — Facebook/Instagram hit Meta Graph; TikTok hits creator_info. Register apps in <a href=\"" + integrationsUrl + "\">Integrations hub</a> first."),
        new("Pick content from the pack", "Marketing pack ships 16 modern captions (LinkedIn, Instagram, Facebook, X) plus TikTok reel scripts. Tenant CP auto-adapts brand name, domain, and hashtags for <strong>" + brand.BrandName + "</strong>."),
        new("Publish from Drafts (with video)", "Open <strong>Drafts</strong> → Compose. Choose Facebook / Instagram / TikTok, paste a <em>public HTTPS</em> image or .mp4 URL (or click <em>Use as media URL</em> on a sample reel), then <strong>Publish now</strong>. Instagram: media → poll → media_publish. TikTok: PULL_FROM_URL (verify domain). Unaudited TikTok apps: SELF_ONLY only."),
        new("AI advisor", "Check weekly formats and industry hooks, then generate a caption for your product line. GCC: mix English + Arabic hashtags. Pakistan: Urdu/English on Facebook; English on LinkedIn."),
        new("Measure & handoff", "Published drafts store external post IDs. Track clicks via Web tracker / GA4. LinkedIn and X remain copy→native until those APIs are wired — vault still keeps tokens safe for your team."),
    ];

    /// <summary>PHP hub tab strip.</summary>
    public static IReadOnlyList<(string Key, string Label, string Icon)> Tabs { get; } =
    [
        ("pack", "Marketing pack", "fa-file-text-o"),
        ("tiktok", "TikTok", "fa-music"),
        ("instagram", "Instagram", "fa-instagram"),
        ("accounts", "Connected accounts", "fa-link"),
        ("ai", "AI advisor", "fa-magic"),
        ("drafts", "Drafts", "fa-pencil"),
        ("guide", "Guide", "fa-book"),
    ];

    /// <summary>PHP tab fallback: unknown tab renders the guide.</summary>
    public static string NormalizeTab(string? raw)
    {
        var tab = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Tabs.Any(t => t.Key == tab) ? tab : "pack";
    }

    /// <summary>PHP ready-post KPI: LinkedIn + Instagram + Facebook + X pack captions.</summary>
    public static int ReadyPostCount()
        => PackTabPlatforms.Sum(p => PackPosts(p).Count);
}
