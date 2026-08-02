using Xunit;
using EcomAE.Workers;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchOneWorkerDryRunEvidenceManifestTests
{
    [Fact]
    public void BuildManifestCreatesEvidencePlaceholderForEveryBatchOneWorker()
    {
        var manifest = new ZeroPhpBatchOneWorkerDryRunEvidenceManifest(new ZeroPhpBatchOneWorkerReplacementCatalog());

        var evidence = manifest.BuildManifest();

        Assert.Equal(50, evidence.Count);
        Assert.All(evidence, item => Assert.Equal("parity-sample-required", item.Status));
        Assert.All(evidence, item => Assert.Contains("--dry-run", item.DryRunCommand, StringComparison.OrdinalIgnoreCase));
        Assert.All(evidence, item => Assert.StartsWith("docs/migration/parity/batch-001/", item.ParitySamplePath, StringComparison.Ordinal));
        Assert.All(evidence, item => Assert.Contains("--keep-php-fallback", item.RollbackCommand, StringComparison.OrdinalIgnoreCase));
        Assert.All(evidence, item => Assert.True(item.ExactRouteOnly));
        Assert.All(evidence, item => Assert.True(item.PhpFallbackRequired));
    }
}
