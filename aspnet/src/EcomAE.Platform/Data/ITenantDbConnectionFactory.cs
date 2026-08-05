using System.Data.Common;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Data;

public interface ITenantDbConnectionFactory
{
    bool IsConfigured { get; }

    /// <summary>
    /// Opens a MySQL connection. When <paramref name="databaseName"/> is null, uses request
    /// <see cref="TenantContext"/> database/credentials when present; otherwise the registry default.
    /// </summary>
    Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default);

    /// <summary>Opens with explicit credentials (dedicated tenant DB). Password is never logged.</summary>
    Task<DbConnection> OpenAsync(
        string? databaseName,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default);

    /// <summary>Opens using tenant context database + credentials when available.</summary>
    Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the platform/portal registry connection (base connection string).
    /// Never applies request <see cref="TenantContext"/> DB or credentials.
    /// </summary>
    Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default);
}
