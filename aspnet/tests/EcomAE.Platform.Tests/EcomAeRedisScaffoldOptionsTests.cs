using EcomAE.Platform.Caching;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeRedisScaffoldOptionsTests
{
    [Fact]
    public void RedisScaffoldOptionsDefaultToDisabledAndDoNotReplacePhpCookies()
    {
        var options = new EcomAeRedisScaffoldOptions();
        Assert.Equal("EcomAe:Redis", EcomAeRedisScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplacePhpSessionCookies);
        Assert.Equal("ecomae:", options.KeyPrefix);
        Assert.Equal(string.Empty, options.ConnectionString);
    }
}
