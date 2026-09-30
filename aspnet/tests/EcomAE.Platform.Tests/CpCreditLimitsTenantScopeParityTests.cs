using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCreditLimitsTenantScopeParityTests
{
    [Fact]
    public void CreditLimits_UsesResolvedTenantSiteForFormAndWriteScope()
    {
        var page = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCreditLimitsApp.razor"));
        var module = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));

        Assert.Contains("value=\"@_siteKey\"", page, StringComparison.Ordinal);
        Assert.Contains("TenantResolutionMiddleware.HttpContextItemKey", page, StringComparison.Ordinal);
        Assert.Contains("siteKey = tenant.SiteKey", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ControlPanelCreditLimitsSet", module, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"epartscart\"", page, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
