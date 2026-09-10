using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosNotificationPrefsSaveWriteTests
{
    [Fact]
    public void Route_exposes_prefs_save()
    {
        Assert.Equal("/bos/notifications/prefs-save", EcomAeRoutes.BosNotificationsPrefsSave);
    }

    [Fact]
    public void Defaults_match_php()
    {
        Assert.Equal("*", BosNotificationWriteService.ResolveCategory(null));
        Assert.Equal("", BosNotificationWriteService.ResolveCategory(""));
        Assert.Equal("order", BosNotificationWriteService.ResolveCategory("order"));
        Assert.Equal("daily", BosNotificationWriteService.ResolveDigest(null));
        Assert.Equal("hourly", BosNotificationWriteService.ResolveDigest("hourly"));
        Assert.Equal(1, BosNotificationWriteService.ResolveChannel(null, 1));
        Assert.Equal(0, BosNotificationWriteService.ResolveChannel(null, 0));
        Assert.Equal(0, BosNotificationWriteService.ResolveChannel("0", 1));
        Assert.Equal(1, BosNotificationWriteService.ResolveChannel("1", 0));
    }

    [Fact]
    public void Page_posts_native_prefs_save()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/notifications/prefs-save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"user_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"tenant_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"channel_in_app\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"channel_email\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"channel_webhook\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"email_digest\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_prefs_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/notifications/prefs-save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("prefs_save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_notification_prefs", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_prefs_save_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosNotificationsPrefsSave", module, StringComparison.Ordinal);
        Assert.Contains("SavePrefsAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosNotificationWriteService.cs"));
        Assert.Contains("epc_notification_prefs_save", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_notification_prefs`", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
