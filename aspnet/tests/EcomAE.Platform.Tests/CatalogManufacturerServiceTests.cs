using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CatalogManufacturerServiceTests
{
    [Fact]
    public async Task GetBySectionDefaultsPassengerAndMapsFallbackFields()
    {
        var service = new CatalogManufacturerService(new StaticRepo([
            new CatalogManufacturerRow("passenger", 10, "TOYOTA", null, "PC", "JP", true, false, null, 1)
        ]));

        var result = await service.GetBySectionAsync(" ");

        Assert.True(result.Ok);
        Assert.Equal("passenger", result.Section);
        Assert.Equal(1, result.Rows);
        Assert.Equal("database", result.Source);
    }

    [Fact]
    public async Task GetBySectionReturnsEmptyMessageWhenNoRows()
    {
        var service = new CatalogManufacturerService(new StaticRepo([]));

        var result = await service.GetBySectionAsync("cv");

        Assert.Equal(0, result.Rows);
        Assert.Equal("database-empty", result.Source);
        Assert.Contains("No cached manufacturers", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacySqlIsReadOnlyAgainstManufacturersTable()
    {
        Assert.Equal("epc_umapi_manufacturers", LegacyCatalogManufacturersSql.SourceTable);
        Assert.Contains("ORDER BY `manufacturer` ASC", LegacyCatalogManufacturersSql.SelectBySection, StringComparison.Ordinal);
        Assert.StartsWith("SELECT", LegacyCatalogManufacturersSql.SelectBySection.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" INSERT ", $" {LegacyCatalogManufacturersSql.SelectBySection} ", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" UPDATE ", $" {LegacyCatalogManufacturersSql.SelectBySection} ", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" DELETE ", $" {LegacyCatalogManufacturersSql.SelectBySection} ", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticRepo : ICatalogManufacturerRepository
    {
        private readonly IReadOnlyList<CatalogManufacturerRow> _rows;

        public StaticRepo(IReadOnlyList<CatalogManufacturerRow> rows) => _rows = rows;

        public Task<IReadOnlyList<CatalogManufacturerRow>> FindBySectionAsync(string section, CancellationToken cancellationToken = default)
            => Task.FromResult(_rows);
    }
}
