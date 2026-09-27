using System.Text;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceEntryPreviewInstallerTests
{
    [Fact]
    public void InstallerContainsOnlyApprovedExactEntryAndLoginRoutes()
    {
        var script = ReadRepoFile("scripts/cloudpanel_install_surface_entry_previews.sh");
        var example = ReadRepoFile("deploy/aspnet/nginx-surface-entry-previews-example.conf");

        foreach (var route in new[] { "/erp", "/erp/login", "/cp/app", "/cp/login", "/bos", "/bos/login" })
        {
            Assert.Contains($"location = {route}", example, StringComparison.Ordinal);
        }

        Assert.Contains("ECOMAE_CONFIRM_INSTALL_SURFACE_ENTRY_PREVIEWS", script, StringComparison.Ordinal);
        Assert.Contains("exact-route", script, StringComparison.Ordinal);
        Assert.DoesNotContain("location ^~ /erp/", example, StringComparison.Ordinal);
        Assert.DoesNotContain("location ^~ /cp/", example, StringComparison.Ordinal);
        Assert.DoesNotContain("location ^~ /bos/", example, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            var parent = Directory.GetParent(directory)?.FullName;
            Assert.NotNull(parent);
            directory = parent!;
        }

        return File.ReadAllText(Path.Combine(directory, relativePath), Encoding.UTF8);
    }
}
