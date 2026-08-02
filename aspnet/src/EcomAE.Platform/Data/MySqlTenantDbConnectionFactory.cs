using System.Data.Common;
using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace EcomAE.Platform.Data;

public sealed class MySqlTenantDbConnectionFactory : ITenantDbConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<EcomAeOptions> _options;
    private readonly IOptions<PriceLookupOptions> _priceLookupOptions;

    public MySqlTenantDbConnectionFactory(
        IConfiguration configuration,
        IOptions<EcomAeOptions> options,
        IOptions<PriceLookupOptions> priceLookupOptions)
    {
        _configuration = configuration;
        _options = options;
        _priceLookupOptions = priceLookupOptions;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveConnectionString());

    public async Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveConnectionString()
            ?? throw new InvalidOperationException("MySQL connection string is not configured for tenant/price lookup reads.");

        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            builder.Database = databaseName;
        }

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private string? ResolveConnectionString()
    {
        var configuredName = _priceLookupOptions.Value.ConnectionStringName;
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            var named = _configuration.GetConnectionString(configuredName);
            if (!string.IsNullOrWhiteSpace(named))
            {
                return named;
            }
        }

        var registryName = _options.Value.TenantRegistryConnectionStringName;
        return string.IsNullOrWhiteSpace(registryName)
            ? null
            : _configuration.GetConnectionString(registryName);
    }
}
