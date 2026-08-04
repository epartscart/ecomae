using EcomAE.Workers.Observability;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeWorkerActivitySourcesTests
{
    [Fact]
    public void WorkerActivitySourceNameMatchesEnterpriseBosScaffolding()
    {
        Assert.Equal("EcomAE.Workers", EcomAeWorkerActivitySources.WorkersName);
        Assert.Equal(EcomAeWorkerActivitySources.WorkersName, EcomAeWorkerActivitySources.Workers.Name);
    }
}
