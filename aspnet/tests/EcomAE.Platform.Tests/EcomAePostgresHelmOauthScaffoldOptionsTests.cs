using EcomAE.Platform.Auth.Scaffolding;
using EcomAE.Platform.Data.Scaffolding;
using EcomAE.Platform.Presentation.Scaffolding;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAePostgresHelmOauthScaffoldOptionsTests
{
    [Fact]
    public void PostgresScaffoldOptionsDefaultToDisabledAndDoNotReplaceMysqlBridge()
    {
        var options = new EcomAePostgresScaffoldOptions();
        Assert.Equal("EcomAe:Postgres", EcomAePostgresScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplaceMysqlBridge);
        Assert.Equal(5432, options.Port);
        Assert.Equal("ecomae", options.Database);
    }

    [Fact]
    public void OAuthScaffoldOptionsDefaultToDisabledAndDoNotReplacePhpCookieBridge()
    {
        var options = new EcomAeOAuthScaffoldOptions();
        Assert.Equal("EcomAe:OAuth", EcomAeOAuthScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.RequireMfa);
        Assert.False(options.ReplacePhpCookieBridge);
        Assert.Equal("ecomae-platform", options.Audience);
    }

    [Fact]
    public void SpaScaffoldOptionsDefaultToDisabledAndDoNotReplaceBlazorHybrid()
    {
        var options = new EcomAeSpaScaffoldOptions();
        Assert.Equal("EcomAe:Spa", EcomAeSpaScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplaceBlazorHybridPresentation);
        Assert.Equal("angular", options.Framework);
        Assert.Equal("/api/v1", options.ApiBasePath);
    }
}
