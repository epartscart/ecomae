using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosVaultUploadWriteTests
{
    [Fact]
    public void Route_exposes_upload()
    {
        Assert.Equal("/bos/vault/upload", EcomAeRoutes.BosVaultUpload);
    }

    [Fact]
    public void Php_file_data_matches_php_defaults()
    {
        Assert.Equal("acme1", BosVaultWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosVaultWriteService.PhpBosSiteKey("!!!"));

        var empty = BosVaultWriteService.ParseFileData(null);
        Assert.Equal(0L, empty.FolderId);
        Assert.Equal("", empty.Filename);
        Assert.Equal("application/octet-stream", empty.MimeType);
        Assert.Equal(0L, empty.FileSize);
        Assert.Equal("[]", empty.TagsJson);
        Assert.Equal("tenant", empty.AccessLevel);
        Assert.Equal(0L, empty.RetentionDays);
        Assert.Equal(0L, empty.UploadedBy);
        Assert.Equal("", empty.FilePath);
        Assert.Equal("", empty.Checksum);
        Assert.Equal(empty, BosVaultWriteService.ParseFileData(""));
        Assert.Equal(empty, BosVaultWriteService.ParseFileData("{}"));
        Assert.Equal(empty, BosVaultWriteService.ParseFileData("[]"));
        Assert.Equal(empty, BosVaultWriteService.ParseFileData("not-json"));

        var parsed = BosVaultWriteService.ParseFileData(
            "{\"folder_id\":\"3x\",\"filename\":\"a.pdf\",\"mime_type\":\"application/pdf\",\"file_size\":12.9,\"tags\":[\"legal\"],\"access_level\":\"private\",\"retention_days\":30,\"uploaded_by\":4,\"file_path\":\"/docs/a.pdf\",\"checksum\":\"abc\"}");
        Assert.Equal(3L, parsed.FolderId);
        Assert.Equal("a.pdf", parsed.Filename);
        Assert.Equal("application/pdf", parsed.MimeType);
        Assert.Equal(12L, parsed.FileSize);
        Assert.Equal("[\"legal\"]", parsed.TagsJson);
        Assert.Equal("private", parsed.AccessLevel);
        Assert.Equal(30L, parsed.RetentionDays);
        Assert.Equal(4L, parsed.UploadedBy);
        Assert.Equal("/docs/a.pdf", parsed.FilePath);
        Assert.Equal("abc", parsed.Checksum);

        var omittedMime = BosVaultWriteService.ParseFileData("{\"filename\":\"x\"}");
        Assert.Equal("application/octet-stream", omittedMime.MimeType);
        Assert.Equal("tenant", omittedMime.AccessLevel);
        Assert.Equal("[]", omittedMime.TagsJson);

        var emptyMime = BosVaultWriteService.ParseFileData("{\"mime_type\":\"\",\"access_level\":\"\"}");
        Assert.Equal("", emptyMime.MimeType);
        Assert.Equal("", emptyMime.AccessLevel);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosVaultWriteService(new UnconfiguredConnections())
            .UploadAsync("!!!", "{\"filename\":\"a.bin\"}");
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_upload()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/vault/upload\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"filename\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"file_path\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_upload_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/vault/upload");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("upload", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_upload", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_documents", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_vault_versions", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_upload_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosVaultUpload", module, StringComparison.Ordinal);
        Assert.Contains("UploadAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosVaultWriteService.cs"));
        Assert.Contains("epc_vault_upload", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_vault_documents`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_vault_versions`", service, StringComparison.Ordinal);
        Assert.Contains("Initial upload", service, StringComparison.Ordinal);
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
