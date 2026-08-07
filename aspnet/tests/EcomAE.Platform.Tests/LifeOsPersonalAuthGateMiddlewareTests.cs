using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPersonalAuthGateMiddlewareTests
{
    [Theory]
    [InlineData("/lifeos", false)]
    [InlineData("/lifeos/login", false)]
    [InlineData("/lifeos/spec", false)]
    [InlineData("/lifeos/cinematic-app", false)]
    [InlineData("/lifeos/join.js", false)]
    [InlineData("/lifeos/companion.js", false)]
    [InlineData("/lifeos/manifest.webmanifest", false)]
    [InlineData("/lifeos/join", true)]
    [InlineData("/lifeos/mobile", true)]
    [InlineData("/lifeos/results", true)]
    [InlineData("/lifeos/results/json", true)]
    [InlineData("/lifeos/companion", true)]
    [InlineData("/lifeos/companion/track", true)]
    [InlineData("/lifeos/companion/talk", true)]
    [InlineData("/lifeos/clients-board", true)]
    [InlineData("/lifeos/clients/cp", true)]
    [InlineData("/lifeos/directory", true)]
    [InlineData("/lifeos/app", true)]
    public void Personal_surfaces_require_login(string path, bool required)
    {
        Assert.Equal(required, LifeOsPersonalAuthGateMiddleware.RequiresPersonalLogin(path));
    }
}
