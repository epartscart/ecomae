namespace EcomAE.Platform.Routing;

public static class EcomAeRoutes
{
    public const string Health = "/health";
    public const string MigrationStatus = "/migration/status";
    public const string MigrationReadiness = "/migration/readiness";
    public const string TenantContext = "/tenant/context";
    public const string LegacySessionProbe = "/auth/session/probe";
    public const string ControlPanel = "/CP";
    public const string Erp = "/ERP";
    public const string Bos = "/BOS";
    public const string ApiPrefix = "/api";
    public const string ApiMigrationStatus = "/api/migration/status";
    public const string CatalogStatus = "/api/v1/catalog/status";
    public const string PriceLookup = "/api/v1/price/lookup";

    public static readonly string[] ProtectedSurfaces =
    [
        ControlPanel,
        Erp,
        Bos
    ];
}
