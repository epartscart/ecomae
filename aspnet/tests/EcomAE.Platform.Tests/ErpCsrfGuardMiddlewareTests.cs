using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCsrfGuardMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/erp/ajax/so-to-invoice", true)]
    [InlineData("POST", "/erp/quality/ncr-update", true)]
    [InlineData("PUT", "/erp/x", true)]
    [InlineData("DELETE", "/erp/x", true)]
    [InlineData("POST", "/content/general_pages/ajax_epc_erp.php", true)]
    [InlineData("POST", "/erp/login", false)]
    [InlineData("POST", "/erp/logout", false)]
    [InlineData("GET", "/erp/ajax/dashboard", false)]
    [InlineData("POST", "/cp/orders/delete", false)]
    [InlineData("POST", "/erpx/other", false)]
    public void AppliesToErpUnsafeMethodsOnly(string method, string path, bool expected)
        => Assert.Equal(expected, ErpCsrfGuardMiddleware.AppliesTo(method, path));

    [Fact]
    public void ProgramRegistersGuardAfterCrossSiteGuard()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        var cross = program.IndexOf("UseMiddleware<AdminCrossSiteWriteGuardMiddleware>", StringComparison.Ordinal);
        var csrf = program.IndexOf("UseMiddleware<ErpCsrfGuardMiddleware>", StringComparison.Ordinal);
        Assert.True(cross >= 0 && csrf > cross);
        Assert.Contains("<AdminCsrfGuardBridge />", File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Components/App.razor")));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")) && !Directory.Exists(Path.Combine(dir.FullName, "aspnet", "src")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
