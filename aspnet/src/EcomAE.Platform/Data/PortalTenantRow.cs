using EcomAE.Platform.Services;

namespace EcomAE.Platform.Data;

public sealed record PortalTenantRow(
    string SiteKey,
    string Hostname,
    string DatabaseName,
    string Status,
    bool IsDemo,
    bool ErpOnlyShared,
    bool IsActive,
    bool DedicatedDb,
    string ScalePolicy)
{
    public TenantRegistryRecord ToTenantRegistryRecord()
    {
        var mode = IsDemo
            ? TenantMode.DemoTenant
            : ErpOnlyShared ? TenantMode.ErpOnlyTenant : TenantMode.LiveTenant;

        return new TenantRegistryRecord(
            Hostname,
            mode,
            SiteKey,
            DatabaseName,
            StorefrontEnabled: !ErpOnlyShared,
            ErpEnabled: true,
            ControlPanelEnabled: !ErpOnlyShared,
            BosEnabled: false);
    }
}
