using EcomAE.Platform.Observability;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeActivitySourcesTests
{
    [Fact]
    public void ActivitySourceNamesAreStableForEnterpriseBosScaffolding()
    {
        Assert.Equal("EcomAE.Platform", EcomAeActivitySources.PlatformName);
        Assert.Equal("EcomAE.Platform.Auth", EcomAeActivitySources.AuthName);
        Assert.Equal("EcomAE.Platform.Surfaces", EcomAeActivitySources.SurfacesName);
        Assert.Equal("EcomAE.Platform.Data", EcomAeActivitySources.DataName);
        Assert.Equal("EcomAE.Workers", EcomAeActivitySources.WorkersName);
        Assert.Equal(EcomAeActivitySources.PlatformName, EcomAeActivitySources.Platform.Name);
        Assert.Equal(EcomAeActivitySources.DataName, EcomAeActivitySources.Data.Name);
    }
}
