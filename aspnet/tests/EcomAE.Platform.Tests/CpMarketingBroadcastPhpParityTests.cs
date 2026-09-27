using System;
using System.IO;
using System.Linq;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMarketingBroadcastPhpParityTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "aspnet")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void Tabs_MatchPhpOrder()
    {
        Assert.Equal(
            new[] { "email", "whatsapp", "history", "guide" },
            MarketingBroadcastCatalog.Tabs.Select(t => t.Key).ToArray());
    }

    [Theory]
    [InlineData("", "email")]
    [InlineData("history", "history")]
    [InlineData("WhatsApp", "whatsapp")]
    [InlineData("nope", "email")]
    public void NormalizeTab_FallsBackToEmail(string input, string expected)
        => Assert.Equal(expected, MarketingBroadcastCatalog.NormalizeTab(input));

    [Fact]
    public void AudienceModes_MatchPhpKeys()
    {
        var expected = new[] { "all", "with_orders", "group", "manual" };
        Assert.Equal(expected, MarketingBroadcastCatalog.EmailAudienceModes.Select(m => m.Key).ToArray());
        Assert.Equal(expected, MarketingBroadcastCatalog.WhatsappAudienceModes.Select(m => m.Key).ToArray());
        Assert.Equal(expected, CpMarketingBroadcastRecipients.Modes);
    }

    [Fact]
    public void Templates_CoverPhpKeys()
    {
        foreach (var key in new[] { "promo_sale", "new_arrivals", "service_reminder", "blank" })
        {
            Assert.Equal(key, MarketingBroadcastCatalog.EmailTemplate(key).Key);
        }

        foreach (var key in new[] { "promo_bilingual", "brochure_share", "follow_up", "event_invite", "blank" })
        {
            Assert.Equal(key, MarketingBroadcastCatalog.WhatsappTemplate(key).Key);
        }
    }

    [Fact]
    public void ApplyVars_ReplacesPhpMergeTags()
    {
        var merged = MarketingBroadcastCatalog.ApplyVars(
            "Hi {{customer_name}} from {{shop_name}} — {{shop_url}}",
            "Sara",
            "Indus",
            "https://indus.ae");
        Assert.Equal("Hi Sara from Indus — https://indus.ae", merged);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("", 50)]
    [InlineData("250", 100)]
    [InlineData("37", 37)]
    public void BatchLimit_ClampsToPhpRange(string raw, int expected)
        => Assert.Equal(expected, CpMarketingBroadcastRecipients.NormalizeBatchLimit(raw));

    [Fact]
    public void ManualRecipients_FilterNonEmailsForEmailChannel()
    {
        var rows = CpMarketingBroadcastRecipients.ManualRecipients(
            "a@b.com\nnot-an-email\nc@d.com",
            "email");
        Assert.Equal(new[] { "a@b.com", "c@d.com" }, rows.Select(r => r.Email).ToArray());
    }

    [Fact]
    public void WhatsappShareUrl_MatchesPhpHelper()
    {
        Assert.Equal(
            "https://wa.me/971500000000?text=Hi%20there",
            CpMarketingBroadcastRecipients.WhatsappShareUrl("+971 50 000 0000", "Hi there"));
        Assert.Equal(string.Empty, CpMarketingBroadcastRecipients.WhatsappShareUrl("", "Hi"));
    }

    [Fact]
    public void Schema_UsesPhpCampaignAndLogTables()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Cp/CpMarketingBroadcastRecipients.cs"));
        Assert.Contains("epc_marketing_broadcast_campaigns", text, StringComparison.Ordinal);
        Assert.Contains("epc_marketing_broadcast_log", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_RendersPhpComposerSurfaces()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingBroadcastApp.razor"));

        Assert.Contains("epc-marketing-broadcast.css", text, StringComparison.Ordinal);
        Assert.Contains("ICpMarketingBroadcastService", text, StringComparison.Ordinal);
        Assert.Contains("/cp/marketing-broadcast/write", text, StringComparison.Ordinal);
        Assert.Contains("name=\"body_html\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"body_text\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"batch_limit\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"audience_meta_manual\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"audience_meta_group\"", text, StringComparison.Ordinal);
        Assert.Contains("epc-mb-preview", text, StringComparison.Ordinal);
        Assert.Contains("epc-mb-wa-bubble", text, StringComparison.Ordinal);
        Assert.Contains("SMTP ready", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpMarketingBroadcastDigestAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteEndpoint_RequiresConfirmAndCpCapability()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var idx = text.IndexOf("EcomAeRoutes.CpMarketingBroadcastWrite", StringComparison.Ordinal);
        Assert.True(idx > 0);
        var slice = text.Substring(idx, Math.Min(6000, text.Length - idx));
        Assert.Contains("session.Capabilities.Contains(\"cp\")", slice, StringComparison.Ordinal);
        Assert.Contains("confirm && key is \"send_email\" or \"send_whatsapp\"", slice, StringComparison.Ordinal);
        Assert.Contains("SendWhatsappCampaignAsync", slice, StringComparison.Ordinal);
        Assert.Contains("SendEmailCampaignAsync", slice, StringComparison.Ordinal);
    }

    [Fact]
    public void Services_AreRegistered()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpMarketingBroadcastService, EcomAE.Platform.Cp.CpMarketingBroadcastService", text, StringComparison.Ordinal);
        Assert.Contains("ICpMarketingBroadcastWriteService, EcomAE.Platform.Cp.CpMarketingBroadcastWriteService", text, StringComparison.Ordinal);
    }
}
