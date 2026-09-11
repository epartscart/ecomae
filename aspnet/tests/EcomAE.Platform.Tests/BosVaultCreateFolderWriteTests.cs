using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosVaultCreateFolderWriteTests
{
    [Fact]
    public void Route_exposes_create_folder()
    {
        Assert.Equal("/bos/vault/create-folder", EcomAeRoutes.BosVaultCreateFolder);
    }

    [Fact]
    public void Php_site_key_and_path_match_php()
    {
        Assert.Equal("acme1", BosVaultWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("acme_1", BosVaultWriteService.PhpBosSiteKey("Acme_1"));
        Assert.Equal("", BosVaultWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal("", BosVaultWriteService.PhpBosSiteKey(null));

        Assert.Equal("/Inbox/", BosVaultWriteService.BuildParentPath("/", "Inbox"));
        Assert.Equal("/root/Inbox/", BosVaultWriteService.BuildParentPath("/root/", "Inbox"));
        Assert.Equal("/root/Inbox/", BosVaultWriteService.BuildParentPath("/root", "Inbox"));
        Assert.Equal("/Docs/", BosVaultWriteService.BuildParentPath(null, "Docs"));
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosVaultWriteService(new UnconfiguredConnections())
            .CreateFolderAsync("!!!", "Inbox", 0, 0);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_create_folder()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/vault/create-folder\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"parent_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_folder_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/vault/create-folder");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("create_folder", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_create_folder", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_folders", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_folder_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosVaultCreateFolder", module, StringComparison.Ordinal);
        Assert.Contains("CreateFolderAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosVaultWriteService.cs"));
        Assert.Contains("epc_vault_create_folder", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_vault_folders`", service, StringComparison.Ordinal);
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
