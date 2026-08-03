using Microsoft.EntityFrameworkCore;

namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core 10 scaffold for Enterprise BOS readiness.
/// Not registered in <c>Program.cs</c> and must not connect to production until
/// repository/domain cutover is approved. Legacy digests continue to use MySqlConnector.
/// Bounded-context stubs: Catalog, TenantRegistry, Identity.
/// </summary>
public sealed class EcomAeScaffoldDbContext : DbContext
{
    public EcomAeScaffoldDbContext(DbContextOptions<EcomAeScaffoldDbContext> options)
        : base(options)
    {
    }

    public DbSet<CatalogBrandStub> CatalogBrands => Set<CatalogBrandStub>();

    public DbSet<CatalogProductStub> CatalogProducts => Set<CatalogProductStub>();

    public DbSet<TenantRegistryStub> TenantRegistry => Set<TenantRegistryStub>();

    public DbSet<IdentityAdminStub> IdentityAdmins => Set<IdentityAdminStub>();

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

        modelBuilder.Entity<TenantRegistryStub>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SiteKey).HasMaxLength(64);
            entity.Property(item => item.Host).HasMaxLength(255);
        });

        modelBuilder.Entity<IdentityAdminStub>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(255);
        });

        base.OnModelCreating(modelBuilder);
    }
}
