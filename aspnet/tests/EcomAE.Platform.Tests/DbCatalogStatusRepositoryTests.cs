using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Data;
using System.Data.Common;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class DbCatalogStatusRepositoryTests
{
    [Fact]
    public async Task GetStatusFallsBackWhenConnectionFactoryNotConfigured()
    {
        var repository = new DbCatalogStatusRepository(new UnconfiguredFactory());

        var payload = await repository.GetStatusAsync();

        Assert.False(payload.Connected);
        Assert.Equal("migration-placeholder", payload.Source);
        Assert.Contains(payload.ActionRequired, item => item.Contains("TenantRegistry", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacySqlTargetsUmapiStatusTablesReadOnly()
    {
        Assert.Equal("epc_umapi_sync_status", LegacyCatalogStatusSql.SyncStatusTable);
        Assert.Contains("epc_umapi_manufacturers", LegacyCatalogStatusSql.CountManufacturers, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacyCatalogStatusSql.SelectSyncStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyCatalogStatusSql.SelectSyncStatus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacyCatalogStatusSql.SelectSyncStatus, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("not configured");
        }

        public Task<DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<DbConnection> OpenForTenantAsync(EcomAE.Platform.Services.TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
