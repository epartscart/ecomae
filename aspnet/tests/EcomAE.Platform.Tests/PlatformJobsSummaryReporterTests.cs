using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PlatformJobsSummaryReporterTests
{
    [Fact]
    public async Task BuildReturnsMigrationPlaceholderWhenDbUnavailable()
    {
        var reporter = new PlatformJobsSummaryReporter(new UnconfiguredFactory());
        var summary = await reporter.BuildAsync(25);
        Assert.Equal("migration", summary.Source);
        Assert.Contains("not configured", summary.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, summary.Total);
    }

    [Fact]
    public void LegacySqlIsSelectOnly()
    {
        Assert.Equal("epc_platform_jobs", LegacyPlatformJobsSql.SourceTable);
        Assert.StartsWith("SELECT", LegacyPlatformJobsSql.CountByStatus.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyPlatformJobsSql.Recent.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyPlatformJobsSql.CountByType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyPlatformJobsSql.Recent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyPlatformJobsSql.Recent, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(EcomAE.Platform.Services.TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
