using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontAccountRouteTests
{
    [Fact]
    public void LegacyAccountRouteRedirectsToTheImplementedAspNetAccountPage()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs");
        var text = File.ReadAllText(path);

        Assert.Contains(
            """endpoints.MapGet(EcomAeRoutes.StorefrontAccount, () =>""",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            """Results.Redirect("/storefront/account-app", permanent: false)""",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Customer-gated account shell only", text, StringComparison.Ordinal);
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

        throw new FileNotFoundException(relative);
    }
}
