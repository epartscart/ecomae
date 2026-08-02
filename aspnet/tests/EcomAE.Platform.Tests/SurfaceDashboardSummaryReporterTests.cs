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
        var tenants = await reporter.ListPortalTenantsAsync(10);
        var health = await reporter.BuildBosFleetHealthAsync(5);
        var accounts = await reporter.BuildErpAccountsAsync();
        var orders = await reporter.ListStorefrontOrdersAsync(9, 10);

        Assert.Equal("migration", cp.Source);
        Assert.Equal("migration", erp.Source);
        Assert.Equal("migration", bos.Source);
        Assert.Equal("migration", tenants.Source);
        Assert.Equal("migration", health.Source);
        Assert.Equal("migration", accounts.Source);
        Assert.Equal("migration", orders.Source);
        Assert.Equal(0, cp.Users);
        Assert.Equal(0m, erp.CashPosition);
        Assert.Empty(tenants.Tenants);
        Assert.Empty(orders.Orders);

        var account = await reporter.BuildStorefrontAccountAsync(9);
        Assert.Equal("migration", account.Source);
        Assert.Equal(9, account.UserId);
    }

    [Fact]
    public void LegacySqlIsSelectOnlyAndUsesErpTables()
    {
        Assert.StartsWith("SELECT", LegacySurfaceDashboardSql.CountUsers.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_cash_bank_accounts", LegacySurfaceDashboardSql.SumCashBankTotal, StringComparison.Ordinal);
        Assert.Contains("epc_portal_tenants", LegacySurfaceDashboardSql.SelectPortalTenants, StringComparison.Ordinal);
        Assert.Contains("shop_orders", LegacySurfaceDashboardSql.SelectCustomerOrders, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySurfaceDashboardSql.SumSupplierCredit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySurfaceDashboardSql.SelectPortalTenants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySurfaceDashboardSql.SelectCustomerOrders, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
