using EcomAE.Platform.Messaging;
using EcomAE.Platform.Resilience;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeRabbitMqPollyScaffoldOptionsTests
{
    [Fact]
    public void RabbitMqScaffoldOptionsDefaultToDisabledAndDoNotAllowPublish()
    {
        var options = new EcomAeRabbitMqScaffoldOptions();
        Assert.Equal("EcomAe:RabbitMq", EcomAeRabbitMqScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.AllowPublish);
        Assert.Equal(5672, options.Port);
    }

    [Fact]
    public void PollyScaffoldOptionsDefaultToDisabledAndDoNotRegisterPipelines()
    {
        var options = new EcomAePollyScaffoldOptions();
        Assert.Equal("EcomAe:Polly", EcomAePollyScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.RegisterPipelines);
        Assert.Equal(10_000, options.TimeoutMilliseconds);
        Assert.Equal(2, options.RetryCount);
    }
}
