using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyLoginBridgeMiddlewareTests
{
    [Fact]
    public void MiddlewareTypeIsRegisteredInProgram()
    {
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("UseMiddleware<LegacyLoginBridgeMiddleware>", program, StringComparison.Ordinal);
        Assert.Contains("UseAntiforgery()", program, StringComparison.Ordinal);
        var mid = program.IndexOf("UseMiddleware<LegacyLoginBridgeMiddleware>", StringComparison.Ordinal);
        var anti = program.IndexOf("UseAntiforgery()", StringComparison.Ordinal);
        Assert.True(mid >= 0 && anti > mid, "Login bridge middleware must run before UseAntiforgery");
    }

    [Fact]
    public void CpAndErpFormsPostToSurfaceLoginUrls()
    {
        var form = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor"));
        Assert.Contains("LoginPostHref", form, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ControlPanelLogin", form, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpLogin", form, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"@EcomAE.Platform.Routing.EcomAeRoutes.LegacyAdminLogin\"", form, StringComparison.Ordinal);
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
