using System.Text;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ReleaseIdentityTests
{
    [Fact]
    public void ReleaseIdentityRouteIsPubliclyDefinedWithoutBroadCutover()
    {
        var routes = ReadRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs");
        var program = ReadRepoFile("aspnet/src/EcomAE.Platform/Program.cs");

        Assert.Contains("ReleaseIdentity = \"/migration/release\"", routes, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(EcomAeRoutes.ReleaseIdentity", program, StringComparison.Ordinal);
        Assert.Contains("phpReferenceOnly = true", program, StringComparison.Ordinal);
        Assert.Contains("broadCutoverAllowed = false", program, StringComparison.Ordinal);
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
