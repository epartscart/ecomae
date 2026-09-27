using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP twin contract for cp/content/control/portal/epc_social_media_hub_panel.php and the
/// account/draft helpers in content/social_media/epc_social_media_helpers.php.
/// </summary>
public sealed class CpSocialMediaHubPhpParityTests
{
    [Fact]
    public void Tabs_MatchThePhpPanelStrip()
    {
        Assert.Equal(
            ["pack", "tiktok", "instagram", "accounts", "ai", "drafts", "guide"],
            SocialMediaPackCatalog.Tabs.Select(t => t.Key));
        Assert.Equal("pack", SocialMediaPackCatalog.NormalizeTab("unknown"));
        Assert.Equal("guide", SocialMediaPackCatalog.NormalizeTab("GUIDE"));
    }

    [Fact]
    public void Platforms_MatchPhpSocialPlatforms()
    {
        Assert.Equal(
            ["instagram", "tiktok", "facebook", "linkedin", "x"],
            SocialMediaPackCatalog.Platforms.Select(p => p.Key));
    }

    [Fact]
    public void MarketingPack_ShipsThePhpCaptionCounts()
    {
        foreach (var platform in new[] { "linkedin", "instagram", "facebook", "x", "tiktok" })
        {
            Assert.Equal(4, SocialMediaPackCatalog.PackPosts(platform).Count);
        }

        Assert.Equal(16, SocialMediaPackCatalog.ReadyPostCount());
        Assert.Equal(4, SocialMediaPackCatalog.InstagramReelIdeas.Count);
        Assert.Equal(7, SocialMediaPackCatalog.VideoLibrary.Count);
        Assert.Equal(3, SocialMediaPackCatalog.VideoLibrary.Count(v => v.Kind == "guide"));
    }

    [Fact]
    public void BrandAdaptation_RewritesPlatformCopyForTheTenant()
    {
        var brand = new SocialBrandContext(
            "indus", "Indus Jewellery", "indusjewellery", "https://www.indus.ae", "indus.ae",
            "jewellery", "UAE", "GCC", false);

        var adapted = SocialMediaPackCatalog.AdaptText(
            "ECOM AE runs on ecomae.com — follow #ECOMAE", brand);

        Assert.Equal("Indus Jewellery runs on indus.ae — follow #INDUSJEWELLERY", adapted);
        Assert.DoesNotContain("ecomae.com", SocialMediaPackCatalog.PackPostsForBrand("linkedin", brand)[0].Caption);
    }

