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
    // Join + token companion/results are public for new users.
    [InlineData("/lifeos/join", false)]
    [InlineData("/lifeos/mobile", false)]
    [InlineData("/lifeos/results", false)]
    [InlineData("/lifeos/results/json", false)]
    [InlineData("/lifeos/companion", false)]
    [InlineData("/lifeos/companion/track", false)]
    [InlineData("/lifeos/companion/talk", false)]
    // Joined-clients board + JSON are public; console still requires login.
    [InlineData("/lifeos/clients-board", false)]
    [InlineData("/lifeos/clients/cp", false)]
    [InlineData("/lifeos/directory", false)]
    [InlineData("/lifeos/app", true)]
    [InlineData("/lifeos/brain", true)]
    public void Personal_surfaces_require_login(string path, bool required)
    {
        Assert.Equal(required, LifeOsPersonalAuthGateMiddleware.RequiresPersonalLogin(path));
    }

    [Fact]
    public void Join_and_login_are_separate_public_entry_paths()
    {
        Assert.False(LifeOsPersonalAuthGateMiddleware.RequiresPersonalLogin("/lifeos/join"));
        Assert.False(LifeOsPersonalAuthGateMiddleware.RequiresPersonalLogin("/lifeos/login"));
        Assert.True(LifeOsPersonalAuthGateMiddleware.RequiresPersonalLogin("/lifeos/app"));
        Assert.True(LifeOsPersonalAuthGateMiddleware.IsJoinPath("/lifeos/join"));
        Assert.True(LifeOsPersonalAuthGateMiddleware.IsJoinPath("/lifeos/join/"));
        Assert.False(LifeOsPersonalAuthGateMiddleware.IsJoinPath("/lifeos/login"));
    }
}
