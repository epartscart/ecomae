using EcomAE.Platform.Api.Scaffolding;
using EcomAE.Platform.Integrations.Scaffolding;
using EcomAE.Platform.Security.Scaffolding;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeApiIntegrationScaffoldOptionsTests
{
    [Fact]
    public void GraphQlScaffoldOptionsDefaultToDisabledAndNotPublic()
    {
        var options = new EcomAeGraphQlScaffoldOptions();
        Assert.Equal("EcomAe:GraphQl", EcomAeGraphQlScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ExposePublicEndpoint);
        Assert.Equal("/graphql", options.Path);
    }

    [Fact]
    public void GrpcScaffoldOptionsDefaultToDisabledAndNotPublic()
    {
        var options = new EcomAeGrpcScaffoldOptions();
        Assert.Equal("EcomAe:Grpc", EcomAeGrpcScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ExposePublicEndpoint);
        Assert.Equal(5200, options.Port);
    }

    [Fact]
    public void BlockchainScaffoldOptionsDefaultToDisabledAndNeverBusinessSor()
    {
        var options = new EcomAeBlockchainScaffoldOptions();
        Assert.Equal("EcomAe:Blockchain", EcomAeBlockchainScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.UseAsBusinessSourceOfRecord);
    }

    [Fact]
    public void RateLimitScaffoldOptionsDefaultToDisabledAndKeepLegacyThrottle()
    {
        var options = new EcomAeRateLimitScaffoldOptions();
        Assert.Equal("EcomAe:RateLimit", EcomAeRateLimitScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplaceLegacyApiClientThrottle);
        Assert.Equal(100, options.PermitLimit);
        Assert.Equal(60, options.WindowSeconds);
    }
}
