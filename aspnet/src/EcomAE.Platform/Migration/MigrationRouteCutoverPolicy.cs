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
                "API can take shadow traffic; destination is ASP.NET primary. PHP stays the parity-gate authority and later the reference host until dual-sample + approval.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.ApiShadowTrafficEnabled),
            TenantSurface.Storefront => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                StorefrontTargetRuntime(),
                "Storefront cart/checkout/account parity incomplete — PHP remains interim primary. Destination: ASP.NET live; PHP kept as reference for gap-finding.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.StorefrontAspNetEnabled),
            TenantSurface.ControlPanel or TenantSurface.Erp or TenantSurface.Bos => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                AdminTargetRuntime(),
                "Admin/ERP/BOS shells exist for parity; login/writes/audit still use PHP as interim primary. Destination: ASP.NET live primary with PHP reference retained.",
                RequiresPhpFallback: _options.RequirePhpFallback,
                ReadyForAspNetTraffic: _options.AdminAspNetEnabled),
            _ => new MigrationRouteCutoverDecision(
                tenant.Surface,
                tenant.Mode,
                "php-primary",
                "Unknown surface stays on interim PHP primary until explicit migration parity is approved; destination remains ASP.NET with PHP reference kept.",
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
