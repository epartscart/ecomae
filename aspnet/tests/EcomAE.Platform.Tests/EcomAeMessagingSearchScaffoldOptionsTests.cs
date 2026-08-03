using EcomAE.Platform.Messaging;
using EcomAE.Platform.Observability;
using EcomAE.Platform.Search;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeMessagingSearchScaffoldOptionsTests
{
    [Fact]
    public void KafkaScaffoldOptionsDefaultToDisabledAndDoNotAllowPublish()
    {
        var options = new EcomAeKafkaScaffoldOptions();
        Assert.Equal("EcomAe:Kafka", EcomAeKafkaScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.AllowPublish);
        Assert.Equal("ecomae-platform", options.ClientId);
    }

    [Fact]
    public void OpenSearchScaffoldOptionsDefaultToDisabledAndDoNotReplacePhpSearch()
    {
        var options = new EcomAeOpenSearchScaffoldOptions();
        Assert.Equal("EcomAe:OpenSearch", EcomAeOpenSearchScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.ReplacePhpSearch);
        Assert.Equal("ecomae", options.DefaultIndex);
    }

    [Fact]
    public void SerilogScaffoldOptionsDefaultToDisabledAndDoNotRegisterExporters()
    {
        var options = new EcomAeSerilogScaffoldOptions();
        Assert.Equal("EcomAe:Serilog", EcomAeSerilogScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.RegisterExporters);
        Assert.Equal("Information", options.MinimumLevel);
    }
}
