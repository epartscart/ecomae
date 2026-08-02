using Microsoft.EntityFrameworkCore;

namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// Empty EF Core 10 scaffold for Enterprise BOS readiness.
/// Not registered in <c>Program.cs</c> and must not connect to production until
/// repository/domain cutover is approved. Legacy digests continue to use MySqlConnector.
/// </summary>
public sealed class EcomAeScaffoldDbContext : DbContext
{
    public EcomAeScaffoldDbContext(DbContextOptions<EcomAeScaffoldDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Intentionally empty: bounded contexts (Catalog, Identity, ERP, TenantRegistry)
        // will be modeled here after Zero-PHP parity evidence for each surface.
        base.OnModelCreating(modelBuilder);
    }
}
