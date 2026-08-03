using EcomAE.Platform.Hosting.Scaffolding;
using EcomAE.Platform.Integrations.Scaffolding;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeGitOpsAotAiScaffoldOptionsTests
{
    [Fact]
    public void NativeAotScaffoldOptionsDefaultOffForPlatformHost()
    {
        var options = new EcomAeNativeAotScaffoldOptions();
        Assert.Equal("EcomAe:NativeAot", EcomAeNativeAotScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.RequireForPlatformHost);
        Assert.True(options.AllowIsolatedServiceEvaluation);
    }

    [Fact]
    public void AiSidecarScaffoldOptionsDefaultToDisabledAndNoBusinessWrites()
    {
        var options = new EcomAeAiSidecarScaffoldOptions();
        Assert.Equal("EcomAe:AiSidecar", EcomAeAiSidecarScaffoldOptions.SectionName);
        Assert.False(options.Enabled);
        Assert.False(options.AllowBusinessWrites);
        Assert.Equal("http://127.0.0.1:8100", options.BaseUrl);
    }
}
