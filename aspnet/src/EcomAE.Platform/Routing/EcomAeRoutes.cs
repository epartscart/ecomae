namespace EcomAE.Platform.Routing;

public static class EcomAeRoutes
{
    public const string Health = "/health";
    public const string MigrationStatus = "/migration/status";
    public const string TenantContext = "/tenant/context";
    public const string ControlPanel = "/CP";
    public const string Erp = "/ERP";
    public const string Bos = "/BOS";
    public const string ApiPrefix = "/api";

    public static readonly string[] ProtectedSurfaces =
    [
        ControlPanel,
        Erp,
        Bos
    ];
}
