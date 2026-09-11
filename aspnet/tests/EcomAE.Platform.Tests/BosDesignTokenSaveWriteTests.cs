using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosDesignTokenSaveWriteTests
{
    [Fact]
    public void Route_exposes_save()
    {
        Assert.Equal("/bos/design-tokens/save", EcomAeRoutes.BosDesignTokensSave);
    }

    [Fact]
    public void Php_keys_and_whitelist_match_php()
    {
        Assert.Equal("acme1", BosDesignTokenWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosDesignTokenWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal("brand_primary", BosDesignTokenWriteService.PhpSettingKey("brand_primary"));
        Assert.Equal("randrimary", BosDesignTokenWriteService.PhpSettingKey("Brand-Primary!"));
        Assert.Equal("", BosDesignTokenWriteService.PhpSettingKey("!!!"));
        Assert.True(BosDesignTokenWriteService.AllowedKeys.Contains("brand_primary"));
        Assert.True(BosDesignTokenWriteService.AllowedKeys.Contains("white_label_login"));
        Assert.False(BosDesignTokenWriteService.AllowedKeys.Contains("Brand_Primary"));
        Assert.False(BosDesignTokenWriteService.AllowedKeys.Contains("secret"));
    }

    [Fact]
    public async Task Missing_or_unknown_keys_fail_invalid_before_db()
    {
        var missing = await new BosDesignTokenWriteService(new UnconfiguredConnections())
            .SaveTokenAsync("!!!", "brand_primary", "#111");
        Assert.False(missing.Succeeded);
        Assert.Equal("invalid", missing.Code);
        Assert.Equal("Missing site_key or setting_key", missing.Message);

        var missingSetting = await new BosDesignTokenWriteService(new UnconfiguredConnections())
            .SaveTokenAsync("acme", "!!!", "#111");
        Assert.False(missingSetting.Succeeded);
        Assert.Equal("invalid", missingSetting.Code);
        Assert.Equal("Missing site_key or setting_key", missingSetting.Message);

        var unknown = await new BosDesignTokenWriteService(new UnconfiguredConnections())
            .SaveTokenAsync("acme", "secret", "#111");
        Assert.False(unknown.Succeeded);
        Assert.Equal("invalid", unknown.Code);
        Assert.Equal("Unknown setting_key", unknown.Message);
    }

    [Fact]
    public void Page_posts_native_save()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/design-tokens/save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"setting_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"value\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/design-tokens/save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("save_token", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_design_tokens_save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_settings", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("/bos/ajax/save-token", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosDesignTokensSave", module, StringComparison.Ordinal);
        Assert.Contains("SaveTokenAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosDesignTokenWriteService.cs"));
        Assert.Contains("epc_design_tokens_save", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_settings`", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosDesignTokenWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"design-tokens\"", policy, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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
