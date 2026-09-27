using System.Text;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpWorkspacePreviewInstallerTests
{
    [Fact]
    public void InstallerUsesOnlyTheExactErpWorkspaceRoute()
    {
        var script = ReadRepoFile("scripts/cloudpanel_install_erp_workspace_preview.sh");
        var example = ReadRepoFile("deploy/aspnet/nginx-erp-workspace-preview-example.conf");

        Assert.Contains("ECOMAE_CONFIRM_INSTALL_ERP_WORKSPACE_PREVIEW", script, StringComparison.Ordinal);
        Assert.Contains("PHP remains the fallback", script, StringComparison.Ordinal);
        Assert.Contains("location = /erp/app", example, StringComparison.Ordinal);
        Assert.DoesNotContain("location ^~ /erp/", example, StringComparison.Ordinal);
        Assert.DoesNotContain("location /", example, StringComparison.Ordinal);
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