    [Fact]
    public void TrendingFormats_RotateFourPerWeekLikePhp()
    {
        var even = SocialMediaPackCatalog.TrendingFormats(new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero));
        var odd = SocialMediaPackCatalog.TrendingFormats(new DateTimeOffset(2026, 1, 19, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(4, even.Count);
        Assert.Equal(4, odd.Count);
        Assert.NotEqual(even[0].Name, odd[0].Name);
    }

    [Fact]
    public void IndustryHooksAndHashtags_FollowPhpIndustryMap()
    {
        var brand = CpSocialHubService.DefaultBrand("platform");
        Assert.Contains(SocialMediaPackCatalog.IndustryHooks("auto_parts", brand), h => h.Contains("VIN", StringComparison.Ordinal));
        Assert.Contains("#Jewellery", SocialMediaPackCatalog.HashtagsForIndustry("jewellery", "UAE"));
        Assert.Contains("#PakistanBusiness", SocialMediaPackCatalog.HashtagsForIndustry("auto_parts", "Pakistan"));
        Assert.Contains("#UAEBusiness", SocialMediaPackCatalog.HashtagsForIndustry("auto_parts", "UAE"));
    }

    [Fact]
    public void GenerateCaption_UsesBrandDomainAndIndustryHashtags()
    {
        var brand = CpSocialHubService.DefaultBrand("platform");
        var caption = SocialMediaPackCatalog.GenerateCaption(brand, "tiktok", "brake kits");
        Assert.Equal("tiktok", caption.Platform);
        Assert.Contains("brake kits", caption.Caption, StringComparison.Ordinal);
        Assert.Contains(brand.Domain, caption.Caption, StringComparison.Ordinal);
        Assert.Contains("#ECOMAE", caption.Hashtags, StringComparison.Ordinal);
    }

    [Fact]
    public void CredentialVault_RoundTripsPerSiteKeyAndRejectsForeignKeys()
    {
        var cipher = CpSocialCrypto.Encrypt("{\"access_token\":\"abc\"}", "indus");
        Assert.NotEqual("{\"access_token\":\"abc\"}", cipher);
        Assert.Equal("{\"access_token\":\"abc\"}", CpSocialCrypto.Decrypt(cipher, "indus"));
        Assert.Equal(string.Empty, CpSocialCrypto.Decrypt(cipher, "other_tenant"));
        Assert.Equal(string.Empty, CpSocialCrypto.Decrypt("not-base64!!", "indus"));
    }

    [Fact]
    public void PublicAccountMeta_NeverExposesTheToken()
    {
        var cipher = CpSocialCrypto.Encrypt(
            "{\"access_token\":\"secret\",\"page_id\":\"11\",\"ig_user_id\":\"22\",\"privacy_level\":\"PUBLIC_TO_EVERYONE\"}",
            "indus");
        var meta = CpSocialHubService.ParseCredentials(cipher, "indus");

        Assert.Equal("11", meta.PageId);
        Assert.Equal("22", meta.IgUserId);
        Assert.Equal("PUBLIC_TO_EVERYONE", meta.PrivacyLevel);
        Assert.True(meta.HasToken);

        var empty = CpSocialHubService.ParseCredentials(string.Empty, "indus");
        Assert.False(empty.HasToken);
        Assert.Equal("SELF_ONLY", empty.PrivacyLevel);
    }

    [Fact]
    public void BlankTokenFields_KeepTheStoredCredentials()
    {
        var stored = CpSocialCrypto.Encrypt(
            "{\"access_token\":\"keep-me\",\"api_key\":\"key\",\"page_id\":\"11\",\"privacy_level\":\"FOLLOWER_OF_CREATOR\"}",
            "indus");
        var payload = new JsonObject
        {
            ["access_token"] = string.Empty,
            ["api_key"] = string.Empty,
            ["api_secret"] = string.Empty,
            ["page_id"] = string.Empty,
            ["ig_user_id"] = string.Empty,
            ["open_id"] = string.Empty,
            ["privacy_level"] = "SELF_ONLY",
        };

        CpSocialHubWriteService.MergeStoredCredentials(
            payload,
            stored,
            "indus",
            new CpSocialHubSaveAccountRequest("indus", "instagram", "", "", "", "", "", "", "", "", "", false, "indus.ae"));

        Assert.Equal("keep-me", payload["access_token"]!.GetValue<string>());
        Assert.Equal("key", payload["api_key"]!.GetValue<string>());
        Assert.Equal("11", payload["page_id"]!.GetValue<string>());
        Assert.Equal("FOLLOWER_OF_CREATOR", payload["privacy_level"]!.GetValue<string>());
    }

    [Fact]
    public void PrivacyLevel_StaysInThePhpWhitelist()
    {
        Assert.Equal("SELF_ONLY", CpSocialHubWriteService.NormalizePrivacy("nonsense"));
        Assert.Equal("PUBLIC_TO_EVERYONE", CpSocialHubWriteService.NormalizePrivacy("public_to_everyone"));
        Assert.Equal("SELF_ONLY", CpSocialHubWriteService.NormalizePrivacy(null));
    }

    [Fact]
    public void SiteKeyResolution_KeepsTenantsScopedToTheirHost()
    {
        Assert.Equal("platform", CpSocialHubWriteService.ResolveSiteKey(null, superCpHost: true, "cp.ecomae.com"));
        Assert.Equal("indus", CpSocialHubWriteService.ResolveSiteKey("Indus", superCpHost: true, "cp.ecomae.com"));
        Assert.Equal("indus-ae", CpSocialHubWriteService.ResolveSiteKey(null, superCpHost: false, "www.indus.ae"));
    }

    [Fact]
    public void Page_RendersEveryPhpTabAndAccountAction()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSocialHubApp.razor"));

        Assert.Contains("epc-social-media-hub.css", text, StringComparison.Ordinal);
        Assert.Contains("epc-social-tabs", text, StringComparison.Ordinal);
        foreach (var action in new[] { "save_account", "test_account", "delete_account", "save_draft" })
        {
            Assert.Contains("value=\"" + action + "\"", text, StringComparison.Ordinal);
        }

        Assert.Contains("Caption generator", text, StringComparison.Ordinal);
        Assert.Contains("Publishing guide", text, StringComparison.Ordinal);
        Assert.DoesNotContain("This page does not invent a send.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteEndpoint_DispatchesTheAccountActions()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));

        Assert.Contains("\"save_account\" => await writes.SaveAccountAsync(", text, StringComparison.Ordinal);
        Assert.Contains("\"test_account\" => await writes.TestAccountAsync(", text, StringComparison.Ordinal);
        Assert.Contains("writes.DeleteAccountAsync(", text, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
