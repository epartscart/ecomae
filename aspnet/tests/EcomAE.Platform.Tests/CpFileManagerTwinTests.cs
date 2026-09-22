using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpFileManagerTwinTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "epc-fm-" + Guid.NewGuid().ToString("N"));

    public CpFileManagerTwinTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        File.WriteAllBytes(Path.Combine(_root, "images", "a.png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(_root, ".htaccess"), "deny");
        File.WriteAllText(Path.Combine(_root, "doc.pdf"), "%PDF");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    [Fact]
    public void Paths_and_names_follow_elfinder_locks()
    {
        Assert.Equal("", CpFileManagerService.NormalizeRelative(null));
        Assert.Equal("images/x", CpFileManagerService.NormalizeRelative("/images//x/"));
        Assert.Null(CpFileManagerService.NormalizeRelative("../etc"));
        Assert.Null(CpFileManagerService.NormalizeRelative("images/.git"));
        Assert.False(CpFileManagerService.IsValidName(".hidden"));
        Assert.False(CpFileManagerService.IsValidName("a/b"));
        Assert.True(CpFileManagerService.IsValidName("Photo 1.jpg"));
        Assert.True(CpFileManagerService.IsAllowedUpload("x.PDF"));
        Assert.False(CpFileManagerService.IsAllowedUpload("x.php"));
        Assert.Equal("1.5 KB", CpFileManagerService.HumanSize(1536));
    }

    [Fact]
    public void Listing_hides_dot_files_and_sorts_folders_first()
    {
        var svc = CpFileManagerService.ForRoot(_root);
        var list = svc.List("");
        Assert.True(list.RootExists);
        Assert.Equal(["images", "doc.pdf"], list.Entries.Select(e => e.Name).ToArray());
        Assert.True(list.Entries[0].IsDirectory);

        var sub = svc.List("images");
        Assert.Equal(["images"], sub.Crumbs);
        Assert.Single(sub.Entries);
        Assert.True(sub.Entries[0].IsImage);
        Assert.Equal("images/a.png", sub.Entries[0].RelativePath);

        Assert.Equal("", svc.List("../..").RelativePath);
    }

    [Fact]
    public async Task Writes_stay_inside_root_and_respect_type_rules()
    {
        var svc = CpFileManagerService.ForRoot(_root);
        Assert.True(svc.CreateDirectory("", "docs").Succeeded);
        Assert.Equal("exists", svc.CreateDirectory("", "docs").Code);
        Assert.Equal("invalid", svc.CreateDirectory("", ".git").Code);
        Assert.Equal("invalid", svc.CreateDirectory("..", "x").Code);

        Assert.True(svc.Rename("images", "a.png", "b.png").Succeeded);
        Assert.Equal("invalid", svc.Rename("images", "b.png", "b.php").Code);
        Assert.True(File.Exists(Path.Combine(_root, "images", "b.png")));

        Assert.Equal("not_empty", svc.Delete("", ["images"]).Code);
        Assert.True(svc.Delete("images", ["b.png"]).Succeeded);
        Assert.True(svc.Delete("", ["images", "docs"]).Succeeded);
        Assert.Equal("missing", svc.Delete("", ["nothing"]).Code);

        await using var ms = new MemoryStream([9, 9, 9]);
        var ok = await svc.UploadAsync("", new FormFile(ms, 0, 3, "files", "new.gif"), CancellationToken.None);
        Assert.True(ok.Succeeded);
        Assert.True(File.Exists(Path.Combine(_root, "new.gif")));
        ms.Position = 0;
        Assert.Equal("exists", (await svc.UploadAsync("", new FormFile(ms, 0, 3, "files", "new.gif"), CancellationToken.None)).Code);
        ms.Position = 0;
        Assert.Equal("invalid", (await svc.UploadAsync("", new FormFile(ms, 0, 3, "files", "shell.php"), CancellationToken.None)).Code);
        Assert.False(File.Exists(Path.Combine(_root, "shell.php")));
    }

    [Fact]
    public void Page_routes_and_registration_exist()
    {
        Assert.Equal("/cp/file-manager/write", EcomAeRoutes.ControlPanelFileManagerWrite);
        var root = FindRepoRoot();
        Assert.Contains("ICpFileManagerService", File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ControlPanelFileManagerWrite", File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs")), StringComparison.Ordinal);
        Assert.Contains("\"/content/files/{**relativePath}\"", File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs")), StringComparison.Ordinal);

        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpFileManagerApp.razor"));
        Assert.Contains("@inject ICpFileManagerService Files", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/file-manager/write\"", page, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", page, StringComparison.Ordinal);
        Assert.Contains("epc-filemanager__toolbar", page, StringComparison.Ordinal);
        Assert.Contains("Upload images (JPEG, PNG, GIF) and PDFs.", page, StringComparison.Ordinal);
        Assert.Contains("Server files", page, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpFileManagerDigestAsync", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", page, StringComparison.Ordinal);
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

        throw new InvalidOperationException("repo root not found");
    }
}
