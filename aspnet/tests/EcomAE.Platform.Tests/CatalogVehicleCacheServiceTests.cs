using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CatalogVehicleCacheServiceTests
{
    [Fact]
    public async Task GetModelsRequiresMfaId()
    {
        var service = new CatalogVehicleCacheService(new StaticRepo());
        var result = await service.GetModelsAsync("passenger", 0);
        Assert.False(result.Ok);
        Assert.Contains("mfa_id", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetModelsReturnsMappedRows()
    {
        var service = new CatalogVehicleCacheService(new StaticRepo(
            models:
            [
                new CatalogModelRow("passenger", 10, 100, "Corolla", "2010", "2020", null, 1)
            ]));

        var result = await service.GetModelsAsync("passenger", 10);
        Assert.True(result.Ok);
        Assert.Equal(1, result.Rows);
        Assert.Equal("database", result.Source);
    }

    [Fact]
    public async Task GetModificationsRequiresMsId()
    {
        var service = new CatalogVehicleCacheService(new StaticRepo());
        var result = await service.GetModificationsAsync("passenger", 0);
        Assert.False(result.Ok);
        Assert.Contains("ms_id", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBrandsReturnsRows()
    {
        var service = new CatalogVehicleCacheService(new StaticRepo(
            brands: [new CatalogBrandRow(1, "BOSCH", "Robert Bosch", null, 1)]));

        var result = await service.GetBrandsAsync();
        Assert.True(result.Ok);
        Assert.Equal(1, result.Rows);
    }

    [Fact]
    public void LegacySqlContractsAreSelectOnly()
    {
        Assert.StartsWith("SELECT", LegacyCatalogModelsSql.SelectBySectionAndMfa.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyCatalogModificationsSql.SelectBySectionAndMs.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyCatalogBrandsSql.SelectAll.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticRepo : ICatalogVehicleCacheRepository
    {
        private readonly IReadOnlyList<CatalogModelRow> _models;
        private readonly IReadOnlyList<CatalogModificationRow> _mods;
        private readonly IReadOnlyList<CatalogBrandRow> _brands;

        public StaticRepo(
            IReadOnlyList<CatalogModelRow>? models = null,
            IReadOnlyList<CatalogModificationRow>? mods = null,
            IReadOnlyList<CatalogBrandRow>? brands = null)
        {
            _models = models ?? [];
            _mods = mods ?? [];
            _brands = brands ?? [];
        }

        public Task<IReadOnlyList<CatalogModelRow>> FindModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default)
            => Task.FromResult(_models);

        public Task<IReadOnlyList<CatalogModificationRow>> FindModificationsAsync(string section, int msId, CancellationToken cancellationToken = default)
            => Task.FromResult(_mods);

        public Task<IReadOnlyList<CatalogBrandRow>> FindBrandsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_brands);
    }
}
