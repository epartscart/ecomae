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
                "API scaffolding can receive shadow traffic, but PHP remains authoritative until database-backed auth, quotas, and catalog parity are proven.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.ApiShadowTrafficEnabled),
            TenantSurface.Storefront => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                StorefrontTargetRuntime(),
                "Storefront rendering, cart, checkout, and customer account parity are not complete, so PHP remains primary.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.StorefrontAspNetEnabled),
            TenantSurface.ControlPanel or TenantSurface.Erp or TenantSurface.Bos => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                AdminTargetRuntime(),
                "Administrative shells exist for parity planning, but login, permissions, data writes, audit, and reports still require PHP primary handling.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.AdminAspNetEnabled),
            _ => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "php-primary",
                "Unknown or default surface stays on PHP until explicit migration parity is approved.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: false)
        };
    }

    private string ApiTargetRuntime()
    {
        return _options.ApiShadowTrafficEnabled ? "aspnet-shadow-with-php-fallback" : "php-primary";
    }

    private string StorefrontTargetRuntime()
    {
        return _options.StorefrontAspNetEnabled ? "aspnet-storefront-with-php-fallback" : "php-primary";
    }

    private string AdminTargetRuntime()
    {
        return _options.AdminAspNetEnabled ? "aspnet-shell-with-php-fallback" : "aspnet-shell-php-primary";
    }
}
