using EcomAE.Platform.Services;

namespace EcomAE.Platform.Migration;

public sealed class MigrationRouteCutoverPolicy : IMigrationRouteCutoverPolicy
{
    public MigrationRouteCutoverDecision Decide(TenantContext tenant)
    {
        return tenant.Surface switch
        {
            TenantSurface.Api => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "aspnet-shadow-with-php-fallback",
                "API scaffolding can receive shadow traffic, but PHP remains authoritative until database-backed auth, quotas, and catalog parity are proven.",
                RequiresPhpFallback: true,
                ReadyForAspNetTraffic: true),
            TenantSurface.Storefront => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "php-primary",
                "Storefront rendering, cart, checkout, and customer account parity are not complete, so PHP remains primary.",
                RequiresPhpFallback: true,
                ReadyForAspNetTraffic: false),
            TenantSurface.ControlPanel or TenantSurface.Erp or TenantSurface.Bos => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "aspnet-shell-php-primary",
                "Administrative shells exist for parity planning, but login, permissions, data writes, audit, and reports still require PHP primary handling.",
                RequiresPhpFallback: true,
                ReadyForAspNetTraffic: false),
            _ => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "php-primary",
                "Unknown or default surface stays on PHP until explicit migration parity is approved.",
                RequiresPhpFallback: true,
                ReadyForAspNetTraffic: false)
        };
    }
}
