using System.Text;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroDowntimeMigrationTrancheTests
{
    [Fact]
    public void DeploymentReadinessCanRequireReleaseIdentity()
    {
        var waitScript = ReadRepoFile("scripts/wait_for_aspnet_health.sh");
        var deployScript = ReadRepoFile("scripts/deploy_aspnet_foundation.sh");

        Assert.Contains("ECOMAE_EXPECTED_RELEASE_SHA", waitScript, StringComparison.Ordinal);
        Assert.Contains("/migration/release", waitScript, StringComparison.Ordinal);
        Assert.Contains("commitSha", waitScript, StringComparison.Ordinal);
        Assert.Contains("RELEASE_SHA", deployScript, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaMigrationPlanIsExpandContractAndDisabledByDefault()
    {
        var options = ReadRepoFile("aspnet/src/EcomAE.Platform/Configuration/SchemaMigrationOptions.cs");
        var runner = ReadRepoFile("aspnet/src/EcomAE.Platform/Migration/ExpandContractMigrationRunner.cs");
        var settings = ReadRepoFile("aspnet/src/EcomAE.Platform/appsettings.json");

        Assert.Contains("ApplyOnStartup", options, StringComparison.Ordinal);
        Assert.Contains("\"expand\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"contract\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"ApplyOnStartup\": false", settings, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS", runner, StringComparison.Ordinal);
        Assert.Contains("lock_wait_timeout", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void DataProtectionAndConnectionRetryAreConfiguredForSideBySideReleases()
    {
        var program = ReadRepoFile("aspnet/src/EcomAE.Platform/Program.cs");
        var factory = ReadRepoFile("aspnet/src/EcomAE.Platform/Data/MySqlTenantDbConnectionFactory.cs");
        var settings = ReadRepoFile("aspnet/src/EcomAE.Platform/appsettings.json");

        Assert.Contains("PersistKeysToFileSystem", program, StringComparison.Ordinal);
        Assert.Contains("OpenWithRetryAsync", factory, StringComparison.Ordinal);
        Assert.Contains("\"OpenRetryAttempts\": 3", settings, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            var parent = Directory.GetParent(directory)?.FullName;
            Assert.NotNull(parent);
            directory = parent!;
        }

        return File.ReadAllText(Path.Combine(directory, relativePath), Encoding.UTF8);
    }
}
