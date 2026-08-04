using EcomAE.Platform.Services;

namespace EcomAE.Platform.Data;

public sealed record PortalTenantRow(
    string SiteKey,
    string Hostname,
    string DatabaseName,
    string DbUser,
    string DbPassword,
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
            string.IsNullOrWhiteSpace(DatabaseName) ? null : DatabaseName,
            StorefrontEnabled: !ErpOnlyShared,
            ErpEnabled: true,
            ControlPanelEnabled: !ErpOnlyShared,
            BosEnabled: false,
            DbUser: string.IsNullOrWhiteSpace(DbUser) ? null : DbUser,
            DbPassword: string.IsNullOrWhiteSpace(DbPassword) ? null : DbPassword,
            DedicatedDb: DedicatedDb || string.Equals(ScalePolicy, "dedicated_mysql", StringComparison.OrdinalIgnoreCase),
            ScalePolicy: string.IsNullOrWhiteSpace(ScalePolicy) ? null : ScalePolicy);
    }
}
