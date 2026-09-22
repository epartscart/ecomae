using System.Data.Common;
using System.IO.Compression;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPacksTwinTests
{
    private sealed class Unconfigured : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    [Theory]
    [InlineData("1.0", "1.0", 0)]
    [InlineData("1.1", "1.0", 1)]
    [InlineData("1.0", "1.1", -1)]
    [InlineData("2", "1.9.9", 1)]
    [InlineData("1.10", "1.1", 0)]
    public void CompareVersions_MatchesPhpDigitPadding(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(CpPacksService.CompareVersions(a, b)));
    }

    [Theory]
    [InlineData("/content/files/", true)]
    [InlineData("/<backend_dir>/content/plugins/", true)]
    [InlineData("content/files/", false)]
    [InlineData("/../etc/", false)]
    [InlineData("/content/.git/", false)]
    [InlineData("", false)]
    public void SafeServerPath_RejectsRelativeAndTraversal(string path, bool ok)
    {
        Assert.Equal(ok, CpPacksService.SafeServerPath(path));
    }

    [Fact]
    public void DestinationPath_ReplacesBackendDirAndStaysUnderDocRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "epc-root"));
        var dst = CpPacksService.DestinationPath(root, "/<backend_dir>/content/x/", "a.php");
        Assert.Equal(Path.Combine(root, "cp", "content", "x", "a.php"), dst);
        Assert.StartsWith(root + Path.DirectorySeparatorChar, dst, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_ParsesPackJsonSectionsLikePackControlPhp()
    {
        const string json = """
            {"files":[{"server_path":"/content/files/","file_name":"a.txt","pack_path":"files"}],
             "templates":[{"id":5,"caption":"Tpl"}],
             "modules_prototypes":[{"id":7,"prototype_name":"proto"}],
             "plugins":[{"id":9,"caption":"Plg"}]}
            """;
        var d = CpPacksService.Detail(new CpPackRow(1, "C", "1.0", "A", "n", 0), json);
        Assert.Single(d.Files);
        Assert.Equal("a.txt", d.Files[0].FileName);
        Assert.Equal((5L, "Tpl"), (d.Templates[0].Id, d.Templates[0].Caption));
        Assert.Equal((7L, "proto"), (d.ModulesPrototypes[0].Id, d.ModulesPrototypes[0].Caption));
        Assert.Equal((9L, "Plg"), (d.Plugins[0].Id, d.Plugins[0].Caption));
    }

    [Fact]
    public void Detail_ToleratesEmptyOrBrokenJson()
    {
        var row = new CpPackRow(1, "C", "1.0", "A", "n", 0);
        Assert.Empty(CpPacksService.Detail(row, "").Files);
        Assert.Empty(CpPacksService.Detail(row, "{not json").Templates);
    }

    [Fact]
    public async Task Reads_ReturnEmptyWhenDbUnconfigured()
    {
        var svc = new CpPacksService(new Unconfigured());
        var list = await svc.ListAsync(3);
        Assert.Equal(0, list.Total);
        Assert.Null(await svc.OpenAsync(1));
        Assert.Null(await svc.OpenAsync(0));
        Assert.Equal("invalid", (await svc.DeleteAsync(0, "")).Code);
        Assert.Equal("db", (await svc.DeleteAsync(1, "")).Code);
    }

    [Fact]
    public async Task Install_RequiresDocRootAndDb()
    {
        var svc = new CpPacksService(new Unconfigured());
        using var ms = new MemoryStream();
        Assert.Equal("db", (await svc.InstallAsync(ms, "", 1)).Code);
    }

    private sealed class Configured : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;
        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("db must not be touched before archive validation");
    }

    private static MemoryStream Zip(params (string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var w = new StreamWriter(z.CreateEntry(name).Open(), Encoding.UTF8);
                w.Write(content);
            }
        }

        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Install_RejectsTraversalEntriesMissingPackJsonAndInvalidMetadataBeforeDb()
    {
        var root = Directory.CreateTempSubdirectory("epc-packs-").FullName;
        try
        {
            var svc = new CpPacksService(new Configured());
            Assert.Equal("archive", (await svc.InstallAsync(Zip(("../evil.php", "x")), root, 1)).Code);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "evil.php")));
            Assert.Equal("pack_json", (await svc.InstallAsync(Zip(("readme.txt", "x")), root, 1)).Code);
            Assert.Equal("pack_json", (await svc.InstallAsync(Zip(("pack.json", "{oops")), root, 1)).Code);
            Assert.Equal("invalid", (await svc.InstallAsync(Zip(("pack.json", """{"name":"n","version":"1"}""")), root, 1)).Code);
            Assert.Equal("files", (await svc.InstallAsync(Zip(("pack.json", """{"name":"n","caption":"c","version":"1","files":[{"server_path":"../","file_name":"a","pack_path":""}]}""")), root, 1)).Code);
            Assert.Equal("files", (await svc.InstallAsync(Zip(("pack.json", """{"name":"n","caption":"c","version":"1","files":[{"server_path":"/content/","file_name":"missing.txt","pack_path":"files"}]}""")), root, 1)).Code);
            Assert.Equal("modules", (await svc.InstallAsync(Zip(("pack.json", """{"name":"n","caption":"c","version":"1","modules_prototypes":[{"is_frontend":1,"prototype_name":"p","content_type":"exe","content":""}]}""")), root, 1)).Code);
            Assert.False(Directory.Exists(Path.Combine(root, "cp", "tmp", "pack_setup")), "tmp folder must be cleared");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Routes_LinkMap_AndRegistration_PointToPacksApp()
    {
        Assert.Equal("/cp/packs/write", EcomAeRoutes.CpPacksWrite);
        Assert.Equal("/cp/packs-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/packs/packs_manager"));
        Assert.Equal("/cp/packs-app?setup=1", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/packs/setup"));
        Assert.Equal("/cp/packs-app?pack_id=3", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/packs/pack_control?pack_id=3"));

        var root = FindRepoRoot();
        Assert.Contains("ICpPacksService", File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpPacksWrite", module, StringComparison.Ordinal);
        Assert.Contains("form.Files.GetFile(\"pack_file\")", module, StringComparison.Ordinal);
    }

    [Fact]
    public void PacksApp_IsPhpShapedManagerDetailSetupTwin()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPacksApp.razor"));
        Assert.Contains("@page \"/cp/packs-app\"", src, StringComparison.Ordinal);
        Assert.Contains("@inject ICpPacksService Packs", src, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/packs/write\" enctype=\"multipart/form-data\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"pack_file\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"setup_pack\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"delete\"", src, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\" value=\"true\"", src, StringComparison.Ordinal);
        Assert.Contains("<th>Technical name</th>", src, StringComparison.Ordinal);
        Assert.Contains("Module prototypes", src, StringComparison.Ordinal);
        Assert.Contains("CpTemplatesPluginsService.ReadPage", src, StringComparison.Ordinal);
        Assert.Contains("/CP/packs/pack_control?pack_id=", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ISurfaceDashboardSummaryReporter", src, StringComparison.Ordinal);
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
