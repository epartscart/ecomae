namespace EcomAE.Platform.Security;

public static class EcomAePolicies
{
    public const string SuperCp = "SuperCp";
    public const string SuperErp = "SuperErp";
    public const string SuperBos = "SuperBos";
    public const string TenantCp = "TenantCp";
    public const string TenantErp = "TenantErp";
    public const string Api = "Api";

    public static IServiceCollection AddEcomAeAuthorization(this IServiceCollection services)
    {
        return services.AddAuthorizationBuilder()
            .AddPolicy(SuperCp, policy => policy.RequireClaim("permission", EcomAePermissions.SuperCpAccess))
            .AddPolicy(SuperErp, policy => policy.RequireClaim("permission", EcomAePermissions.SuperErpAccess))
            .AddPolicy(SuperBos, policy => policy.RequireClaim("permission", EcomAePermissions.SuperBosAccess))
            .AddPolicy(TenantCp, policy => policy.RequireClaim("permission", EcomAePermissions.TenantCpAccess))
            .AddPolicy(TenantErp, policy => policy.RequireClaim("permission", EcomAePermissions.TenantErpAccess))
            .AddPolicy(Api, policy => policy.RequireClaim("permission", EcomAePermissions.ApiAccess))
            .Services;
    }
}
