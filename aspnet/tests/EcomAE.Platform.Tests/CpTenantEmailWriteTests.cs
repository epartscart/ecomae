using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpTenantEmailWriteTests
{
    [Fact]
    public void Routes_expose_save_and_test()
    {
        Assert.Equal("/cp/tenant-email-app", EcomAeRoutes.ControlPanelTenantEmailApp);
        Assert.Equal("/cp/tenant-email/save", EcomAeRoutes.ControlPanelTenantEmailSave);
        Assert.Equal("/cp/tenant-email/test", EcomAeRoutes.ControlPanelTenantEmailTest);
    }

    [Fact]
    public void Page_posts_native_save_and_test_forms()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantEmailApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/tenant-email/save\"", razor);
        Assert.Contains("action=\"/cp/tenant-email/test\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"smtp_username\"", razor);
        Assert.Contains("name=\"test_to\"", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("smtp_password\" value=", razor);
    }

    [Fact]
    public void Module_maps_save_and_test()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelTenantEmailSave", src);
        Assert.Contains("ControlPanelTenantEmailTest", src);
        Assert.Contains("ICpTenantEmailWriteService", src);
    }

    [Fact]
    public void MergeSmtp_keeps_existing_password_when_blank()
    {
        var root = JsonNode.Parse("""{"smtp":{"smtp_password":"keep-me","smtp_host":"old"}}""");
        var merged = CpTenantEmailWriteService.MergeSmtp(
            root,
            new CpTenantEmailSaveRequest(true, "smtp.example.com", "465", "ssl", "user@example.com", "", "Desk", "desk@example.com"));
        var smtp = merged["smtp"]!.AsObject();
        Assert.Equal("keep-me", smtp["smtp_password"]!.GetValue<string>());
        Assert.Equal("smtp.example.com", smtp["smtp_host"]!.GetValue<string>());
        Assert.Equal("465", smtp["smtp_port"]!.GetValue<string>());
        Assert.Equal("ssl", smtp["smtp_encryption"]!.GetValue<string>());
        Assert.True(smtp["use_tenant_smtp"]!.GetValue<bool>());
    }

    [Fact]
    public void MergeSmtp_replaces_password_when_posted()
    {
        var root = JsonNode.Parse("""{"mobile":{"enabled":true},"smtp":{"smtp_password":"old"}}""");
        var merged = CpTenantEmailWriteService.MergeSmtp(
            root,
            new CpTenantEmailSaveRequest(false, "smtp.example.com", "", "none", "u", "new-secret", "", ""));
        Assert.True(merged["mobile"]!["enabled"]!.GetValue<bool>());
        Assert.Equal("new-secret", merged["smtp"]!["smtp_password"]!.GetValue<string>());
        Assert.Equal("587", merged["smtp"]!["smtp_port"]!.GetValue<string>());
        Assert.Equal(string.Empty, merged["smtp"]!["smtp_encryption"]!.GetValue<string>());
        Assert.False(merged["smtp"]!["use_tenant_smtp"]!.GetValue<bool>());
    }

    [Fact]
    public void Helpers_match_php_validation()
    {
        Assert.True(CpTenantEmailWriteService.IsEmail("ops@example.com"));
        Assert.False(CpTenantEmailWriteService.IsEmail("not-an-email"));
        Assert.Equal("tls", CpTenantEmailWriteService.NormalizeEncryption("TLS"));
        Assert.Equal(string.Empty, CpTenantEmailWriteService.NormalizeEncryption("none"));
        Assert.Equal("587", CpTenantEmailWriteService.NormalizePort(""));
    }

    [Fact]
    public void Digest_reads_username_not_password()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("smtp_username", src);
        Assert.Contains("hasPassword = !string.IsNullOrWhiteSpace(ReadJsonString(smtp, \"smtp_password\"))", src);
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
