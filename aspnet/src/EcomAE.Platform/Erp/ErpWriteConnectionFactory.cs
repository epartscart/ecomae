using System.Data.Common;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Opens the tenant shop schema ERP writes target — same resolution the read digests use
/// (PHP <c>epc_portal_resolve_tenant_db</c>): ePartsCart hosts share Model C <c>docpart</c>,
/// every other tenant uses its request-bound database.
/// </summary>
public interface IErpWriteConnectionFactory
{
    bool IsConfigured { get; }

    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpWriteConnectionFactory : IErpWriteConnectionFactory
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ErpWriteConnectionFactory(ITenantDbConnectionFactory connections, IHttpContextAccessor? httpContextAccessor = null)
    {
        _connections = connections;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsConfigured => _connections.IsConfigured;

    public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        => IsEpartsCartRequest()
            ? _connections.OpenAsync("docpart", cancellationToken)
            : _connections.OpenAsync(null, cancellationToken);

    private bool IsEpartsCartRequest()
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (tenant is not null && RouteTenantResolver.IsEpartsCartHost(tenant.Host, tenant.SiteKey))
        {
            return true;
        }

        var host = _httpContextAccessor?.HttpContext?.Request.Host.Host ?? string.Empty;
        return RouteTenantResolver.IsEpartsCartHost(host, siteKey: null);
    }
}
