using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PlatformReadinessTests
{
    [Fact]
    public void ReadinessRouteFailsClosedDuringApplicationStopping()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));

        Assert.Contains("public const string Readiness = \"/ready\";", routes, StringComparison.Ordinal);
        Assert.Contains("app.Lifetime.ApplicationStopping.Register(() => acceptingTraffic = false);", program, StringComparison.Ordinal);
        Assert.Contains("Results.StatusCode(StatusCodes.Status503ServiceUnavailable)", program, StringComparison.Ordinal);
        Assert.Contains("ShutdownTimeout = TimeSpan.FromSeconds(30)", program, StringComparison.Ordinal);
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
