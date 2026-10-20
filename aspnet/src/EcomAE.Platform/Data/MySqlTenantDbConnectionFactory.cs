using System.Data.Common;
using System.Net.Sockets;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
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
        ApplyFirstPaintTimeouts(builder);
        KeepLoginConnection(builder);

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

        // Credentials policy (PHP Model C parity):
        // - Explicit userName → use it (dedicated tenant DB via OpenForTenantAsync).
        // - Implicit open (databaseName null) → TenantContext db_user/password when present.
        // - Explicit databaseName without userName → base connection-string credentials only.
        //   Critical for ePartsCart shared `docpart`: portal epc_portal_tenants.db_user must NOT
        //   override registry credentials (wrong/empty grants → every CP/ERP digest empty).
        if (!string.IsNullOrWhiteSpace(userName))
        {
            builder.UserID = userName;
            builder.Password = password ?? string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(databaseName)
            && tenant is not null
            && tenant.HasUsableDedicatedCredentials)
        {
            builder.UserID = tenant.DbUser;
            builder.Password = (password ?? tenant.DbPassword) ?? string.Empty;
        }

        return await OpenWithRetryAsync(builder.ConnectionString, cancellationToken).ConfigureAwait(false);
    }

    public Task<DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
    {
        if (tenant is null || !tenant.HasTenantDatabase)
        {
            return OpenAsync(null, cancellationToken);
        }

        if (tenant.HasUsableDedicatedCredentials)
        {
            return OpenAsync(tenant.DatabaseName, tenant.DbUser, tenant.DbPassword, cancellationToken);
        }

        // Empty dedicated password (ERP-only onboard) — registry CS + DatabaseName only.
        return OpenAsync(tenant.DatabaseName, userName: null, password: null, cancellationToken);
    }

    public async Task<DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveBaseConnectionString()
            ?? throw new InvalidOperationException("MySQL connection string is not configured for tenant/price lookup reads.");

        var builder = new MySqlConnectionStringBuilder(connectionString);
        ApplyPoolOptions(builder);
        ApplyFirstPaintTimeouts(builder);
        KeepLoginConnection(builder);
        // Explicitly do not apply TenantContext — portal registry must stay isolated.
        return await OpenWithRetryAsync(builder.ConnectionString, cancellationToken).ConfigureAwait(false);
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

        // Clamp to MaximumPoolSize — an oversized minimum makes MySqlConnector reject
        // every open (login included), and warm pools multiply per pool variant, so a
        // runaway min can exhaust max_connections. Always assigning also lets 0
        // disable a positive minimum inherited from the base connection string.
        var minPool = Math.Clamp(pool.MinimumPoolSize, 0, pool.MaximumPoolSize);
        builder.MinimumPoolSize = (uint)minPool;

        if (pool.ConnectionTimeoutSeconds > 0)
        {
            builder.ConnectionTimeout = (uint)pool.ConnectionTimeoutSeconds;
        }

        if (pool.DefaultCommandTimeoutSeconds > 0)
        {
            builder.DefaultCommandTimeout = (uint)pool.DefaultCommandTimeoutSeconds;
        }
    }

    private async Task<DbConnection> OpenWithRetryAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var pool = _poolOptions.Value;
        var attempts = ErpFirstPaint.IsLoginPath(_httpContextAccessor.HttpContext?.Request.Path ?? default)
            ? 1
            : Math.Clamp(pool.OpenRetryAttempts, 1, 5);
        var delay = Math.Clamp(pool.OpenRetryDelayMilliseconds, 25, 2_000);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch (Exception exception) when (attempt < attempts && IsTransientOpenFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientOpenFailure(Exception exception)
        => exception is MySqlException
            || exception is TimeoutException
            || exception is SocketException
            || exception is IOException;

    /// <summary>
    /// First paint must not inherit the 30s pool default or 8s connect wait.
    /// Cap both to the remaining 3s wall clock so sequential opens cannot 524.
    /// </summary>
    private static void ApplyFirstPaintTimeouts(MySqlConnectionStringBuilder builder)
    {
        if (!ErpFirstPaint.IsActive)
        {
            return;
        }

        var remain = (uint)Math.Max(1, ErpFirstPaint.RemainingCommandTimeoutSeconds);
        if (builder.ConnectionTimeout == 0 || builder.ConnectionTimeout > remain)
        {
            builder.ConnectionTimeout = remain;
        }

        builder.DefaultCommandTimeout = remain;
    }

    /// <summary>
    /// Login opens a direct connection with an 8s wait. The catalog pool of 32
    /// and the 1s first-paint cap stay on every other page.
    /// </summary>
    private void KeepLoginConnection(MySqlConnectionStringBuilder builder)
    {
        if (!ErpFirstPaint.IsLoginPath(_httpContextAccessor.HttpContext?.Request.Path ?? default))
        {
            return;
        }

        // A separate pool still deadlocks when every slot is checked out.
        // Login opens a direct connection. The catalog keeps its pool of 32.
        builder.Pooling = false;
        builder.ConnectionTimeout = 8;
        if (builder.DefaultCommandTimeout < 8)
        {
            builder.DefaultCommandTimeout = 8;
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
