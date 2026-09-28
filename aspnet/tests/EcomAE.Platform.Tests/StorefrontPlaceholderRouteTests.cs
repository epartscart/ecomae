using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPlaceholderRouteTests
{
    [Fact]
    public void LegacyPlaceholderRouteRedirectsToTheRealAspNetStorefront()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs");
        var text = File.ReadAllText(path);

        Assert.Contains(
            """endpoints.MapGet("/storefront/migration-placeholder", () =>""",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            """Results.Redirect("/storefront/app", permanent: false)""",
            text,
            StringComparison.Ordinal);
        var routeStart = text.IndexOf(
            """endpoints.MapGet("/storefront/migration-placeholder", () =>""",
            StringComparison.Ordinal);
        var routeEnd = text.IndexOf(
            "endpoints.MapGet(EcomAeRoutes.StorefrontAccount",
            routeStart,
            StringComparison.Ordinal);
        Assert.True(routeStart >= 0 && routeEnd > routeStart);
        var route = text[routeStart..routeEnd];
        Assert.DoesNotContain("migration placeholder", route, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PHP storefront remains authoritative", route, StringComparison.Ordinal);
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
