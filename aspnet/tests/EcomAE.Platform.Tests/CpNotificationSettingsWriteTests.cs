using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpNotificationSettingsWriteTests
{
    [Fact]
    public void Routes_expose_toggle()
    {
        Assert.Equal("/cp/notifications-app", EcomAeRoutes.ControlPanelNotificationsApp);
        Assert.Equal("/cp/notifications/toggle", EcomAeRoutes.ControlPanelNotificationsToggle);
    }

    [Fact]
    public void Normalize_type_and_flag()
    {
        Assert.Equal("email", CpNotificationSettingsWriteService.NormalizeType("EMAIL"));
        Assert.Equal("sms", CpNotificationSettingsWriteService.NormalizeType("sms"));
        Assert.Null(CpNotificationSettingsWriteService.NormalizeType("push"));
        Assert.Equal(1, CpNotificationSettingsWriteService.NormalizeFlag(9));
        Assert.Equal(0, CpNotificationSettingsWriteService.NormalizeFlag(0));
    }

    [Fact]
    public void Page_posts_native_toggle_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpNotificationsApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/notifications/toggle\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"notification_id\"", razor);
        Assert.Contains("name=\"type\"", razor);
        Assert.Contains("name=\"set_send\"", razor);
        Assert.Contains("does not invent a send", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("email_body", razor);
        Assert.DoesNotContain("sms_text", razor);
    }

    [Fact]
    public void Digest_sql_omits_template_bodies()
    {
        Assert.Contains("notifications_settings", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("email_body", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("sms_text", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("email_subject", LegacySurfaceDashboardSql.SelectCpNotificationSettings, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_toggle_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/notifications/toggle");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("set_send", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpNotificationSettingsWriteService", src);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelNotificationsToggle", module);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
