using EcomAE.Platform.Data.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomAeScaffoldDbContextTests
{
    [Fact]
    public void ScaffoldDbContextCanBeConstructedWithoutProductionRegistration()
    {
        var options = new DbContextOptionsBuilder<EcomAeScaffoldDbContext>()
            .UseInMemoryDatabase("ecomae-scaffold-test")
            .Options;

        using var context = new EcomAeScaffoldDbContext(options);
        Assert.NotNull(context.Model);
        Assert.Empty(context.Model.GetEntityTypes());
    }
}
