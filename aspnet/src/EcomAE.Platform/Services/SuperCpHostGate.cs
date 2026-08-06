namespace EcomAE.Platform.Services;

/// <summary>
/// Shared Super-host gate for Super-only CP Blazor apps (tenants, demo tenants,
/// tax toolkits, free-tools hub, governance, failover). Same rule as
/// <see cref="PlatformHostPolicy.IsSuperCpHost"/> / ErpFleetApp.
/// </summary>
public static class SuperCpHostGate
{
    public static bool IsAllowed(string? host) => PlatformHostPolicy.AllowSuperOnlyApp(host);

    public static bool IsAllowed(HttpContext? httpContext)
        => IsAllowed(httpContext?.Request.Host.Host);
}
