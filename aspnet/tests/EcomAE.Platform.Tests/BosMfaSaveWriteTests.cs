using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosMfaSaveWriteTests
{
    [Fact]
    public void Route_exposes_save()
    {
        Assert.Equal("/bos/mfa/save", EcomAeRoutes.BosMfaSave);
        Assert.Equal("/bos/ajax/mfa-policy", EcomAeRoutes.BosAjaxMfaPolicy);
    }

    [Fact]
    public void Php_defaults_match_php()
    {
        Assert.Equal("__platform__", BosMfaWriteService.PhpTenantKey(null));
        Assert.Equal("", BosMfaWriteService.PhpTenantKey(""));
        Assert.Equal("acme", BosMfaWriteService.PhpTenantKey("acme"));
        Assert.Equal(72, BosMfaWriteService.PhpGraceHours(null));
        Assert.Equal(0, BosMfaWriteService.PhpGraceHours(""));
        Assert.Equal(48, BosMfaWriteService.PhpGraceHours("48x"));
        Assert.Equal("[]", BosMfaWriteService.EncodeJsonArray(null));
        Assert.Equal("[]", BosMfaWriteService.EncodeJsonArray("{"));
        Assert.Equal("[]", BosMfaWriteService.EncodeJsonArray("{}"));
        Assert.Equal("[\"super_admin\"]", BosMfaWriteService.EncodeJsonArray("[\"super_admin\"]"));
    }

    [Fact]
    public async Task Unconfigured_factory_fails_db()
    {
        var written = await new BosMfaWriteService(new UnconfiguredConnections())
            .SavePolicyAsync(null, "[]", "[]", null);
        Assert.False(written.Succeeded);
        Assert.Equal("db", written.Code);
        Assert.Equal("Database unavailable", written.Message);
    }

    [Fact]
    public void Page_posts_native_save()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/mfa/save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"tenant_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"roles\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"paths\"", razor, StringComparison.Ordinal);
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
            item.AspNetRouteOrCapability == "/bos/mfa/save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_mfa_save_policy", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_mfa_policy", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
        Assert.Contains("/bos/ajax/mfa-policy", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosMfaSave", module, StringComparison.Ordinal);
        Assert.Contains("IBosMfaWriteService", module, StringComparison.Ordinal);
        Assert.Contains("SavePolicyAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosMfaWriteService.cs"));
        Assert.Contains("epc_mfa_save_policy", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_mfa_policy`", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosMfaWriteService", program, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"mfa\"", policy, StringComparison.Ordinal);
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
