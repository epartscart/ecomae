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
        var users = await reporter.ListCpUsersAsync(10);
        var groups = await reporter.ListCpGroupsAsync(10);
        var suppliers = await reporter.ListErpSuppliersAsync(10);
        var purchases = await reporter.ListErpPurchasesAsync(10);
        var garage = await reporter.ListStorefrontGarageAsync(9, 10);

        Assert.Equal("migration", cp.Source);
        Assert.Equal("migration", erp.Source);
        Assert.Equal("migration", bos.Source);
        Assert.Equal("migration", tenants.Source);
        Assert.Equal("migration", health.Source);
        Assert.Equal("migration", accounts.Source);
        Assert.Equal("migration", orders.Source);
        Assert.Equal("migration", users.Source);
        Assert.Equal("migration", groups.Source);
        Assert.Equal("migration", suppliers.Source);
        Assert.Equal("migration", purchases.Source);
        Assert.Equal("migration", garage.Source);
        Assert.Equal(0, cp.Users);
        Assert.Equal(0m, erp.CashPosition);
        Assert.Empty(tenants.Tenants);
        Assert.Empty(orders.Orders);
        Assert.Empty(users.Users);
        Assert.Empty(garage.Vehicles);

        var account = await reporter.BuildStorefrontAccountAsync(9);
        Assert.Equal("migration", account.Source);
        Assert.Equal(9, account.UserId);
        Assert.Equal(0, account.GarageVehicles);
    }

    [Fact]
    public void LegacySqlIsSelectOnlyAndUsesErpTables()
    {
        Assert.StartsWith("SELECT", LegacySurfaceDashboardSql.CountUsers.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_cash_bank_accounts", LegacySurfaceDashboardSql.SumCashBankTotal, StringComparison.Ordinal);
        Assert.Contains("epc_portal_tenants", LegacySurfaceDashboardSql.SelectPortalTenants, StringComparison.Ordinal);
        Assert.Contains("shop_orders", LegacySurfaceDashboardSql.SelectCustomerOrders, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_garage", LegacySurfaceDashboardSql.SelectCustomerGarage, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchases", LegacySurfaceDashboardSql.SelectErpPurchases, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySurfaceDashboardSql.SumSupplierCredit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySurfaceDashboardSql.SelectPortalTenants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySurfaceDashboardSql.SelectCustomerOrders, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySurfaceDashboardSql.SelectCpUsers, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
