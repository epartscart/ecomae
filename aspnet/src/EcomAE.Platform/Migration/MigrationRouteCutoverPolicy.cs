using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Migration;

public sealed class MigrationRouteCutoverPolicy : IMigrationRouteCutoverPolicy
{
    private readonly MigrationRouteCutoverOptions _options;

    public MigrationRouteCutoverPolicy()
        : this(Options.Create(new MigrationRouteCutoverOptions()))
    {
    }

    public MigrationRouteCutoverPolicy(IOptions<MigrationRouteCutoverOptions> options)
    {
        _options = options.Value;
    }

    public MigrationRouteCutoverDecision Decide(TenantContext tenant)
    {
        return tenant.Surface switch
        {
            TenantSurface.Api => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                ApiTargetRuntime(),
                "API shadow traffic on ASP.NET; PHP kept as reference for parity samples until dual-sample + approval.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.ApiShadowTrafficEnabled),
            TenantSurface.Storefront => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                StorefrontTargetRuntime(),
                "Storefront product URLs are ASP.NET-primary for all tenants; PHP only via /php-reference/*.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.StorefrontAspNetEnabled),
            TenantSurface.ControlPanel or TenantSurface.Erp or TenantSurface.Bos or TenantSurface.Ip
                or TenantSurface.LifeOs => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                AdminTargetRuntime(),
                "CP/ERP/BOS/IP/LifeOS product shells are ASP.NET-primary; PHP only via /php-reference/*.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.AdminAspNetEnabled),
            _ => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                _options.AdminAspNetEnabled || _options.StorefrontAspNetEnabled
                    ? "aspnet-primary-php-reference"
                    : "php-primary",
                "Unknown surface follows ASP.NET-primary product policy when storefront/admin flags are on; PHP stays under /php-reference/*.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.AdminAspNetEnabled || _options.StorefrontAspNetEnabled)
        };
    }

    private string ApiTargetRuntime()
    {
        return _options.ApiShadowTrafficEnabled ? "aspnet-shadow-with-php-fallback" : "php-primary";
    }

    private string StorefrontTargetRuntime()
    {
        return _options.StorefrontAspNetEnabled ? "aspnet-primary-php-reference" : "php-primary";
    }

    private string AdminTargetRuntime()
    {
        return _options.AdminAspNetEnabled ? "aspnet-primary-php-reference" : "aspnet-shell-php-primary";
    }
}
