using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceDashboardSummaryReporterTests
{
    [Fact]
    public async Task BuildReturnsMigrationPlaceholderWhenDbUnavailable()
    {
        var reporter = new SurfaceDashboardSummaryReporter(new UnconfiguredFactory());
        var cp = await reporter.BuildControlPanelAsync();
        var erp = await reporter.BuildErpAsync();
        var bos = await reporter.BuildBosAsync();

        Assert.Equal("migration", cp.Source);
        Assert.Equal("migration", erp.Source);
        Assert.Equal("migration", bos.Source);
        Assert.Equal(0, cp.Users);
        Assert.Equal(0m, erp.CashPosition);

        var account = await reporter.BuildStorefrontAccountAsync(9);
        Assert.Equal("migration", account.Source);
        Assert.Equal(9, account.UserId);
    }

    [Fact]
    public void LegacySqlIsSelectOnly()
    {
        Assert.StartsWith("SELECT", LegacySurfaceDashboardSql.CountUsers.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacySurfaceDashboardSql.SumBankBalances.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacySurfaceDashboardSql.SumArOutstanding, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySurfaceDashboardSql.CountPortalTenants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySurfaceDashboardSql.SumStockValue, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
