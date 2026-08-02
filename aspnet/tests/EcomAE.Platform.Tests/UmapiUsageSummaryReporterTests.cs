using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class UmapiUsageSummaryReporterTests
{
    [Fact]
    public async Task BuildReturnsMigrationPlaceholderWhenDbUnavailable()
    {
        var reporter = new UmapiUsageSummaryReporter(new UnconfiguredFactory(), new ConfigurationBuilder().Build());
        var summary = await reporter.BuildAsync(7);
        Assert.Equal("migration", summary.Source);
        Assert.Equal(1000, summary.DailyLimit);
        Assert.Contains("not configured", summary.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacySqlIsSelectOnly()
    {
        Assert.Equal("epc_umapi_usage_log", LegacyUmapiUsageSql.SourceTable);
        Assert.StartsWith("SELECT", LegacyUmapiUsageSql.CountTodayLive.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyUmapiUsageSql.ByActionToday, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyUmapiUsageSql.History, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
