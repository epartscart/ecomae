namespace EcomAE.Platform.Routing;

public static class EcomAeRoutes
{
    public const string Health = "/health";
    public const string MigrationStatus = "/migration/status";
    public const string MigrationReadiness = "/migration/readiness";
    public const string MigrationCutoverPlan = "/migration/cutover-plan";
    public const string MigrationProgress = "/migration/progress";
    public const string ZeroPhpCompletion = "/migration/zero-php-completion";
    public const string PythonSidecars = "/migration/python-sidecars";
    public const string MigrationRouteCutover = "/migration/route-cutover";
    public const string MigrationDataParity = "/migration/data-parity";
    public const string MigrationCutoverValidation = "/migration/cutover-validation";
    public const string SurfaceParity = "/migration/surface-parity";
    public const string TenantContext = "/tenant/context";
    public const string TenantWorkspaceParity = "/tenant/workspace/parity";
    public const string LegacySessionProbe = "/auth/session/probe";
    public const string LegacySessionParity = "/auth/session/parity";
    public const string LegacyApiClientParity = "/auth/api-client/parity";
    public const string ControlPanel = "/cp";
    public const string ControlPanelParity = "/cp/parity";
    public const string Erp = "/erp";
    public const string ErpParity = "/erp/parity";
    public const string Bos = "/bos";
    public const string BosParity = "/bos/parity";
    public const string ApiPrefix = "/api";
    public const string ApiMigrationStatus = "/api/migration/status";
    public const string CatalogStatus = "/api/v1/catalog/status";
    public const string CatalogManufacturers = "/api/v1/catalog/manufacturers";
    public const string CatalogParity = "/api/v1/catalog/parity";
    public const string PriceLookup = "/api/v1/price/lookup";
    public const string PriceLookupParity = "/api/v1/price/parity";
    public const string StorefrontParity = "/storefront/parity";

    public static readonly string[] ControlPanelAliases = [ControlPanel, "/cp/", "/CP", "/CP/"];

    public static readonly string[] ErpAliases = [Erp, "/erp/", "/ERP", "/ERP/"];

    public static readonly string[] BosAliases = [Bos, "/bos/", "/BOS", "/BOS/"];

    public static readonly string[] ProtectedSurfaces =
    [
        ControlPanel,
        Erp,
        Bos
    ];
}
