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
    int GarageVehicles,
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

public sealed record CpUserDigest(
    int UserId,
    string Email,
    string Phone,
    int Unlocked,
    long TimeRegistered,
    long TimeLastVisit);

public sealed record CpUserListResult(
    IReadOnlyList<CpUserDigest> Users,
    int Count,
    string Source,
    string Message);

public sealed record CpGroupDigest(
    int Id,
    string Value,
    bool ForBackend,
    bool ForGuests,
    bool ForRegistrated,
    bool Unblocked,
    int Parent,
    int Level);

public sealed record CpGroupListResult(
    IReadOnlyList<CpGroupDigest> Groups,
    int Count,
    string Source,
    string Message);

public sealed record ErpSupplierDigest(
    long Id,
    string Name,
    long StorageId,
    decimal Balance);

public sealed record ErpSupplierListResult(
    IReadOnlyList<ErpSupplierDigest> Suppliers,
    int Count,
    string Source,
    string Message);

public sealed record ErpPurchaseDigest(
    long Id,
    long SupplierId,
    string SupplierName,
    long PurchaseDate,
    string InvoiceNumber,
    decimal TotalAmount,
    string Status,
    long OrderId);

public sealed record ErpPurchaseListResult(
    IReadOnlyList<ErpPurchaseDigest> Purchases,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontGarageVehicleDigest(
    long Id,
    string Caption,
    string Marka,
    string Model,
    string Year,
    string Vin,
    int Active);

public sealed record StorefrontGarageResult(
    int UserId,
    IReadOnlyList<StorefrontGarageVehicleDigest> Vehicles,
    int Count,
    string Source,
    string Message);

public sealed record ErpCashAccountDigest(
    long Id,
    string Name,
    string AccountType,
    string CurrencyCode,
    decimal OpeningBalance,
    decimal Balance);

public sealed record ErpCashAccountListResult(
    IReadOnlyList<ErpCashAccountDigest> Accounts,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontProfileResult(
    int UserId,
    string Email,
    int EmailConfirmed,
    string Phone,
    int PhoneConfirmed,
    int RegVariant,
    IReadOnlyDictionary<string, string> ProfileFields,
    string Source,
    string Message);

public sealed record ErpCashEntryDigest(
    long Id,
    long AccountId,
    string AccountName,
    string AccountType,
    long TimeUnix,
    int Direction,
    decimal Amount,
    string Reference,
    string Note);

public sealed record ErpCashEntryListResult(
    IReadOnlyList<ErpCashEntryDigest> Entries,
    int Count,
    string Source,
    string Message);

public sealed record ErpInvoiceDigest(
    long Id,
    string InvoiceNumber,
    long OrderId,
    int UserId,
    string CustomerEmail,
    long IssueDate,
    string Status,
    decimal TotalInclVat);

public sealed record ErpInvoiceListResult(
    IReadOnlyList<ErpInvoiceDigest> Invoices,
    int Count,
    string Source,
    string Message);

public sealed record ErpGlJournalDigest(
    long Id,
    string JournalNo,
    long JournalDate,
    string SourceType,
    long SourceId,
    string Status,
    decimal TotalDebit);

public sealed record ErpGlJournalListResult(
    IReadOnlyList<ErpGlJournalDigest> Journals,
    int Count,
    string Source,
    string Message);

public sealed record CpModuleDigest(
    int Id,
    string Caption,
    bool Activated,
    bool IsFrontend,
    bool IsPrototype,
    bool ControlAvailable);

public sealed record CpModuleListResult(
    IReadOnlyList<CpModuleDigest> Modules,
    int Count,
    string Source,
    string Message);

public sealed record CpConfigItemMetaDigest(
    string Name,
    string Caption,
    string Type,
    string ConfigGroup,
    bool Visible,
    int Order);

public sealed record CpConfigItemMetaListResult(
    IReadOnlyList<CpConfigItemMetaDigest> Items,
    int Count,
    string Source,
    string Message);

public sealed record BosFleetReadinessResult(
    int Tenants,
    int Pass,
    int Warn,
    int Fail,
    int Active,
    int WithDatabase,
    int ErpOnly,
    string Source,
    string Message);
