using System.Data.Common;
using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class DbPriceOfferRepositoryTests
{
    [Fact]
    public async Task FindOffersReturnsEmptyWhenConnectionFactoryIsNotConfigured()
    {
        var repository = new DbPriceOfferRepository(
            new FakeTenantDbConnectionFactory(configured: false),
            new HttpContextAccessor(),
            Options.Create(new PriceLookupOptions()));

        var rows = await repository.FindOffersAsync("TOYOTA", "044650K020");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task FindOffersUsesTenantDatabaseNameFromHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] = TenantContext.ForKnownTenant(
            siteKey: "platform",
            host: "www.ecomae.com",
            mode: TenantMode.Platform,
            surface: TenantSurface.Api,
            path: "/",
            databaseName: "tenant_prices_db");
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        string? openedDatabase = null;
        var factory = new FakeTenantDbConnectionFactory(configured: true)
        {
            OpenHandler = databaseName =>
            {
                openedDatabase = databaseName;
                throw new InvalidOperationException("stop-after-database-resolution");
            }
        };
        var repository = new DbPriceOfferRepository(
            factory,
            accessor,
            Options.Create(new PriceLookupOptions { DatabaseName = "fallback_db" }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.FindOffersAsync("TOYOTA", "044650K020"));

        Assert.Equal("stop-after-database-resolution", exception.Message);
        Assert.Equal("tenant_prices_db", openedDatabase);
    }

    [Fact]
    public void MapReaderMatchesPhpDefaultsForSupplierAndNullableFields()
    {
        var row = PriceOfferRowMapper.FromLegacyPriceLookup(
            "TOYOTA",
            "04465-0K020",
            "Brake Pad Set",
            120.50m,
            8,
            null,
            "same day");

        Assert.Equal("default", row.Supplier);
        Assert.Equal("TOYOTA", row.Brand);
        Assert.Equal("04465-0K020", row.Article);
        Assert.Equal("Brake Pad Set", row.Name);
        Assert.Equal(120.50m, row.Price);
        Assert.Equal(8, row.StockHint);
        Assert.Equal("same day", row.LeadTime);
    }

    [Fact]
    public void LegacySqlContractRemainsReadOnlyAgainstPricesTable()
    {
        Assert.Equal("shop_docpart_prices_data", LegacyPriceLookupSql.SourceTable);
        Assert.Equal(25, LegacyPriceLookupSql.DefaultLimit);
        Assert.Contains("ORDER BY `price` ASC", LegacyPriceLookupSql.LookupOffers, StringComparison.Ordinal);
        Assert.Contains("LIMIT 25", LegacyPriceLookupSql.LookupOffers, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacyPriceLookupSql.LookupOffers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyPriceLookupSql.LookupOffers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyPriceLookupSql.LookupOffers, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeTenantDbConnectionFactory : ITenantDbConnectionFactory
    {
        public FakeTenantDbConnectionFactory(bool configured)
        {
            IsConfigured = configured;
        }

        public bool IsConfigured { get; }

        public Func<string?, DbConnection>? OpenHandler { get; init; }

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
        {
            if (OpenHandler is null)
            {
                throw new InvalidOperationException("OpenHandler was not configured for this test.");
            }

            return Task.FromResult(OpenHandler(databaseName));
        }

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => OpenAsync(databaseName, cancellationToken);

        public Task<DbConnection> OpenForTenantAsync(EcomAE.Platform.Services.TenantContext? tenant, CancellationToken cancellationToken = default)
            => OpenAsync(tenant?.DatabaseName, cancellationToken);

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => OpenAsync(null, cancellationToken);
    }
}
