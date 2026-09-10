using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosVaultNewVersionWriteTests
{
    [Fact]
    public void Route_exposes_new_version()
    {
        Assert.Equal("/bos/vault/new-version", EcomAeRoutes.BosVaultNewVersion);
    }

    [Fact]
    public void Php_version_data_matches_php_defaults()
    {
        Assert.Equal(("", 0L, "", "", 0L), BosVaultWriteService.ParseVersionData(null));
        Assert.Equal(("", 0L, "", "", 0L), BosVaultWriteService.ParseVersionData(""));
        Assert.Equal(("", 0L, "", "", 0L), BosVaultWriteService.ParseVersionData("{}"));
        Assert.Equal(("", 0L, "", "", 0L), BosVaultWriteService.ParseVersionData("[]"));
        Assert.Equal(("", 0L, "", "", 0L), BosVaultWriteService.ParseVersionData("not-json"));
        var parsed = BosVaultWriteService.ParseVersionData(
            "{\"file_path\":\"/docs/a.pdf\",\"file_size\":12.9,\"checksum\":\"abc\",\"change_note\":\"rev\",\"uploaded_by\":\"4x\"}");
        Assert.Equal("/docs/a.pdf", parsed.FilePath);
        Assert.Equal(12L, parsed.FileSize);
        Assert.Equal("abc", parsed.Checksum);
        Assert.Equal("rev", parsed.ChangeNote);
        Assert.Equal(4L, parsed.UploadedBy);
        Assert.Equal(12L, BosVaultWriteService.PhpIntval("12mb"));
        Assert.Equal(0L, BosVaultWriteService.PhpIntval("abc"));
    }

    [Fact]
    public void Page_posts_native_new_version()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/vault/new-version\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"document_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"file_path\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_new_version_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/vault/new-version");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_vault_new_version", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_versions", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_new_version_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosVaultNewVersion", module, StringComparison.Ordinal);
        Assert.Contains("NewVersionAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosVaultWriteService.cs"));
        Assert.Contains("epc_vault_new_version", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_vault_versions`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"vault\"", policy, StringComparison.Ordinal);
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
