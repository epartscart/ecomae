using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosDunningCreateProfileWriteTests
{
    [Fact]
    public void Route_exposes_create_profile()
    {
        Assert.Equal("/bos/dunning/create-profile", EcomAeRoutes.BosDunningCreateProfile);
    }

    [Fact]
    public void Php_site_key_and_steps_match_php()
    {
        Assert.Equal("acme1", BosDunningWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("acme_1", BosDunningWriteService.PhpBosSiteKey("Acme_1"));
        Assert.Equal("", BosDunningWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal("", BosDunningWriteService.PhpBosSiteKey(null));

        var defaults = BosDunningWriteService.ParseSteps(null);
        Assert.Equal(7, defaults.Count);
        Assert.Equal(1, defaults[0].Day);
        Assert.Equal("email", defaults[0].Action);
        Assert.Equal("friendly_reminder", defaults[0].Template);
        Assert.Equal("Friendly Payment Reminder", defaults[0].Subject);
        Assert.Equal(60, defaults[^1].Day);
        Assert.Equal("final_notice", defaults[^1].Template);

        Assert.Equal(defaults, BosDunningWriteService.ParseSteps(""));
        Assert.Equal(defaults, BosDunningWriteService.ParseSteps("[]"));
        Assert.Equal(defaults, BosDunningWriteService.ParseSteps("not-json"));

        var custom = BosDunningWriteService.ParseSteps(
            "[{\"day\":\"3\",\"action\":\"email\",\"template\":\"nudge\",\"subject\":\"Pay soon\"}]");
        Assert.Single(custom);
        Assert.Equal(3, custom[0].Day);
        Assert.Equal("email", custom[0].Action);
        Assert.Equal("nudge", custom[0].Template);
        Assert.Equal("Pay soon", custom[0].Subject);

        var json = BosDunningWriteService.SerializeSteps(defaults);
        Assert.Contains("\"day\":1", json, StringComparison.Ordinal);
        Assert.Contains("friendly_reminder", json, StringComparison.Ordinal);
        Assert.Contains("Final Notice Before Legal Action", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosDunningWriteService(new UnconfiguredConnections())
            .CreateProfileAsync("!!!", "Default", null);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_create_profile()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/dunning/create-profile\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"steps\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_profile_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/dunning/create-profile");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("profile_create", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_dunning_profile_create", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_dunning_profiles", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_profile_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosDunningCreateProfile", module, StringComparison.Ordinal);
        Assert.Contains("CreateProfileAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosDunningWriteService.cs"));
        Assert.Contains("epc_dunning_profile_create", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_dunning_profiles`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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
