using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMobileAppsWriteTests
{
    [Fact]
    public void Route_exposes_mobile_apps_write()
    {
        Assert.Equal("/cp/mobile-apps/write", EcomAeRoutes.CpMobileAppsWrite);
    }

    [Fact]
    public void Merge_clips_and_keeps_smtp()
    {
        Assert.Equal("abc", CpMobileAppsWriteService.Clip("  abc  ", 120));
        Assert.Equal("12345", CpMobileAppsWriteService.Clip("123456789", 5));
        var root = JsonNode.Parse("""{"smtp":{"smtp_host":"keep.me"},"mobile":{"app_name":"old"}}""");
        var merged = CpMobileAppsWriteService.MergeMobile(
            root,
            new CpMobileAppsSaveRequest(
                true, "  eParts  ", "com.epartscart.app", "epartscart://", "www.epartscart.com",
                "https://www.epartscart.com/en/", "", "", true, "proj", false));
        Assert.Equal("keep.me", merged["smtp"]!["smtp_host"]!.GetValue<string>());
        var mobile = merged["mobile"]!.AsObject();
        Assert.True(mobile["enabled"]!.GetValue<bool>());
        Assert.Equal("eParts", mobile["app_name"]!.GetValue<string>());
        Assert.Equal("com.epartscart.app", mobile["bundle_id"]!.GetValue<string>());
        Assert.True(mobile["pwa_enabled"]!.GetValue<bool>());
        Assert.False(mobile["push_enabled"]!.GetValue<bool>());
        Assert.Equal("proj", mobile["firebase_project_id"]!.GetValue<string>());
    }

    [Fact]
    public void Page_posts_native_save_mobile()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpMobileAppsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/mobile-apps/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_mobile\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"app_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_mobile_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/mobile-apps/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_integrations.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_mobile()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpMobileAppsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpMobileAppsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpMobileAppsWriteService.cs"));
        Assert.Contains("epc_integrations_save_tenant_config", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_portal_site_settings`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
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
