using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlChartOfAccountsSeederTests
{
    [Fact]
    public void SeedMatchesPhpSystemChart()
    {
        var codes = ErpGlChartOfAccountsSeeder.SystemAccounts.Select(a => a.Code).ToArray();
        Assert.Equal(["1000", "1010", "1100", "1150", "2000", "2100", "3000", "3100", "4000", "5000", "6100"], codes);
        Assert.All(ErpGlChartOfAccountsSeeder.SystemAccounts, a => Assert.Contains(a.Type, new[] { "asset", "liability", "equity", "revenue", "expense" }));
        Assert.Equal("Accounts receivable", ErpGlChartOfAccountsSeeder.SystemAccounts.Single(a => a.Code == "1100").Name);
        Assert.Equal("General expenses", ErpGlChartOfAccountsSeeder.SystemAccounts.Single(a => a.Code == "6100").Name);
    }

    [Fact]
    public async Task EnsureRejectsNullConnection()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => ErpGlChartOfAccountsSeeder.EnsureAsync(null!, CancellationToken.None));
}
