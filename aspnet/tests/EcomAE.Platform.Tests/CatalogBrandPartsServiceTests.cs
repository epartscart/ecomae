using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CatalogBrandPartsServiceTests
{
    [Fact]
    public async Task ListRequiresBrand()
    {
        var service = new CatalogBrandPartsService(new StaticRepo());
        var result = await service.ListAsync(" ", 100, 0);
        Assert.False(result.Ok);
        Assert.Contains("Brand", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListReturnsMappedRows()
    {
        var service = new CatalogBrandPartsService(new StaticRepo(
            total: 2,
            page:
            [
                new CatalogBrandPartRow("BOSCH", "0986424590", "0986424590", "Pad", 4, 12.5m, "1", "WH1")
            ]));

        var result = await service.ListAsync("bosch", 100, 0);
        Assert.True(result.Ok);
        Assert.Equal(2, result.Rows);
        Assert.Equal("database", result.Source);
        Assert.Single(result.Data);
    }

    [Fact]
    public void LegacySqlIsSelectOnly()
    {
        Assert.Equal("shop_docpart_prices_data", LegacyCatalogBrandPartsSql.SourceTable);
        Assert.StartsWith("SELECT", LegacyCatalogBrandPartsSql.CountDistinctArticles.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyCatalogBrandPartsSql.SelectPage.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCatalogBrandPartsSql.SelectPage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticRepo : ICatalogBrandPartsRepository
    {
        private readonly int _total;
        private readonly IReadOnlyList<CatalogBrandPartRow> _page;

        public StaticRepo(int total = 0, IReadOnlyList<CatalogBrandPartRow>? page = null)
        {
            _total = total;
            _page = page ?? [];
        }

        public Task<(int TotalRows, IReadOnlyList<CatalogBrandPartRow> Page)> FindByBrandAsync(
            string brandUpper,
            string brandCompact,
            int limit,
            int offset,
            CancellationToken cancellationToken = default)
            => Task.FromResult((_total, _page));
    }
}
