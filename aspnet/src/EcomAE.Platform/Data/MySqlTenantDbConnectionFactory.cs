using System.Data.Common;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace EcomAE.Platform.Data;

public sealed class MySqlTenantDbConnectionFactory : ITenantDbConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly IOptions<EcomAeOptions> _options;
    private readonly IOptions<PriceLookupOptions> _priceLookupOptions;
    private readonly IOptions<TenantDbPoolOptions> _poolOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MySqlTenantDbConnectionFactory(
        IConfiguration configuration,
        IOptions<EcomAeOptions> options,
        IOptions<PriceLookupOptions> priceLookupOptions,
        IOptions<TenantDbPoolOptions> poolOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _options = options;
        _priceLookupOptions = priceLookupOptions;
        _poolOptions = poolOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveBaseConnectionString());

    public Task<DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
        => OpenAsync(databaseName, userName: null, password: null, cancellationToken);

    public async Task<DbConnection> OpenAsync(
        string? databaseName,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveBaseConnectionString()
            ?? throw new InvalidOperationException("MySQL connection string is not configured for tenant/price lookup reads.");

        var builder = new MySqlConnectionStringBuilder(connectionString);
        ApplyPoolOptions(builder);

        var tenant = CurrentTenant();
        var db = !string.IsNullOrWhiteSpace(databaseName)
            ? databaseName
            : !string.IsNullOrWhiteSpace(tenant?.DatabaseName)
                ? tenant.DatabaseName
                : null;

        if (!string.IsNullOrWhiteSpace(db))
        {
            builder.Database = db;
        }

        var user = !string.IsNullOrWhiteSpace(userName) ? userName : tenant?.DbUser;
        var pass = password ?? tenant?.DbPassword;
        if (!string.IsNullOrWhiteSpace(user))
        {
            builder.UserID = user;
            builder.Password = pass ?? string.Empty;
        }

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
    {
        if (tenant is null || !tenant.HasTenantDatabase)
        {
            return OpenAsync(null, cancellationToken);
        }

        return OpenAsync(tenant.DatabaseName, tenant.DbUser, tenant.DbPassword, cancellationToken);
    }

    public async Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveBaseConnectionString()
            ?? throw new InvalidOperationException("MySQL connection string is not configured for tenant/price lookup reads.");

        var builder = new MySqlConnectionStringBuilder(connectionString);
        ApplyPoolOptions(builder);
        // Explicitly do not apply TenantContext — portal registry must stay isolated.
        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private TenantContext? CurrentTenant()
    {
        if (_httpContextAccessor.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant)
        {
            return tenant;
        }

        return null;
    }

    private void ApplyPoolOptions(MySqlConnectionStringBuilder builder)
    {
        var pool = _poolOptions.Value;
        if (pool.MaximumPoolSize > 0)
        {
            builder.MaximumPoolSize = (uint)pool.MaximumPoolSize;
        }

        if (pool.ConnectionTimeoutSeconds > 0)
        {
            builder.ConnectionTimeout = (uint)pool.ConnectionTimeoutSeconds;
        }

        if (pool.DefaultCommandTimeoutSeconds > 0)
        {
            builder.DefaultCommandTimeout = (uint)pool.DefaultCommandTimeoutSeconds;
        }
    }

    private string? ResolveBaseConnectionString()
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
