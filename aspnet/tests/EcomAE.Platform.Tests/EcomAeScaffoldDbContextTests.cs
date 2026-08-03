using EcomAE.Platform.Data.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeScaffoldDbContextTests
{
    [Fact]
    public void ScaffoldDbContextModelsBoundedContextStubsWithoutProductionRegistration()
    {
        var options = new DbContextOptionsBuilder<EcomAeScaffoldDbContext>()
            .UseInMemoryDatabase("ecomae-scaffold-test")
            .Options;

        using var context = new EcomAeScaffoldDbContext(options);
        Assert.NotNull(context.Model);
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(CatalogBrandStub));
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(CatalogProductStub));
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(TenantRegistryStub));
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(IdentityAdminStub));
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(ErpCashAccountStub));
        Assert.Contains(context.Model.GetEntityTypes(), type => type.ClrType == typeof(ErpCashEntryStub));
        Assert.NotNull(context.CatalogBrands);
        Assert.NotNull(context.CatalogProducts);
        Assert.NotNull(context.TenantRegistry);
        Assert.NotNull(context.IdentityAdmins);
        Assert.NotNull(context.ErpCashAccounts);
        Assert.NotNull(context.ErpCashEntries);
    }
}
