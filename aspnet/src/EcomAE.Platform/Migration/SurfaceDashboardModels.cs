namespace EcomAE.Platform.Migration;

public sealed record ControlPanelDashboardSummary(
    int Users,
    int AdminSessions,
    int PortalTenants,
    int ActivePortalTenants,
    string Source,
    string Message);

public sealed record ErpDashboardSummary(
    decimal CashPosition,
    decimal SupplierCredit,
    decimal SupplierDebit,
    decimal SupplierNet,
    int CashAccounts,
    int ActiveSuppliers,
    int ActivePurchases,
    string Source,
    string Message);

public sealed record BosFleetSummary(
    int PortalTenants,
    int ActivePortalTenants,
    int AdminSessions,
    int WithDatabase,
    int ErpOnly,
    string Source,
    string Message);

public sealed record StorefrontAccountSummary(
    int UserId,
    int Orders,
    int Sessions,
    string Source,
    string Message);

public sealed record PortalTenantDigest(
    string SiteKey,
    string Hostname,
    string IndustryCode,
    string Status,
    string TradeName,
    string HubName,
    string HostedOn,
    bool ErpOnly,
    bool IsActive,
    bool HasDb);

public sealed record PortalTenantListResult(
    IReadOnlyList<PortalTenantDigest> Tenants,
    int Count,
    string Source,
    string Message);

public sealed record BosFleetHealthResult(
    BosFleetSummary Summary,
    IReadOnlyList<PortalTenantDigest> SampleTenants,
    string Source,
    string Message);

public sealed record ErpAccountsSummaryResult(
    ErpDashboardSummary Summary,
    string Source,
    string Message);

public sealed record StorefrontOrderDigest(
    long Id,
    long TimeUnix,
    int Paid,
    int SuccessfullyCreated,
    int Status);

public sealed record StorefrontOrdersResult(
    int UserId,
    IReadOnlyList<StorefrontOrderDigest> Orders,
    int Count,
    string Source,
    string Message);
