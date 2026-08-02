using Microsoft.EntityFrameworkCore;

namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core 10 scaffold for Enterprise BOS readiness.
/// Not registered in <c>Program.cs</c> and must not connect to production until
/// repository/domain cutover is approved. Legacy digests continue to use MySqlConnector.
/// </summary>
public sealed class EcomAeScaffoldDbContext : DbContext
{
    public EcomAeScaffoldDbContext(DbContextOptions<EcomAeScaffoldDbContext> options)
        : base(options)
    {
    }

    public DbSet<CatalogBrandStub> CatalogBrands => Set<CatalogBrandStub>();

    public DbSet<CatalogProductStub> CatalogProducts => Set<CatalogProductStub>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogBrandStub>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<CatalogProductStub>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Article).HasMaxLength(64);
            entity.Property(item => item.Brand).HasMaxLength(128);
        });

        base.OnModelCreating(modelBuilder);
    }
}
