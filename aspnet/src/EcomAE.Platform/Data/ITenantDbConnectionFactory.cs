using System.Data.Common;

namespace EcomAE.Platform.Data;

public interface ITenantDbConnectionFactory
{
    bool IsConfigured { get; }

    Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default);
}
