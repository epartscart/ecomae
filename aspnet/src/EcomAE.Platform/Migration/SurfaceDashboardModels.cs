namespace EcomAE.Platform.Migration;

public sealed record ControlPanelDashboardSummary(
    int Users,
    int AdminSessions,
    int PortalTenants,
    int ActivePortalTenants,
    string Source,
    string Message);

/// <summary>
/// ERP dashboard digest fields aligned to PHP <c>erp_dashboard.php</c> executive tiles
/// plus <c>epc_erp_cc_kpi_tiles</c> / approval-queue counts from the command center.
/// Missing tables degrade to zeros / "open" via safe SQL.
/// </summary>
public sealed record ErpDashboardSummary(
    decimal CashPosition,
    decimal SupplierCredit,
    decimal SupplierDebit,
    decimal SupplierNet,
    int CashAccounts,
    int ActiveSuppliers,
    int ActivePurchases,
    decimal Receivables,
    decimal Payables,
    decimal StockValue,
    decimal RevenueExVat,
    int OrdersCount,
    decimal ArBalance,
    decimal ApBalance,
    decimal VatNetPayable,
    string PeriodStatus,
    int InventoryItems,
    int DraftSalesOrders,
    int PendingPurchaseOrders,
    int UnpostedGlJournals,
    int OverdueInvoices,
    int LowStockItems,
    int PendingEinvoices,
    int ProcessOpen,
    int ProcessDone,
    int ProcessOverdue,
    string Source,
    string Message);

/// <summary>PHP <c>epc_erp_cc_approval_queue</c> structured queue item (read-only).</summary>
public sealed record ErpApprovalQueueItemDigest(
    string Id,
    string Category,
    string Label,
    int Count,
    string Action,
    string Link,
    string Severity,
    string Icon);

/// <summary>ERP dashboard KPIs + approval queue rows (PHP command-center parity).</summary>
public sealed record ErpDashboardDigestResult(
    ErpDashboardSummary Summary,
    IReadOnlyList<ErpApprovalQueueItemDigest> ApprovalQueue,
    int Count,
    string Source,
    string Message);

/// <summary>
/// BOS fleet summary aligned to PHP Fleet Command Center stats
/// (Total / Commerce / ERP Only / Demo) plus platform + session digests.
/// </summary>
public sealed record BosFleetSummary(
    int PortalTenants,
    int ActivePortalTenants,
    int AdminSessions,
    int WithDatabase,
    int ErpOnly,
    int CommerceTenants,
    int DemoTenants,
    int PlatformTenants,
    string Source,
    string Message);

public sealed record StorefrontAccountSummary(
    int UserId,
    int Orders,
    int Sessions,
    int GarageVehicles,
    string Source,
    string Message);

public sealed record StorefrontAccountDigestResult(
    StorefrontAccountSummary Summary,
    IReadOnlyList<StorefrontOrderDigest> RecentOrders,
    int Count,
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

public sealed record CpShopOrderDigest(
    long Id,
    long TimeUnix,
    int UserId,
    int Status,
    int Paid,
    int PaidType,
    int OfficeId,
    int SuccessfullyCreated,
    int CountItems,
    decimal OrderSum,
    decimal PurchaseSum = 0m,
    long LastModifiedUnix = 0,
    int ViewedFlag = 1,
    string CustomerLabel = "",
    string StatusName = "",
    string StatusBadgeClass = "epc-scp-badge--normal",
    string ObtainCaption = "");

public sealed record CpOrdersSummary(
    int Open,
    int Today,
    int PendingShip,
    string Source,
    string Message,
    int Completed = 0);

public sealed record CpOrdersListResult(
    CpOrdersSummary Summary,
    IReadOnlyList<CpShopOrderDigest> Orders,
    int Count,
    string Source,
    string Message);

/// <summary>Read-only OMS console detail (PHP epc_orders_detail_pane markers). Writes stay PHP.</summary>
public sealed record CpOrderItemDigest(
    long Id,
    long OrderId,
    string Brand,
    string Article,
    string Name,
    decimal Price,
    decimal CountNeed,
    decimal Purchase,
    int Status,
    string StatusName,
    string StorageLabel);

public sealed record CpOrderLogDigest(long TimeUnix, string Text, int IsManager, int IsRobot);

public sealed record CpOrderMessageDigest(long Id, long TimeUnix, string Text, int IsCustomer);

public sealed record CpOrderDetailDigest(
    CpShopOrderDigest Order,
    decimal PriceSum,
    decimal PurchaseSum,
    decimal PaidSum,
    decimal PaidLeft,
    decimal Margin,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    IReadOnlyList<CpOrderItemDigest> Items,
    IReadOnlyList<CpOrderLogDigest> Logs,
    IReadOnlyList<CpOrderMessageDigest> Messages,
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

public sealed record ErpCoaAccountDigest(
    long Id,
    string Code,
    string Name,
    string AccountType,
    string NormalSide,
    long ParentId,
    decimal OpeningBalance,
    decimal Balance,
    bool Active);

public sealed record ErpCoaAccountListResult(
    IReadOnlyList<ErpCoaAccountDigest> Accounts,
    int Count,
    string Source,
    string Message);

public sealed record ErpWarehouseDigest(
    long Id,
    long StorageId,
    string Code,
    string Name,
    bool Active,
    long TimeCreated);

public sealed record ErpWarehouseListResult(
    IReadOnlyList<ErpWarehouseDigest> Warehouses,
    int Count,
    string Source,
    string Message);

public sealed record ErpSalesOrderDigest(
    long Id,
    string SoNo,
    int CustomerUserId,
    decimal TotalAmount,
    string Status,
    long TimeCreated);

public sealed record ErpSalesOrderListResult(
    IReadOnlyList<ErpSalesOrderDigest> Orders,
    int Count,
    string Source,
    string Message);

public sealed record CpMenuDigest(
    int Id,
    string Caption,
    bool IsFrontend,
    string MenuUlClass,
    string MenuUlId,
    bool StructurePresent = false,
    bool StructureParseOk = true,
    int NodeCount = 0,
    int MaxDepth = 0,
    int UrlLinkCount = 0,
    int ContentLinkCount = 0,
    int UnknownLinkCount = 0);

public sealed record CpMenuListResult(
    IReadOnlyList<CpMenuDigest> Menus,
    int Count,
    string Source,
    string Message);

public sealed record CpPageDigest(
    int Id,
    string Caption,
    string Url,
    string Alias,
    bool IsFrontend,
    bool Published,
    int Level,
    int SortOrder);

public sealed record CpPageListResult(
    IReadOnlyList<CpPageDigest> Pages,
    int Count,
    string Source,
    string Message);

public sealed record CpAdminSessionDigest(
    int UserId,
    string Email,
    int Type,
    int SessionCount);

public sealed record CpAdminSessionListResult(
    IReadOnlyList<CpAdminSessionDigest> Sessions,
    int Count,
    string Source,
    string Message);

public sealed record CpStorageDigest(
    int Id,
    string Name,
    string ShortName,
    bool Hidden);

public sealed record CpStorageListResult(
    IReadOnlyList<CpStorageDigest> Storages,
    int Count,
    string Source,
    string Message);

public sealed record BosAuditLogDigest(
    long Id,
    long Ts,
    int UserId,
    string Actor,
    string Area,
    string Action,
    string Target,
    string Ip);

public sealed record BosAuditLogListResult(
    IReadOnlyList<BosAuditLogDigest> Entries,
    int Count,
    string Source,
    string Message);

public sealed record ErpPurchaseOrderDigest(
    long Id,
    string PoNo,
    long SupplierId,
    string Title,
    decimal TotalAmount,
    string Status,
    long TimeCreated);

public sealed record ErpPurchaseOrderListResult(
    IReadOnlyList<ErpPurchaseOrderDigest> Orders,
    int Count,
    string Source,
    string Message);

public sealed record ErpInventoryStockSummaryResult(
    long RowCount,
    decimal QtyOnHand,
    decimal StockValue,
    int WarehouseCount,
    int ItemCount,
    string Source,
    string Message);

/// <summary>One on-hand stock line — mirrors PHP <c>epc_erp_inventory_stock_report</c>.</summary>
public sealed record ErpInventoryStockDigest(
    long Id,
    long WarehouseId,
    long ItemId,
    string Sku,
    string Name,
    string ItemType,
    string Unit,
    string WarehouseName,
    decimal QtyOnHand,
    decimal AvgUnitCost,
    string BatchNo,
    string VariantLabel,
    string ExpiryDate,
    long TimeUpdated);

public sealed record ErpInventoryLowStockDigest(
    long Id,
    long WarehouseId,
    long ItemId,
    string Sku,
    string Name,
    string WarehouseName,
    decimal QtyOnHand,
    decimal ReorderLevel,
    decimal AvgUnitCost);

public sealed record ErpInventoryStockDigestResult(
    ErpInventoryStockSummaryResult Summary,
    IReadOnlyList<ErpInventoryStockDigest> Stock,
    IReadOnlyList<ErpInventoryLowStockDigest> LowStock,
    int Count,
    string Source,
    string Message);

public sealed record ErpInventoryMovementDigest(
    long Id,
    string MovementType,
    long WarehouseId,
    long ItemId,
    string Sku,
    string ItemName,
    string WarehouseName,
    decimal Qty,
    decimal SignedQty,
    decimal UnitCost,
    decimal TotalCost,
    string BatchNo,
    string Reference,
    long MovementDate,
    decimal RunningBalance);

public sealed record ErpInventoryMovementsSummary(
    int MovementCount,
    int InCount,
    int OutCount,
    decimal TotalInQty,
    decimal TotalOutQty,
    string Source,
    string Message);

public sealed record ErpInventoryMovementsDigestResult(
    ErpInventoryMovementsSummary Summary,
    IReadOnlyList<ErpInventoryMovementDigest> Movements,
    int Count,
    string Source,
    string Message);

public sealed record ErpAgingPartyDigest(
    string Name,
    decimal Bucket0,
    decimal Bucket1,
    decimal Bucket2,
    decimal Bucket3,
    decimal Bucket4,
    decimal Total);

public sealed record ErpAgingSummary(
    int Boundary1,
    int Boundary2,
    int Boundary3,
    decimal ArGrand,
    decimal ApGrand,
    decimal InventoryGrand,
    string Source,
    string Message);

public sealed record ErpAgingDigestResult(
    ErpAgingSummary Summary,
    IReadOnlyList<string> ArLabels,
    IReadOnlyList<string> ApLabels,
    IReadOnlyList<string> InventoryLabels,
    IReadOnlyList<ErpAgingPartyDigest> ArRows,
    IReadOnlyList<ErpAgingPartyDigest> ApRows,
    IReadOnlyList<ErpAgingPartyDigest> InventoryRows,
    int Count,
    string Source,
    string Message);

public sealed record CpCurrencyDigest(
    int Id,
    string IsoCode,
    string IsoName,
    string CaptionShort,
    decimal Rate,
    bool Available,
    int SortOrder);

public sealed record CpCurrencyListResult(
    IReadOnlyList<CpCurrencyDigest> Currencies,
    int Count,
    string Source,
    string Message);

public sealed record CpApiClientMetaDigest(
    long Id,
    string ClientKeyPrefix,
    string Product,
    string Label,
    string ContactEmail,
    bool Active,
    int DailyLimit,
    int CallsToday,
    long TimeCreated);

public sealed record CpApiClientMetaListResult(
    IReadOnlyList<CpApiClientMetaDigest> Clients,
    int Count,
    string Source,
    string Message);

public sealed record CpPowerBiConfigSummary(
    string SiteKey,
    string WorkspaceId,
    string AzureTenantId,
    string DefaultReportId,
    string DefaultDatasetId,
    string EmbedUrl,
    string EmbedMode,
    string Notes,
    bool Active,
    int ReportCount,
    string Source,
    string Message);

public sealed record CpPowerBiReportDigest(
    long Id,
    string SiteKey,
    string ReportId,
    string ReportName,
    string DatasetId,
    string Category,
    string EmbedUrl,
    bool Active);

public sealed record CpPowerBiDigestResult(
    CpPowerBiConfigSummary Summary,
    IReadOnlyList<CpPowerBiReportDigest> Reports,
    int Count,
    string Source,
    string Message);

public sealed record CpMobileAppsSummary(
    bool Enabled,
    string AppName,
    string BundleId,
    string DeepLinkScheme,
    string DeepLinkDomain,
    string ApiBaseUrl,
    string PlayStoreUrl,
    string AppStoreUrl,
    bool PwaEnabled,
    string FirebaseProjectId,
    bool PushEnabled,
    string Source,
    string Message);

public sealed record CpMobileAppsDigestResult(
    CpMobileAppsSummary Summary,
    string Source,
    string Message);

public sealed record CpMetabaseConfigSummary(
    string SiteKey,
    string MetabaseUrl,
    bool Active,
    int DashboardCount,
    string Source,
    string Message);

public sealed record CpMetabaseDashboardDigest(
    long Id,
    string SiteKey,
    int DashboardId,
    string DashboardName,
    string Category,
    bool Active);

public sealed record CpMetabaseDigestResult(
    CpMetabaseConfigSummary Summary,
    IReadOnlyList<CpMetabaseDashboardDigest> Dashboards,
    int Count,
    string Source,
    string Message);

public sealed record CpNlReportDefinitionDigest(
    long Id,
    string SiteKey,
    string Name,
    string Description,
    string ReportType,
    string Schedule,
    string Format,
    bool Active,
    long CreatedBy);

public sealed record CpNlReportingDigestResult(
    IReadOnlyList<CpNlReportDefinitionDigest> Definitions,
    int Count,
    string Source,
    string Message);

public sealed record CpMarketingBroadcastSummary(
    int Campaigns,
    int EmailsSent,
    int WhatsappSent,
    string Source,
    string Message);

public sealed record CpMarketingBroadcastCampaignDigest(
    long Id,
    long CreatedAt,
    string Channel,
    string TemplateKey,
    string Subject,
    string Preview,
    string AudienceMode,
    string AudienceMeta,
    int TotalTargets,
    int SentOk,
    int SentFail,
    string Status,
    long OperatorId);

public sealed record CpMarketingBroadcastDigestResult(
    CpMarketingBroadcastSummary Summary,
    IReadOnlyList<CpMarketingBroadcastCampaignDigest> Campaigns,
    int Count,
    string Source,
    string Message);

public sealed record CpDemoTenantDigest(
    string SiteKey,
    string Hostname,
    string IndustryCode,
    string Status,
    string TradeName,
    string HubName,
    string HostedOn,
    bool ErpOnly,
    bool IsActive,
    long DemoExpiresAt,
    string DemoContactEmail);

public sealed record CpDemoTenantsDigestResult(
    IReadOnlyList<CpDemoTenantDigest> Tenants,
    int Count,
    string Source,
    string Message);

public sealed record CpPartsAgentSummary(
    int TotalSessions,
    int SessionsToday,
    int MessagesToday,
    int LoggedInSessions,
    int GuestSessions,
    bool Enabled,
    string AgentName,
    string Domain,
    string Source,
    string Message);

public sealed record CpPartsAgentSessionDigest(
    string SessionId,
    long UpdatedAt,
    int MessageCount,
    string CountryCode,
    string CountryName,
    long UserId,
    string IpHash,
    string LastUserText,
    string LastAgentText);

public sealed record CpPartsAgentDigestResult(
    CpPartsAgentSummary Summary,
    IReadOnlyList<CpPartsAgentSessionDigest> Sessions,
    int Count,
    string Source,
    string Message);

public sealed record CpPosOverviewSummary(
    bool PosEnabled,
    string RegisterName,
    int OpenSessions,
    int SalesToday,
    decimal SalesTotalToday,
    string Source,
    string Message);

public sealed record CpPosSaleDigest(
    long Id,
    string SaleNo,
    long SessionId,
    string CustomerLabel,
    decimal SubtotalEx,
    decimal VatAmount,
    decimal TotalAmount,
    string PaymentMethod,
    string TaxKitCode,
    string Status,
    long TimeCreated);

public sealed record CpPosOverviewDigestResult(
    CpPosOverviewSummary Summary,
    IReadOnlyList<CpPosSaleDigest> Sales,
    int Count,
    string Source,
    string Message);

public sealed record CpTaxToolkitsSummary(
    int ToolkitCount,
    int InstallCount,
    string TenantCountry,
    string TenantKitCode,
    string Source,
    string Message);

public sealed record CpTaxToolkitDigest(
    long Id,
    string KitCode,
    string Name,
    string Jurisdiction,
    string TaxType,
    bool IsSystem,
    bool Active);

public sealed record CpTaxToolkitsDigestResult(
    CpTaxToolkitsSummary Summary,
    IReadOnlyList<CpTaxToolkitDigest> Toolkits,
    int Count,
    string Source,
    string Message);

public sealed record CpSmsWhatsappSummary(
    int SmsOperators,
    string ActiveOperator,
    int WhatsappSent,
    int WhatsappFailed,
    string Source,
    string Message);

public sealed record CpSmsOperatorDigest(
    long Id,
    string Name,
    string Handler,
    string Description,
    bool Active,
    bool ControlAvailable);

public sealed record CpWhatsappLogDigest(
    long Id,
    long CreatedAt,
    string NotifyName,
    string PhoneMasked,
    int Status,
    string MessagePreview);

public sealed record CpSmsWhatsappDigestResult(
    CpSmsWhatsappSummary Summary,
    IReadOnlyList<CpSmsOperatorDigest> Operators,
    IReadOnlyList<CpWhatsappLogDigest> WhatsappLog,
    int Count,
    string Source,
    string Message);


public sealed record CpCrmBoardSummary(
    int Leads,
    int Opportunities,
    int Activities,
    int TicketsOpen,
    string Source,
    string Message);

public sealed record CpCrmLeadDigest(
    long Id,
    string Title,
    string Status,
    string Source,
    long OwnerId,
    decimal Amount,
    long UpdatedAt);

public sealed record CpCrmBoardDigestResult(
    CpCrmBoardSummary Summary,
    IReadOnlyList<CpCrmLeadDigest> Leads,
    int Count,
    string Source,
    string Message);

public sealed record CpDocumentControlSummary(
    string CompanyName,
    int TemplateCount,
    int AttachmentCount,
    string Source,
    string Message);

public sealed record CpDocumentTemplateDigest(
    long Id,
    string Code,
    string Title,
    string Category,
    bool Active,
    int SortOrder);

public sealed record CpDocumentControlDigestResult(
    CpDocumentControlSummary Summary,
    IReadOnlyList<CpDocumentTemplateDigest> Templates,
    int Count,
    string Source,
    string Message);

public sealed record CpDeliveryMethodsSummary(
    int Methods,
    int Available,
    string Source,
    string Message);

public sealed record CpDeliveryMethodDigest(
    long Id,
    string Caption,
    string Handler,
    bool Available,
    bool ControlAvailable,
    int SortOrder);

public sealed record CpDeliveryMethodsDigestResult(
    CpDeliveryMethodsSummary Summary,
    IReadOnlyList<CpDeliveryMethodDigest> Modes,
    int Count,
    string Source,
    string Message);

public sealed record CpCrossesSummary(
    int TotalPairs,
    int Brands,
    string Source,
    string Message);

public sealed record CpCrossPairDigest(
    long Id,
    string Manufacturer,
    string Article,
    string CrossManufacturer,
    string CrossArticle);

public sealed record CpCrossesDigestResult(
    CpCrossesSummary Summary,
    IReadOnlyList<CpCrossPairDigest> Pairs,
    int Count,
    string Source,
    string Message);

public sealed record CpHrOverviewSummary(
    int ActiveEmployees,
    int PendingLeave,
    int PayrollRuns,
    int AttendanceRows,
    string Source,
    string Message);

public sealed record CpHrEmployeeDigest(
    long Id,
    string Code,
    string Name,
    string Department,
    string Status,
    long JoinDate);

public sealed record CpHrOverviewDigestResult(
    CpHrOverviewSummary Summary,
    IReadOnlyList<CpHrEmployeeDigest> Employees,
    int Count,
    string Source,
    string Message);

public sealed record CpProductionOverviewSummary(
    int BomCount,
    int OpenWorkOrders,
    int CompletedWorkOrders,
    string Source,
    string Message);

public sealed record CpProductionWorkOrderDigest(
    long Id,
    string WoNo,
    string Status,
    decimal QtyPlanned,
    decimal QtyProduced,
    long UpdatedAt);

public sealed record CpProductionOverviewDigestResult(
    CpProductionOverviewSummary Summary,
    IReadOnlyList<CpProductionWorkOrderDigest> WorkOrders,
    int Count,
    string Source,
    string Message);

public sealed record CpProjectsOverviewSummary(
    int OpenProjects,
    int TaskCount,
    int ContractCount,
    string Source,
    string Message);

public sealed record CpProjectDigest(
    long Id,
    string Code,
    string Name,
    string Status,
    string BillingType,
    decimal ContractValue);

public sealed record CpProjectsOverviewDigestResult(
    CpProjectsOverviewSummary Summary,
    IReadOnlyList<CpProjectDigest> Projects,
    int Count,
    string Source,
    string Message);

public sealed record CpIndustryPacksSummary(
    int PackCount,
    int ActivePacks,
    int Assignments,
    string Source,
    string Message);

public sealed record CpIndustryPackDigest(
    long Id,
    string PackKey,
    string Name,
    string Description,
    string Icon,
    bool Active);

public sealed record CpIndustryPacksDigestResult(
    CpIndustryPacksSummary Summary,
    IReadOnlyList<CpIndustryPackDigest> Packs,
    int Count,
    string Source,
    string Message);

/// <summary>PHP <c>epc_erp_companies_list</c> row for ASP.NET company picker.</summary>
public sealed record ErpCompanyDigest(
    long Id,
    string Code,
    string Name,
    string CurrencyCode,
    string CountryCode,
    string IndustryPack,
    bool Active);

public sealed record ErpCompaniesDigestResult(
    IReadOnlyList<ErpCompanyDigest> Companies,
    int Count,
    string Source,
    string Message);

public sealed record CpJewelleryRetailSummary(
    int VoucherCount,
    int OpenVouchers,
    int TagCount,
    int MetalStockRows,
    string Source,
    string Message);

public sealed record CpJewelleryVoucherDigest(
    long Id,
    string VocType,
    string VocDate,
    long VocNo,
    string PartyName,
    string Status,
    decimal NetAmount,
    decimal VatAmount,
    decimal TotalWithVat);

public sealed record CpJewelleryRetailDigestResult(
    CpJewelleryRetailSummary Summary,
    IReadOnlyList<CpJewelleryVoucherDigest> Vouchers,
    int Count,
    string Source,
    string Message);

public sealed record CpPriceListsSummary(
    int ActiveLists,
    int PriceRows,
    int UploadCount,
    string Source,
    string Message);

public sealed record CpPriceListDigest(
    long Id,
    string Code,
    string Name,
    string Currency,
    long CustomerId,
    int Priority,
    bool Active);

public sealed record CpPriceListsDigestResult(
    CpPriceListsSummary Summary,
    IReadOnlyList<CpPriceListDigest> Lists,
    int Count,
    string Source,
    string Message);

public sealed record CpAutoPriceSummary(
    int ActiveRules,
    int ActiveSources,
    int CompareRuns,
    string Source,
    string Message);

public sealed record CpAutoPriceRuleDigest(
    long Id,
    string SiteKey,
    string RuleKey,
    decimal MinMarginPercent,
    bool AutoUpdatePrices,
    int ScheduleHours,
    bool Active,
    long UpdatedAt);

public sealed record CpAutoPriceDigestResult(
    CpAutoPriceSummary Summary,
    IReadOnlyList<CpAutoPriceRuleDigest> Rules,
    int Count,
    string Source,
    string Message);

public sealed record CpUaeTaxComplianceSummary(
    int LegislationCount,
    int VatAdvanceRows,
    int VatRefundRows,
    string Source,
    string Message);

public sealed record CpUaeTaxItemDigest(
    long Id,
    string Slug,
    string Title,
    string IssueDate,
    string Category,
    string TaxCategory,
    bool IsNew,
    bool IsUpdated,
    long TimeSynced);

public sealed record CpUaeTaxComplianceDigestResult(
    CpUaeTaxComplianceSummary Summary,
    IReadOnlyList<CpUaeTaxItemDigest> Items,
    int Count,
    string Source,
    string Message);

public sealed record CpBudgetsSummary(
    int BudgetCount,
    int ActiveBudgets,
    int BudgetLineCount,
    int DimensionCount,
    string Source,
    string Message);

public sealed record CpBudgetDigest(
    long Id,
    string Code,
    string Name,
    string FiscalYear,
    long BusinessUnitId,
    bool IsMaster,
    bool Active);

public sealed record CpBudgetsDigestResult(
    CpBudgetsSummary Summary,
    IReadOnlyList<CpBudgetDigest> Budgets,
    int Count,
    string Source,
    string Message);

public sealed record CpCarriersSummary(
    int CarrierCount,
    int ActiveCarriers,
    int ShipmentCount,
    int OpenShipments,
    string Source,
    string Message);

/// <summary>Courier partner accounts from <c>epc_carrier_accounts</c> (+ catalog region/blurb). Not ERP TMS.</summary>
public sealed record CpCarrierDigest(
    long Id,
    string Code,
    string Name,
    bool Active,
    bool DemoMode,
    long TimeCreated,
    string Region,
    string Blurb);

public sealed record CpCarriersDigestResult(
    CpCarriersSummary Summary,
    IReadOnlyList<CpCarrierDigest> Carriers,
    int Count,
    string Source,
    string Message);

public sealed record CpPaymentGatewaysSummary(
    int GatewayCount,
    int EnabledGateways,
    int ActiveGateways,
    int SelectableGateways,
    int AccountCount,
    string Source,
    string Message);

/// <summary><c>anable</c>=Enabled; <c>active</c>=Default gateway. Secrets omitted.</summary>
public sealed record CpPaymentGatewayDigest(
    long Id,
    string Name,
    string Handler,
    bool Anable,
    bool Active,
    bool IsSelectable);

public sealed record CpPaymentGatewaysDigestResult(
    CpPaymentGatewaysSummary Summary,
    IReadOnlyList<CpPaymentGatewayDigest> Gateways,
    int Count,
    string Source,
    string Message);

public sealed record CpWorkflowsSummary(
    int WorkflowCount,
    int ActiveWorkflows,
    int RunCount,
    int FailedRuns,
    string Source,
    string Message);

public sealed record CpWorkflowDigest(
    long Id,
    string SiteKey,
    string Name,
    string TriggerType,
    bool Active,
    int Version,
    int RunCount,
    string LastRunStatus);

public sealed record CpWorkflowsDigestResult(
    CpWorkflowsSummary Summary,
    IReadOnlyList<CpWorkflowDigest> Workflows,
    int Count,
    string Source,
    string Message);

public sealed record CpPurchaseRequestsSummary(
    int ReqCount,
    int DraftCount,
    int PendingApproval,
    int LineCount,
    int CategoryCount,
    string Source,
    string Message);

public sealed record CpPurchaseRequestDigest(
    long Id,
    long CompanyId,
    string ReqNumber,
    string Requester,
    long BusinessUnitId,
    string Status,
    decimal Total,
    bool RequiresApproval,
    string PoRef,
    long TimeCreated);

public sealed record CpPurchaseRequestsDigestResult(
    CpPurchaseRequestsSummary Summary,
    IReadOnlyList<CpPurchaseRequestDigest> Requests,
    int Count,
    string Source,
    string Message);

public sealed record CpPromotionsSummary(
    int PromotionCount,
    int ActivePromotions,
    int PercentPromotions,
    int LoyaltyAccounts,
    string Source,
    string Message);

public sealed record CpPromotionDigest(
    long Id,
    string Code,
    string Name,
    string Type,
    decimal Value,
    decimal MinSpend,
    long ValidFrom,
    long ValidTo,
    bool Active);

public sealed record CpPromotionsDigestResult(
    CpPromotionsSummary Summary,
    IReadOnlyList<CpPromotionDigest> Promotions,
    int Count,
    string Source,
    string Message);

public sealed record CpCrmOpportunitiesSummary(
    int OpportunityCount,
    int OpenOpportunities,
    int WonOpportunities,
    decimal PipelineAmount,
    string Source,
    string Message);

public sealed record CpCrmOpportunityDigest(
    long Id,
    string Title,
    string Stage,
    decimal Amount,
    int Probability,
    long CloseDate,
    long OwnerUserId,
    long LeadId,
    bool Active);

public sealed record CpCrmOpportunitiesDigestResult(
    CpCrmOpportunitiesSummary Summary,
    IReadOnlyList<CpCrmOpportunityDigest> Opportunities,
    int Count,
    string Source,
    string Message);

/// <summary>CP Integrations Hub KPIs (catalog rows) — not webhook delivery counters.</summary>
public sealed record CpIntegrationsSummary(
    int CatalogCount,
    int ActiveCount,
    int GuideCount,
    int CategoryCount,
    string Source,
    string Message);

/// <summary>Hub row from <c>epc_integrations_hub_rows</c> shape (key/label/blurb/category/configure_url).</summary>
public sealed record CpIntegrationDigest(
    string Key,
    string Label,
    string Blurb,
    string Category,
    bool Active,
    string ConfigureUrl,
    string Guide,
    string Icon,
    string Color);

public sealed record CpIntegrationsDigestResult(
    CpIntegrationsSummary Summary,
    IReadOnlyList<CpIntegrationDigest> Integrations,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontPartOfferDigest(
    int PriceId,
    string PriceList,
    string Manufacturer,
    string Article,
    string ArticleShow,
    string Name,
    decimal Price,
    int Exist,
    string Storage,
    string TimeToExe = "",
    string CheckHash = "",
    int MinOrder = 1,
    int ProductType = 2,
    int OfficeId = 0,
    int StorageId = 0,
    string JsonParams = "",
    decimal PricePurchase = 0m,
    int Markup = 0,
    int Probability = 100,
    string TimeToExeGuaranteed = "");

public sealed record StorefrontPartSearchResult(
    string Article,
    IReadOnlyList<StorefrontPartOfferDigest> Rows,
    int Count,
    string Source,
    string Message);

/// <summary>
/// Lightweight CHPU SEO stock probe (PHP <c>$epc_chpu_anchor_has_stock</c> LIMIT 1).
/// Does not load full warehouse offer rows — those arrive via protocol-3 AJAX.
/// </summary>
public sealed record StorefrontPartStockProbeResult(
    string Article,
    bool InStock,
    string ProductName,
    decimal MinPrice,
    string Source,
    string Message);

/// <summary>
/// PHP brand-picker row (Manufacturer / Article / Name / Availability / Term / Price).
/// Cross-only brands may have empty stock fields until the warehouse CHPU is opened.
/// </summary>
public sealed record StorefrontArticleBrandDigest(
    string Brand,
    string Name = "",
    int Exist = 0,
    decimal? MinPrice = null,
    string Warehouse = "");

public sealed record StorefrontArticleBrandsResult(
    string Article,
    IReadOnlyList<StorefrontArticleBrandDigest> Brands,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontCrossRefDigest(
    string Brand,
    string Article,
    bool InStock,
    string Source = "cp");

public sealed record StorefrontCrossRefsResult(
    string Article,
    IReadOnlyList<StorefrontCrossRefDigest> Rows,
    int Count,
    string Source,
    string Message);

/// <summary>PHP <c>ajax_epc_cross_search</c>-shaped payload for CHPU first paint (~1s).</summary>
public sealed record StorefrontCrossSearchResult(
    string Article,
    string Brand,
    IReadOnlyList<StorefrontCrossRefDigest> References,
    IReadOnlyList<StorefrontCrossRefDigest> Stock,
    int LocalCount,
    int CrossbaseCount,
    int UniqueReferenceCount,
    string Source,
    string Message);

public sealed record StorefrontQuoteDigest(
    int Id,
    string Status,
    long TimeCreated,
    long TimeUpdated,
    int ItemCount);

public sealed record StorefrontQuoteItemDigest(
    int Id,
    string Manufacturer,
    string Article,
    string Name,
    decimal CountNeed,
    decimal Price);

public sealed record StorefrontQuoteListResult(
    int UserId,
    IReadOnlyList<StorefrontQuoteDigest> Rows,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontQuoteDetailDigest(
    int Id,
    string Status,
    IReadOnlyList<StorefrontQuoteItemDigest> Items,
    string Source,
    string Message);

public sealed record StorefrontProductImageDigest(
    int Id,
    string Url,
    string Alt = "",
    bool IsPrimary = false);

public sealed record StorefrontProductSpecDigest(
    string GroupName,
    string Label,
    string Value,
    string Unit = "",
    string ValueType = "text");

public sealed record StorefrontProductDigest(
    int Id,
    string Caption,
    string Alias,
    int CategoryId,
    string Manufacturer,
    string Article,
    bool Published,
    string Description = "",
    IReadOnlyList<StorefrontProductImageDigest>? Images = null,
    IReadOnlyList<StorefrontProductSpecDigest>? Specs = null);

public sealed record StorefrontProductResult(
    StorefrontProductDigest? Product,
    string Source,
    string Message);

public sealed record StorefrontProductListResult(
    IReadOnlyList<StorefrontProductDigest> Rows,
    int Count,
    string Source,
    string Message);

/// <summary>Flat own-catalogue category row (PHP <c>shop_catalogue_categories</c>).</summary>
public sealed record StorefrontCatalogueCategoryRow(
    int Id,
    string Alias,
    string Url,
    int Parent,
    int Level,
    int ChildCount,
    int SortOrder,
    string Image,
    string Value);

/// <summary>Nested own-catalogue tree node for mega menu / own-catalog-app.</summary>
public sealed record StorefrontCatalogueCategoryNode(
    int Id,
    string Alias,
    string Url,
    int Parent,
    int Level,
    int ChildCount,
    int SortOrder,
    string Image,
    string Value,
    string Href,
    IReadOnlyList<StorefrontCatalogueCategoryNode> Data);

public sealed record StorefrontCatalogueTreeResult(
    IReadOnlyList<StorefrontCatalogueCategoryNode> Tree,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontCatalogueProductsResult(
    int CategoryId,
    string CategoryUrl,
    string CategoryValue,
    string Search,
    IReadOnlyList<StorefrontProductDigest> Rows,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontGenuineBrandsResult(
    IReadOnlyList<string> Brands,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontOfficeStorageBunchDigest(
    int OfficeId,
    int StorageId,
    int ProtocolVersion,
    string HandlerFolder,
    bool TreelaxCatalogue,
    IReadOnlyList<StorefrontOfficeStorageBunchDigest>? NestedBunches = null);

public sealed record StorefrontOfficeStorageBunchesResult(
    string Article,
    string Brand,
    IReadOnlyList<StorefrontOfficeStorageBunchDigest> Bunches,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontProductsOfBunchResult(
    int Result,
    int OfficeId,
    int StorageId,
    IReadOnlyList<StorefrontPartOfferDigest> Products,
    bool PricesVisible,
    string Source,
    string Message);

public sealed record StorefrontBulkUploadHistoryDigest(
    int Id,
    string FileName,
    string Priority,
    int UploadedCount,
    int AvailableCount,
    int CrossCount,
    int ShortCount,
    int NotFoundCount,
    string CreatedAt,
    string UpdatedAt);

public sealed record StorefrontBulkUploadHistoryResult(
    int UserId,
    IReadOnlyList<StorefrontBulkUploadHistoryDigest> Rows,
    int Count,
    string Source,
    string Message);

public sealed record StorefrontCartLineDigest(
    long Id,
    decimal Price,
    decimal CountNeed,
    bool CheckedForOrder,
    int ProductType,
    string Manufacturer,
    string Article,
    string Name,
    string TimeToExe,
    string TimeToExeGuaranteed,
    decimal MinOrder,
    decimal T2Exist = 0m);

public sealed record StorefrontCartSummary(
    int Count,
    decimal Sum,
    string Source,
    string Message);

public sealed record StorefrontCartListResult(
    int UserId,
    StorefrontCartSummary Summary,
    IReadOnlyList<StorefrontCartLineDigest> Lines,
    int Count,
    string Source,
    string Message);

public sealed record ErpBankReconciliationSummary(
    int LineCount,
    int UnmatchedCount,
    int MatchedCount,
    decimal CreditTotal,
    decimal DebitTotal,
    string Source,
    string Message);

public sealed record ErpBankStatementLineDigest(
    long Id,
    long AccountId,
    long LineDate,
    string Description,
    string Reference,
    decimal Amount,
    int Direction,
    long MatchedEntryId,
    string ImportBatch,
    long TimeCreated);

public sealed record ErpBankReconciliationDigestResult(
    ErpBankReconciliationSummary Summary,
    IReadOnlyList<ErpBankStatementLineDigest> Lines,
    int Count,
    string Source,
    string Message);

public sealed record ErpStockTransfersSummary(
    int TransferCount,
    int DraftCount,
    int InTransitCount,
    int ReceivedCount,
    decimal TotalQty,
    string Source,
    string Message);

public sealed record ErpStockTransferDigest(
    long Id,
    long CompanyId,
    string TransferNo,
    long FromWarehouseId,
    long ToWarehouseId,
    string Reason,
    string Status,
    int TotalItems,
    decimal TotalQty,
    string ShippedAt,
    string ReceivedAt,
    long CreatedBy,
    long TimeCreated);

public sealed record ErpStockTransfersDigestResult(
    ErpStockTransfersSummary Summary,
    IReadOnlyList<ErpStockTransferDigest> Transfers,
    int Count,
    string Source,
    string Message);

public sealed record ErpSalesQuotationsSummary(
    int QuoteCount,
    int DraftCount,
    int SentCount,
    int AcceptedCount,
    decimal SubtotalSum,
    string Source,
    string Message);

public sealed record ErpSalesQuotationDigest(
    long Id,
    long OpportunityId,
    long LeadId,
    long CustomerUserId,
    string QuoteNumber,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    long ShopOrderId,
    long TimeCreated,
    bool Active);

public sealed record ErpSalesQuotationsDigestResult(
    ErpSalesQuotationsSummary Summary,
    IReadOnlyList<ErpSalesQuotationDigest> Quotations,
    int Count,
    string Source,
    string Message);

public sealed record ErpWorkspaceFavoritesSummary(
    int ShortcutCount,
    int PinnedCount,
    int UserCount,
    int ErpSurfaceCount,
    string Source,
    string Message);

public sealed record ErpWorkspaceFavoriteDigest(
    long Id,
    long CompanyId,
    long UserId,
    string Surface,
    string ShortcutKey,
    string Label,
    string IconClass,
    string TargetUrl,
    string TargetTab,
    int SortOrder,
    bool IsPinned,
    long TimeCreated);

public sealed record ErpWorkspaceFavoritesDigestResult(
    ErpWorkspaceFavoritesSummary Summary,
    IReadOnlyList<ErpWorkspaceFavoriteDigest> Favorites,
    int Count,
    string Source,
    string Message);

public sealed record ErpFixedAssetsSummary(
    int AssetCount,
    int ActiveCount,
    int DisposedCount,
    decimal CostTotal,
    decimal BookValueTotal,
    string Source,
    string Message);

public sealed record ErpFixedAssetDigest(
    long Id,
    string AssetCode,
    string Name,
    long CategoryId,
    string AcquisitionDate,
    decimal Cost,
    decimal SalvageValue,
    int UsefulLifeMonths,
    string DepreciationMethod,
    decimal AccumulatedDepreciation,
    decimal BookValue,
    string Location,
    string Status,
    long TimeCreated);

public sealed record ErpFixedAssetsDigestResult(
    ErpFixedAssetsSummary Summary,
    IReadOnlyList<ErpFixedAssetDigest> Assets,
    int Count,
    string Source,
    string Message);

/// <summary>Process-flow case KPIs from PHP <c>epc_pf_cases</c> (digest route keeps -tasks stem).</summary>
public sealed record ErpProcessFlowTasksSummary(
    int TaskCount,
    int OpenCount,
    int DoneCount,
    int OverdueCount,
    int CancelledCount,
    string Source,
    string Message);

public sealed record ErpProcessFlowTaskDigest(
    long Id,
    long ProcessId,
    string Title,
    string Reference,
    string Priority,
    string Status,
    int CurrentStepNo,
    long CurrentAssigneeId,
    string CurrentDepartment,
    long InitiatorId,
    string SubjectType,
    long SubjectId,
    long StartedAt,
    long DueAt,
    long CompletedAt,
    long TimeCreated,
    long TimeUpdated);

public sealed record ErpProcessFlowTasksDigestResult(
    ErpProcessFlowTasksSummary Summary,
    IReadOnlyList<ErpProcessFlowTaskDigest> Tasks,
    int Count,
    string Source,
    string Message);

/// <summary>PHP <c>epc_rc_registry</c> report metadata (run closures stay PHP).</summary>
public sealed record ErpReportCenterReportDigest(
    string Key,
    string Area,
    string Name,
    string Desc);

public sealed record ErpReportCenterSummary(
    int ReportCount,
    int AreaCount,
    string SelectedKey,
    int SelectedRowCount,
    string Source,
    string Message);

public sealed record ErpReportCenterDigestResult(
    ErpReportCenterSummary Summary,
    IReadOnlyList<ErpReportCenterReportDigest> Reports,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    int Count,
    string Source,
    string Message);

public sealed record CpPageBuilderSummary(
    int LayoutCount,
    int PublishedCount,
    int DraftCount,
    int SiteCount,
    string Source,
    string Message);

public sealed record CpPageBuilderLayoutDigest(
    long Id,
    string SiteKey,
    string PageKey,
    bool IsPublished,
    long UpdatedAt,
    long PublishedAt);

public sealed record CpPageBuilderDigestResult(
    CpPageBuilderSummary Summary,
    IReadOnlyList<CpPageBuilderLayoutDigest> Layouts,
    int Count,
    string Source,
    string Message);

public sealed record CpProductCatalogueSummary(
    int ProductCount,
    int PublishedCount,
    int UnpublishedCount,
    int CategoryCount,
    string Source,
    string Message);

public sealed record CpProductCatalogueDigest(
    long Id,
    long CategoryId,
    string Caption,
    string Alias,
    bool PublishedFlag);

public sealed record CpProductCatalogueDigestResult(
    CpProductCatalogueSummary Summary,
    IReadOnlyList<CpProductCatalogueDigest> Products,
    int Count,
    string Source,
    string Message);

public sealed record CpPlatformGovernanceSummary(
    int RuleCount,
    int ActiveCount,
    int RequiredCount,
    int CategoryCount,
    string Source,
    string Message);

public sealed record CpPlatformGovernanceRuleDigest(
    long Id,
    string RuleKey,
    string Category,
    string Title,
    string Enforcement,
    string Scope,
    string ModuleLink,
    bool Active,
    long TimeUpdated);

public sealed record CpPlatformGovernanceDigestResult(
    CpPlatformGovernanceSummary Summary,
    IReadOnlyList<CpPlatformGovernanceRuleDigest> Rules,
    int Count,
    string Source,
    string Message);

public sealed record CpEinvoiceDocumentsSummary(
    int DocumentCount,
    int OpenCount,
    int SubmittedCount,
    decimal TotalInclVat,
    string Source,
    string Message);

public sealed record CpEinvoiceDocumentDigest(
    long Id,
    string Uuid,
    string InvoiceNumber,
    long OrderId,
    long UserId,
    string DocCategory,
    long IssueDate,
    string CurrencyCode,
    string Status,
    decimal TotalInclVat,
    bool ValidationOk,
    long TimeCreated);

public sealed record CpEinvoiceDocumentsDigestResult(
    CpEinvoiceDocumentsSummary Summary,
    IReadOnlyList<CpEinvoiceDocumentDigest> Documents,
    int Count,
    string Source,
    string Message);

public sealed record CpJewelleryRepairsSummary(
    int RepairCount,
    int OpenCount,
    int AuthorizedCount,
    int ItemCount,
    string Source,
    string Message);

public sealed record CpJewelleryRepairDigest(
    long Id,
    long CompanyId,
    string Branch,
    string VocType,
    string VocDate,
    long VocNo,
    string CustomerName,
    string Status,
    string Currency,
    string DeliveryDate,
    bool Authorized,
    string CreatedAt);

public sealed record CpJewelleryRepairsDigestResult(
    CpJewelleryRepairsSummary Summary,
    IReadOnlyList<CpJewelleryRepairDigest> Repairs,
    int Count,
    string Source,
    string Message);

public sealed record CpCrmTicketsSummary(
    int TicketCount,
    int OpenCount,
    int HighPriorityCount,
    int MessageCount,
    string Source,
    string Message);

public sealed record CpCrmTicketDigest(
    long Id,
    long CustomerUserId,
    long OrderId,
    string Subject,
    string Status,
    string Priority,
    long AssignedUserId,
    long TimeCreated,
    long TimeUpdated,
    bool Active);

public sealed record CpCrmTicketsDigestResult(
    CpCrmTicketsSummary Summary,
    IReadOnlyList<CpCrmTicketDigest> Tickets,
    int Count,
    string Source,
    string Message);

public sealed record CpMarketingGrowthSummary(
    int TaskCount,
    int TasksDone,
    int KpiLogCount,
    int ReviewCount,
    string Source,
    string Message);

public sealed record CpMarketingGrowthReviewDigest(
    long Id,
    string StrategyKey,
    string ReviewType,
    int Score,
    long CreatedAt,
    long CreatedBy);

public sealed record CpMarketingGrowthDigestResult(
    CpMarketingGrowthSummary Summary,
    IReadOnlyList<CpMarketingGrowthReviewDigest> Reviews,
    int Count,
    string Source,
    string Message);

public sealed record CpSoc2ComplianceSummary(
    int ControlCount,
    int ImplementedCount,
    int EvidenceCount,
    int PolicyCount,
    string Source,
    string Message);

public sealed record CpSoc2ControlDigest(
    long Id,
    string ControlId,
    string Category,
    string Title,
    string Status,
    string Owner,
    string Frequency,
    string RiskLevel);

public sealed record CpSoc2ComplianceDigestResult(
    CpSoc2ComplianceSummary Summary,
    IReadOnlyList<CpSoc2ControlDigest> Controls,
    int Count,
    string Source,
    string Message);

public sealed record CpCostModelsSummary(
    int ItemCount,
    int TxnCount,
    int CloseCount,
    int ModelCount,
    string Source,
    string Message);

public sealed record CpCostModelItemDigest(
    long Id,
    long CompanyId,
    long ItemId,
    string Model,
    decimal StdCost,
    long TimeUpdated);

public sealed record CpCostModelsDigestResult(
    CpCostModelsSummary Summary,
    IReadOnlyList<CpCostModelItemDigest> Items,
    int Count,
    string Source,
    string Message);

public sealed record CpFinAdvancedSummary(
    int PeriodCount,
    int OpenPeriodCount,
    int AllocRuleCount,
    int AccrualCount,
    string Source,
    string Message);

public sealed record CpFinPeriodDigest(
    long Id,
    long CompanyId,
    int Fy,
    int PeriodNo,
    long StartDate,
    long EndDate,
    string Status,
    long TimeCreated);

public sealed record CpFinAdvancedDigestResult(
    CpFinAdvancedSummary Summary,
    IReadOnlyList<CpFinPeriodDigest> Periods,
    int Count,
    string Source,
    string Message);

public sealed record CpBlockchainProofsSummary(
    int ProofCount,
    int PendingCount,
    int AnchoredCount,
    int BatchCount,
    string Source,
    string Message);

public sealed record CpBlockchainProofDigest(
    long Id,
    string ProofUid,
    string TenantKey,
    string RecordType,
    string RecordId,
    string PayloadHash,
    string Status,
    long? BatchId,
    string AnchorRef,
    string CreatedAt);

public sealed record CpBlockchainProofsDigestResult(
    CpBlockchainProofsSummary Summary,
    IReadOnlyList<CpBlockchainProofDigest> Proofs,
    int Count,
    string Source,
    string Message);

public sealed record CpLandedCostSummary(
    int SheetCount,
    int PostedCount,
    int ExpenseCount,
    int LineCount,
    string Source,
    string Message);

public sealed record CpLandedCostSheetDigest(
    long Id,
    long CompanyId,
    string SheetNo,
    string PoReference,
    string GrnReference,
    long SupplierId,
    string SupplierName,
    decimal GoodsValue,
    decimal TotalExpenses,
    string DistributionMethod,
    string Currency,
    string Status,
    long TimeCreated);

public sealed record CpLandedCostDigestResult(
    CpLandedCostSummary Summary,
    IReadOnlyList<CpLandedCostSheetDigest> Sheets,
    int Count,
    string Source,
    string Message);

public sealed record CpWarehouseWmsSummary(
    int LocationCount,
    int LpCount,
    int WaveCount,
    int OpenWorkCount,
    string Source,
    string Message);

public sealed record CpWarehouseWmsWorkDigest(
    long Id,
    long CompanyId,
    string WorkType,
    string Reference,
    long WaveId,
    string Item,
    decimal Qty,
    string Status,
    string AssignedTo,
    long TimeCreated);

public sealed record CpWarehouseWmsDigestResult(
    CpWarehouseWmsSummary Summary,
    IReadOnlyList<CpWarehouseWmsWorkDigest> Work,
    int Count,
    string Source,
    string Message);

public sealed record CpAiServiceSummary(
    int QueryCount,
    int SuccessCount,
    int BlockedCount,
    int ProviderCount,
    string Source,
    string Message);

public sealed record CpAiServiceQueryDigest(
    long Id,
    string SiteKey,
    long UserId,
    string Service,
    string Intent,
    int TokensUsed,
    int ExecutionMs,
    int PiiStripped,
    string Status,
    string CreatedAt);

public sealed record CpAiServiceDigestResult(
    CpAiServiceSummary Summary,
    IReadOnlyList<CpAiServiceQueryDigest> Queries,
    int Count,
    string Source,
    string Message);

public sealed record CpReturnsRmaSummary(
    int RmaCount,
    int OpenCount,
    int ActiveWarrantyCount,
    int ItemCount,
    string Source,
    string Message);

public sealed record CpReturnsRmaRequestDigest(
    long Id,
    string SiteKey,
    string RmaNumber,
    long? WarrantyId,
    long CustomerId,
    string CustomerName,
    string Reason,
    string Status,
    string ResolutionType,
    string CreatedAt);

public sealed record CpReturnsRmaDigestResult(
    CpReturnsRmaSummary Summary,
    IReadOnlyList<CpReturnsRmaRequestDigest> Requests,
    int Count,
    string Source,
    string Message);

public sealed record CpIsolationAuditSummary(
    int RunCount,
    int FailedRunCount,
    int ViolationCount,
    int SiteCount,
    string Source,
    string Message);

public sealed record CpIsolationAuditRunDigest(
    long Id,
    string RunAt,
    int TotalTenants,
    int Passed,
    int Failed,
    int Warnings,
    string TriggeredBy);

public sealed record CpIsolationAuditDigestResult(
    CpIsolationAuditSummary Summary,
    IReadOnlyList<CpIsolationAuditRunDigest> Runs,
    int Count,
    string Source,
    string Message);

public sealed record CpAmlComplianceSummary(
    int KycCount,
    int PendingKycCount,
    int FlaggedTxnCount,
    int ActiveRuleCount,
    string Source,
    string Message);

public sealed record CpAmlComplianceKycDigest(
    long Id,
    long CompanyId,
    long CustomerId,
    string CustomerName,
    string IdType,
    string RiskLevel,
    int PepStatus,
    string VerificationStatus,
    long TimeCreated);

public sealed record CpAmlComplianceDigestResult(
    CpAmlComplianceSummary Summary,
    IReadOnlyList<CpAmlComplianceKycDigest> Kyc,
    int Count,
    string Source,
    string Message);

public sealed record CpJewelleryMastersSummary(
    int KaratCount,
    int RateTypeCount,
    int BarcodeCount,
    int DiamondCount,
    string Source,
    string Message);

public sealed record CpJewelleryMastersKaratDigest(
    long Id,
    long CompanyId,
    string KaratCode,
    decimal StdPurity,
    decimal RangeFrom,
    decimal RangeTo,
    decimal SpGravity,
    string Division,
    string CreatedAt);

public sealed record CpJewelleryMastersDigestResult(
    CpJewelleryMastersSummary Summary,
    IReadOnlyList<CpJewelleryMastersKaratDigest> Karats,
    int Count,
    string Source,
    string Message);

public sealed record CpConsolidationsSummary(
    int EntityCount,
    int FigureCount,
    int IcCount,
    int OpenIcCount,
    string Source,
    string Message);

public sealed record CpConsolidationsEntityDigest(
    long Id,
    string Code,
    string Name,
    string CurrencyCode,
    decimal OwnershipPct,
    int IsHome,
    string ParentCode,
    int Active,
    long TimeCreated);

public sealed record CpConsolidationsDigestResult(
    CpConsolidationsSummary Summary,
    IReadOnlyList<CpConsolidationsEntityDigest> Entities,
    int Count,
    string Source,
    string Message);

public sealed record CpCrmActivitiesSummary(
    int ActivityCount,
    int OpenCount,
    int OverdueCount,
    int DoneCount,
    string Source,
    string Message);

public sealed record CpCrmActivitiesActivityDigest(
    long Id,
    string ActivityType,
    string RelatedType,
    long RelatedId,
    long DueDate,
    int Done,
    long OwnerUserId,
    long TimeCreated,
    int Active);

public sealed record CpCrmActivitiesDigestResult(
    CpCrmActivitiesSummary Summary,
    IReadOnlyList<CpCrmActivitiesActivityDigest> Activities,
    int Count,
    string Source,
    string Message);

public sealed record CpAuthMfaSummary(
    int SecretCount,
    int ConfirmedCount,
    int BackupUnusedCount,
    int PolicyCount,
    string Source,
    string Message);

public sealed record CpAuthMfaSecretDigest(
    long Id,
    long UserId,
    string Method,
    int Confirmed,
    string Label,
    string CreatedAt,
    string LastUsedAt);

public sealed record CpAuthMfaDigestResult(
    CpAuthMfaSummary Summary,
    IReadOnlyList<CpAuthMfaSecretDigest> Secrets,
    int Count,
    string Source,
    string Message);

public sealed record CpElectronicReportingSummary(
    int FormatCount,
    int FieldCount,
    int RunCount,
    int OutputTypeCount,
    string Source,
    string Message);

public sealed record CpElectronicReportingFormatDigest(
    long Id,
    long CompanyId,
    string Code,
    string Name,
    string OutputType,
    string RootElement,
    string RowElement,
    int Active,
    long TimeCreated);

public sealed record CpElectronicReportingDigestResult(
    CpElectronicReportingSummary Summary,
    IReadOnlyList<CpElectronicReportingFormatDigest> Formats,
    int Count,
    string Source,
    string Message);

public sealed record CpCollectionsDunningSummary(
    int QueueCount,
    int OpenCount,
    int ProfileCount,
    int LogCount,
    string Source,
    string Message);

public sealed record CpCollectionsDunningQueueDigest(
    long Id,
    string SiteKey,
    long CustomerId,
    string InvoiceRef,
    decimal InvoiceAmount,
    decimal AmountDue,
    string DueDate,
    int DaysOverdue,
    int DunningStep,
    string Status,
    string UpdatedAt);

public sealed record CpCollectionsDunningDigestResult(
    CpCollectionsDunningSummary Summary,
    IReadOnlyList<CpCollectionsDunningQueueDigest> Queue,
    int Count,
    string Source,
    string Message);

public sealed record CpMarketplaceChannelsSummary(
    int ChannelCount,
    int ActiveCount,
    int SkuMapCount,
    int OrderCount,
    string Source,
    string Message);

public sealed record CpMarketplaceChannelsChannelDigest(
    long Id,
    string Code,
    string Name,
    string MarketplaceId,
    int Active,
    int DemoMode,
    long LastSyncAt,
    long TimeCreated,
    string Family,
    string Region,
    string Api,
    string Blurb);

public sealed record CpMarketplaceChannelsDigestResult(
    CpMarketplaceChannelsSummary Summary,
    IReadOnlyList<CpMarketplaceChannelsChannelDigest> Channels,
    int Count,
    string Source,
    string Message);

public sealed record CpDemandIntelligenceSummary(
    int CountryCount,
    int ArticleDemandCount,
    int PriceListDemandCount,
    int UserDemandCount,
    string Source,
    string Message);

public sealed record CpDemandIntelligenceCountryDigest(
    string Code,
    string Name,
    int SortOrder);

public sealed record CpDemandIntelligenceDigestResult(
    CpDemandIntelligenceSummary Summary,
    IReadOnlyList<CpDemandIntelligenceCountryDigest> Countries,
    int Count,
    string Source,
    string Message);

public sealed record CpCreditLimitsSummary(
    int LimitCount,
    int ActiveCount,
    int HeldCount,
    int TxnCount,
    string Source,
    string Message);

public sealed record CpCreditLimitsLimitDigest(
    long Id,
    string SiteKey,
    long CustomerId,
    decimal CreditLimit,
    decimal BalanceUsed,
    string Currency,
    string Status,
    int RiskScore,
    string PaymentTerms,
    string UpdatedAt);

public sealed record CpCreditLimitsDigestResult(
    CpCreditLimitsSummary Summary,
    IReadOnlyList<CpCreditLimitsLimitDigest> Limits,
    int Count,
    string Source,
    string Message);

public sealed record CpInsuranceComplianceSummary(
    int PolicyCount,
    int ActiveCount,
    int ClaimCount,
    int DocumentCount,
    string Source,
    string Message);

public sealed record CpInsuranceCompliancePolicyDigest(
    long Id,
    long CompanyId,
    string PolicyNo,
    string Class,
    string Title,
    string Insurer,
    decimal SumInsured,
    decimal Premium,
    string Currency,
    long ExpiryDate,
    string Status,
    long TimeCreated);

public sealed record CpInsuranceComplianceDigestResult(
    CpInsuranceComplianceSummary Summary,
    IReadOnlyList<CpInsuranceCompliancePolicyDigest> Policies,
    int Count,
    string Source,
    string Message);

public sealed record CpAuditTrailSummary(
    int EntryCount,
    int ActionCount,
    int AdminCount,
    int EntityTypeCount,
    string Source,
    string Message);

public sealed record CpAuditTrailEntryDigest(
    long Id,
    long TimeUnix,
    long AdminId,
    string Action,
    string EntityType,
    long EntityId,
    string Summary);

public sealed record CpAuditTrailDigestResult(
    CpAuditTrailSummary Summary,
    IReadOnlyList<CpAuditTrailEntryDigest> Entries,
    int Count,
    string Source,
    string Message);

public sealed record CpDocExpirySummary(
    int DocumentCount,
    int ActiveCount,
    int ExpiredCount,
    int ReminderCount,
    string Source,
    string Message);

public sealed record CpDocExpiryDocumentDigest(
    long Id,
    long CompanyId,
    string Category,
    string DocType,
    string Title,
    string RefNo,
    string Owner,
    string Issuer,
    long ExpiryDate,
    string SourceModule,
    int Active,
    long TimeCreated);

public sealed record CpDocExpiryDigestResult(
    CpDocExpirySummary Summary,
    IReadOnlyList<CpDocExpiryDocumentDigest> Documents,
    int Count,
    string Source,
    string Message);

public sealed record CpTenantConfigSummary(
    int ConfigCount,
    int GroupCount,
    int EditableCount,
    int HistoryCount,
    string Source,
    string Message);

public sealed record CpTenantConfigEntryDigest(
    long Id,
    string SiteKey,
    string ConfigGroup,
    string ConfigKey,
    string ValueType,
    string Label,
    int Editable,
    long UpdatedBy,
    string UpdatedAt);

public sealed record CpTenantConfigDigestResult(
    CpTenantConfigSummary Summary,
    IReadOnlyList<CpTenantConfigEntryDigest> Entries,
    int Count,
    string Source,
    string Message);

public sealed record CpJewelleryStockVerificationSummary(
    int VerificationCount,
    int InProgressCount,
    int CompleteCount,
    int LineCount,
    string Source,
    string Message);

public sealed record CpJewelleryStockVerificationRowDigest(
    long Id,
    long CompanyId,
    string Branch,
    string VocType,
    string VocDate,
    long VocNo,
    string Location,
    int TotalPcs,
    int ScannedPcs,
    int RemainingPcs,
    string Status,
    string CreatedBy);

public sealed record CpJewelleryStockVerificationDigestResult(
    CpJewelleryStockVerificationSummary Summary,
    IReadOnlyList<CpJewelleryStockVerificationRowDigest> Verifications,
    int Count,
    string Source,
    string Message);
public sealed record CpTaxExternalReportingSummary(
    int RuleCount,
    int ActiveCount,
    int StagingCount,
    int AuditCount,
    string Source,
    string Message);

public sealed record CpTaxExternalReportingRowDigest(
    long Id,
    string Country,
    string RuleKey,
    long Version,
    string Status,
    string RuleSource,
    long ValidFrom,
    long ValidTo);

public sealed record CpTaxExternalReportingDigestResult(
    CpTaxExternalReportingSummary Summary,
    IReadOnlyList<CpTaxExternalReportingRowDigest> Rules,
    int Count,
    string Source,
    string Message);

public sealed record CpPoApprovalsSummary(
    int RequestCount,
    int PendingCount,
    int ApprovedCount,
    int StepCount,
    string Source,
    string Message);

public sealed record CpPoApprovalsRowDigest(
    long Id,
    string SiteKey,
    string PoNumber,
    long RequesterId,
    string VendorName,
    string Currency,
    decimal Total,
    string Status,
    int CurrentTier,
    string Priority,
    string CreatedAt);

public sealed record CpPoApprovalsDigestResult(
    CpPoApprovalsSummary Summary,
    IReadOnlyList<CpPoApprovalsRowDigest> Requests,
    int Count,
    string Source,
    string Message);

public sealed record CpFinanceCloseSummary(
    int BatchCount,
    int PostedBatchCount,
    int OpeningLineCount,
    int PeriodCount,
    int ClosedPeriodCount,
    int CloseLogCount,
    string Source,
    string Message);

public sealed record CpFinanceCloseRowDigest(
    long Id,
    string Module,
    string AsOfDate,
    string Reference,
    string Status,
    long AdminId,
    long TimeCreated,
    long TimePosted);

public sealed record CpFinanceCloseDigestResult(
    CpFinanceCloseSummary Summary,
    IReadOnlyList<CpFinanceCloseRowDigest> Batches,
    int Count,
    string Source,
    string Message);

public sealed record CpJewelleryFixingSummary(
    int FixingCount,
    int OpenFixingCount,
    int PurchaseFixCount,
    int SettlementCount,
    int PettyCashCount,
    string Source,
    string Message);

public sealed record CpJewelleryFixingRowDigest(
    long Id,
    long CompanyId,
    string Branch,
    string FixType,
    string FixDate,
    long FixNo,
    string PartyCode,
    string PartyName,
    string Metal,
    string Karat,
    decimal FixQtyGms,
    decimal FixAmount,
    string Status,
    string CreatedBy);

public sealed record CpJewelleryFixingDigestResult(
    CpJewelleryFixingSummary Summary,
    IReadOnlyList<CpJewelleryFixingRowDigest> Fixings,
    int Count,
    string Source,
    string Message);

public sealed record CpWebTrackerSummary(
    int SessionCount,
    int PageviewCount,
    int EventCount,
    int CountryCount,
    string Source,
    string Message);

public sealed record CpWebTrackerRowDigest(
    long Id,
    string SessionUid,
    string SiteKey,
    long PageviewCount,
    long EventCount,
    string CountryCode,
    string DeviceType,
    string Browser,
    long FirstSeenAt,
    long LastSeenAt);

public sealed record CpWebTrackerDigestResult(
    CpWebTrackerSummary Summary,
    IReadOnlyList<CpWebTrackerRowDigest> Sessions,
    int Count,
    string Source,
    string Message);

public sealed record CpAbandonedCartsSummary(
    int LineCount,
    int GuestLineCount,
    int UserLineCount,
    int GuestSessionCount,
    int UserCartCount,
    decimal CartSum,
    string Source,
    string Message);

public sealed record CpAbandonedCartsRowDigest(
    long Id,
    long UserId,
    long SessionId,
    decimal Price,
    int CountNeed,
    int CheckedForOrder,
    int ProductType,
    string Manufacturer,
    string Article,
    string Name,
    long TimeUnix,
    decimal PriceSum);

public sealed record CpAbandonedCartsDigestResult(
    CpAbandonedCartsSummary Summary,
    IReadOnlyList<CpAbandonedCartsRowDigest> Carts,
    int Count,
    string Source,
    string Message);

public sealed record CpQuoteRequestsSummary(
    int QuoteCount,
    int DraftCount,
    int SubmittedCount,
    int QuotedCount,
    int AcceptedCount,
    int ItemCount,
    string Source,
    string Message);

public sealed record CpQuoteRequestsRowDigest(
    long Id,
    long UserId,
    long SessionId,
    string Status,
    long TimeCreated,
    long TimeUpdated,
    long TimeSubmitted,
    long AcceptedOrderId);

public sealed record CpQuoteRequestsDigestResult(
    CpQuoteRequestsSummary Summary,
    IReadOnlyList<CpQuoteRequestsRowDigest> Quotes,
    int Count,
    string Source,
    string Message);

public sealed record CpPlatformCommunicationSummary(
    int SettingCount,
    int TaskCount,
    int OpenTaskCount,
    int HighPriorityCount,
    string Source,
    string Message);

public sealed record CpPlatformCommunicationRowDigest(
    long Id,
    string Title,
    long AssignedTo,
    string SiteKey,
    string Category,
    string Status,
    string Priority,
    long DueAt,
    long CreatedAt);

public sealed record CpPlatformCommunicationDigestResult(
    CpPlatformCommunicationSummary Summary,
    IReadOnlyList<CpPlatformCommunicationRowDigest> Tasks,
    int Count,
    string Source,
    string Message);

public sealed record CpInfoBlocksSummary(
    int BlockCount,
    int ActiveCount,
    int PlacementCount,
    int LocaleCount,
    string Source,
    string Message);

public sealed record CpInfoBlocksRowDigest(
    long Id,
    string BlockKey,
    string Title,
    string Scope,
    string SiteKey,
    string Placement,
    string Locale,
    int Active,
    int SortOrder,
    long UpdatedAt);

public sealed record CpInfoBlocksDigestResult(
    CpInfoBlocksSummary Summary,
    IReadOnlyList<CpInfoBlocksRowDigest> Blocks,
    int Count,
    string Source,
    string Message);

public sealed record CpFreeToolsSummary(
    int AccountCount,
    int SaveCount,
    int SettingCount,
    int ActiveAccountCount,
    string Source,
    string Message);

public sealed record CpFreeToolsRowDigest(
    long Id,
    string Email,
    string Company,
    string Country,
    long UseCount,
    long LoginCount,
    long TimeCreated,
    long TimeLastSeen);

public sealed record CpFreeToolsDigestResult(
    CpFreeToolsSummary Summary,
    IReadOnlyList<CpFreeToolsRowDigest> Accounts,
    int Count,
    string Source,
    string Message);

public sealed record CpConfigSandboxSummary(
    int SnapshotCount,
    int ActiveSnapshotCount,
    int PromotedSnapshotCount,
    int ChangeCount,
    string Source,
    string Message);

public sealed record CpConfigSandboxRowDigest(
    long Id,
    string SiteKey,
    string SnapshotName,
    string Status,
    long CreatedBy,
    string CreatedAt,
    string PromotedAt);

public sealed record CpConfigSandboxDigestResult(
    CpConfigSandboxSummary Summary,
    IReadOnlyList<CpConfigSandboxRowDigest> Snapshots,
    int Count,
    string Source,
    string Message);

public sealed record CpMarketplaceAppsSummary(
    int AppCount,
    int PublishedCount,
    int InstallCount,
    int ActiveInstallCount,
    int ReviewCount,
    string Source,
    string Message);

public sealed record CpMarketplaceAppsRowDigest(
    long Id,
    string AppKey,
    string Name,
    string ShortDesc,
    string Category,
    string Developer,
    string Version,
    string Pricing,
    decimal PriceMonthly,
    long Downloads,
    decimal AvgRating,
    long ReviewCount,
    string Status,
    string PublishedAt);

public sealed record CpMarketplaceAppsDigestResult(
    CpMarketplaceAppsSummary Summary,
    IReadOnlyList<CpMarketplaceAppsRowDigest> Apps,
    int Count,
    string Source,
    string Message);

public sealed record CpNotificationsSummary(
    int NotificationCount,
    int UnreadCount,
    int PrefCount,
    int ChannelCount,
    string Source,
    string Message);

public sealed record CpNotificationsRowDigest(
    long Id,
    string TenantKey,
    long UserId,
    string Channel,
    string Category,
    string Severity,
    string Title,
    int IsRead,
    string CreatedAt);

public sealed record CpNotificationsDigestResult(
    CpNotificationsSummary Summary,
    IReadOnlyList<CpNotificationsRowDigest> Notifications,
    int Count,
    string Source,
    string Message);

public sealed record CpPortalSettingsSummary(
    int SiteCount,
    int IndustryCount,
    int AccessModeCount,
    int DeployTargetCount,
    string Source,
    string Message);

public sealed record CpPortalSettingsRowDigest(
    string Host,
    string IndustryCode,
    string SystemName,
    string HubName,
    string Tagline,
    string DomainPath,
    string ThemeTemplate,
    string AccessMode,
    string CpDefaultLang,
    string CountryCode,
    long UpdatedAt);

public sealed record CpPortalSettingsDigestResult(
    CpPortalSettingsSummary Summary,
    IReadOnlyList<CpPortalSettingsRowDigest> Sites,
    int Count,
    string Source,
    string Message);

public sealed record CpDataMigrationsSummary(
    int MigrationCount,
    int CompletedCount,
    int FailedCount,
    int RowCount,
    string Source,
    string Message);

public sealed record CpDataMigrationsRowDigest(
    long Id,
    long CompanyId,
    string MigrationType,
    string EntityType,
    string FileName,
    long TotalRows,
    long ValidRows,
    long ErrorRows,
    long ImportedRows,
    string Status,
    string ImportedByName,
    long TimeCreated,
    long TimeCompleted);

public sealed record CpDataMigrationsDigestResult(
    CpDataMigrationsSummary Summary,
    IReadOnlyList<CpDataMigrationsRowDigest> Migrations,
    int Count,
    string Source,
    string Message);

// ---- Wave 22 CMS/platform leftover digests ----
public sealed record CpGeoRegionsSummary(
    int NodeCount,
    int Level1Count,
    int Level2Count,
    int MappedOfficeCount,
    string Source,
    string Message);

public sealed record CpGeoRegionsRowDigest(
    long Id,
    int Level,
    long Parent,
    int SortOrder,
    int ChildCount,
    long ValueLangId);

public sealed record CpGeoRegionsDigestResult(
    CpGeoRegionsSummary Summary,
    IReadOnlyList<CpGeoRegionsRowDigest> Nodes,
    int Count,
    string Source,
    string Message);

public sealed record CpProductFiltersSummary(
    int FilterCount,
    int WithStorageScope,
    int WithPriceBand,
    int WithTimeBand,
    string Source,
    string Message);

public sealed record CpProductFiltersRowDigest(
    long Id,
    string Manufacturer,
    string Article,
    string Name,
    decimal MinPrice,
    decimal MaxPrice,
    int MinTime,
    int MaxTime);

public sealed record CpProductFiltersDigestResult(
    CpProductFiltersSummary Summary,
    IReadOnlyList<CpProductFiltersRowDigest> Filters,
    int Count,
    string Source,
    string Message);

public sealed record CpSearchTabsSummary(
    int TabCount,
    int EnabledCount,
    int DisabledCount,
    int MaxOrder,
    string Source,
    string Message);

public sealed record CpSearchTabsRowDigest(
    long Id,
    string Caption,
    int SortOrder,
    int Enabled);

public sealed record CpSearchTabsDigestResult(
    CpSearchTabsSummary Summary,
    IReadOnlyList<CpSearchTabsRowDigest> Tabs,
    int Count,
    string Source,
    string Message);

public sealed record CpSystemRequestsSummary(
    int RequestCount,
    int UnviewedCount,
    int ViewedCount,
    int WithUserCount,
    string Source,
    string Message);

public sealed record CpSystemRequestsRowDigest(
    long Id,
    long TimeUnix,
    long UserId,
    int Viewed);

public sealed record CpSystemRequestsDigestResult(
    CpSystemRequestsSummary Summary,
    IReadOnlyList<CpSystemRequestsRowDigest> Requests,
    int Count,
    string Source,
    string Message);

public sealed record CpAdditionalTextsSummary(
    int TextCount,
    int BeforeMainCount,
    int WithTitleCount,
    int WithDescriptionCount,
    string Source,
    string Message);

public sealed record CpAdditionalTextsRowDigest(
    long Id,
    string Url,
    int BeforeMain,
    string TitleTag,
    string KeywordsTag);

public sealed record CpAdditionalTextsDigestResult(
    CpAdditionalTextsSummary Summary,
    IReadOnlyList<CpAdditionalTextsRowDigest> Texts,
    int Count,
    string Source,
    string Message);

public sealed record CpSliderBannersSummary(
    int ImageCount,
    int Connected,
    int CntImg,
    int CntImgNext,
    string Source,
    string Message);

public sealed record CpSliderBannersRowDigest(
    long Id,
    int SortOrder,
    string Link,
    string Href);

public sealed record CpSliderBannersDigestResult(
    CpSliderBannersSummary Summary,
    IReadOnlyList<CpSliderBannersRowDigest> Images,
    int Count,
    string Source,
    string Message);

public sealed record CpStructureDumpsSummary(
    int DumpCount,
    int TotalRecords,
    int LatestTimeCreated,
    int WithFileCount,
    string Source,
    string Message);

public sealed record CpStructureDumpsRowDigest(
    long Id,
    long TimeCreated,
    string FieldsInDump,
    string FileName,
    long RecordsCount);

public sealed record CpStructureDumpsDigestResult(
    CpStructureDumpsSummary Summary,
    IReadOnlyList<CpStructureDumpsRowDigest> Dumps,
    int Count,
    string Source,
    string Message);

public sealed record CpCommunicationsTestSummary(
    int SmsActiveCount,
    int SmsTotalCount,
    string EmailLastStatus,
    string SmsLastStatus,
    string Source,
    string Message);

public sealed record CpCommunicationsTestRowDigest(
    string Name,
    int Active,
    int IsSelectable,
    string Handler);

public sealed record CpCommunicationsTestDigestResult(
    CpCommunicationsTestSummary Summary,
    IReadOnlyList<CpCommunicationsTestRowDigest> Channels,
    int Count,
    string Source,
    string Message);

public sealed record CpLanguagesSummary(
    int LanguageCount,
    int ActiveCount,
    int DefaultCount,
    int InactiveCount,
    string Source,
    string Message);

public sealed record CpLanguagesRowDigest(
    string LangCode,
    int Active,
    int IsDefault);

public sealed record CpLanguagesDigestResult(
    CpLanguagesSummary Summary,
    IReadOnlyList<CpLanguagesRowDigest> Languages,
    int Count,
    string Source,
    string Message);

public sealed record CpPluginsManagerSummary(
    int PluginCount,
    int ActivatedCount,
    int FrontendCount,
    int LockedCount,
    string Source,
    string Message);

public sealed record CpPluginsManagerRowDigest(
    long Id,
    string Caption,
    int SortOrder,
    int Activated,
    int IsFrontend,
    int ControlLock);

public sealed record CpPluginsManagerDigestResult(
    CpPluginsManagerSummary Summary,
    IReadOnlyList<CpPluginsManagerRowDigest> Plugins,
    int Count,
    string Source,
    string Message);

public sealed record CpTemplatesManagerSummary(
    int TemplateCount,
    int FrontendCount,
    int CurrentFrontendCount,
    int CurrentBackendCount,
    string Source,
    string Message);

public sealed record CpTemplatesManagerRowDigest(
    long Id,
    string Caption,
    string Name,
    int Current,
    int IsFrontend,
    int PhoneSupport,
    int TabletSupport);

public sealed record CpTemplatesManagerDigestResult(
    CpTemplatesManagerSummary Summary,
    IReadOnlyList<CpTemplatesManagerRowDigest> Templates,
    int Count,
    string Source,
    string Message);

public sealed record CpDesignTokensSummary(
    int TokenCount,
    int TenantCount,
    int WhiteLabelCount,
    int UpdatedRecentCount,
    string Source,
    string Message);

public sealed record CpDesignTokensRowDigest(
    string SiteKey,
    string SettingKey,
    string UpdatedAt);

public sealed record CpDesignTokensDigestResult(
    CpDesignTokensSummary Summary,
    IReadOnlyList<CpDesignTokensRowDigest> Tokens,
    int Count,
    string Source,
    string Message);

public sealed record CpSitemapSummary(
    int ContentUrlCount,
    int CategoryCount,
    int ProductCount,
    int FrontendContentCount,
    string Source,
    string Message);

public sealed record CpSitemapRowDigest(
    long Id,
    string Alias,
    long ValueLangId,
    int IsFrontend,
    int PublishedFlag);

public sealed record CpSitemapDigestResult(
    CpSitemapSummary Summary,
    IReadOnlyList<CpSitemapRowDigest> Pages,
    int Count,
    string Source,
    string Message);

public sealed record CpFailoverStatusSummary(
    int ModeFilePresent,
    int StatusJsonPresent,
    int ConfigPresent,
    int BackupMode,
    string Source,
    string Message);

public sealed record CpFailoverStatusRowDigest(
    string Path,
    int Present,
    string Kind);

public sealed record CpFailoverStatusDigestResult(
    CpFailoverStatusSummary Summary,
    IReadOnlyList<CpFailoverStatusRowDigest> Signals,
    int Count,
    string Source,
    string Message);

public sealed record CpOpsGuidesSummary(
    int GroupCount,
    int ItemCount,
    int ShowAnywayCount,
    int UrlItemCount,
    string Source,
    string Message);

public sealed record CpOpsGuidesRowDigest(
    long Id,
    long ItemsGroup,
    string Caption,
    string Url,
    int ShowAnyway,
    int SortOrder);

public sealed record CpOpsGuidesDigestResult(
    CpOpsGuidesSummary Summary,
    IReadOnlyList<CpOpsGuidesRowDigest> Items,
    int Count,
    string Source,
    string Message);

public sealed record CpFileManagerSummary(
    int RootPresent,
    int FileCount,
    int DirCount,
    long TotalBytes,
    string Source,
    string Message);

public sealed record CpFileManagerRowDigest(
    string Name,
    int IsDirectory,
    long SizeBytes,
    string Extension);

public sealed record CpFileManagerDigestResult(
    CpFileManagerSummary Summary,
    IReadOnlyList<CpFileManagerRowDigest> Entries,
    int Count,
    string Source,
    string Message);

public sealed record CpServerIpSummary(
    int AddressCount,
    int HasIpv4,
    int HasIpv6,
    int LoopbackOnly,
    string Source,
    string Message);

public sealed record CpServerIpRowDigest(
    string Address,
    string AddressFamily,
    int IsLoopback);

public sealed record CpServerIpDigestResult(
    CpServerIpSummary Summary,
    IReadOnlyList<CpServerIpRowDigest> Addresses,
    int Count,
    string Source,
    string Message);

public sealed record CpDebugConsoleSummary(
    int FileCount,
    int TmpRootPresent,
    int AllowlistOnly,
    long TotalBytes,
    string Source,
    string Message);

public sealed record CpDebugConsoleRowDigest(
    string Basename,
    long SizeBytes,
    long LastModifiedUnix,
    int Allowlisted);

public sealed record CpDebugConsoleDigestResult(
    CpDebugConsoleSummary Summary,
    IReadOnlyList<CpDebugConsoleRowDigest> Files,
    int Count,
    string Source,
    string Message);

/// <summary>On-premises license digest row — license_key is masked; notes/fingerprint/ip omitted.</summary>
public sealed record OnPremisesLicenseDigest(
    long Id,
    string LicenseKeyPreview,
    string CustomerName,
    string Tier,
    int UsersMax,
    string Status,
    string Hostname,
    long IssuedAt,
    long ActivatedAt,
    long LastSeenAt,
    long ExpiresAt);

public sealed record OnPremisesLicenseListResult(
    IReadOnlyList<OnPremisesLicenseDigest> Licenses,
    int Count,
    string Source,
    string Message);

public sealed record CpStatisticsSummary(int OrderCount, int QueryCount, int UniqueArticles, int ActiveDays, string Source, string Message);
public sealed record CpStatisticsRowDigest(string Article, string Brand, int Hits, long LastSeen);
public sealed record CpStatisticsDigestResult(CpStatisticsSummary Summary, IReadOnlyList<CpStatisticsRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpAccessoriesSummary(int ListingCount, int PublishedCount, int CategoryCount, int PhotoCount, string Source, string Message);
public sealed record CpAccessoriesRowDigest(int Id, string Title, string Make, string Model, decimal Price, string Status);
public sealed record CpAccessoriesDigestResult(CpAccessoriesSummary Summary, IReadOnlyList<CpAccessoriesRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpSynonymsSummary(int ManufacturerCount, int SynonymCount, int OrphanCount, int MappedCount, string Source, string Message);
public sealed record CpSynonymsRowDigest(string Manufacturer, string Synonym, int ManufacturerId);
public sealed record CpSynonymsDigestResult(CpSynonymsSummary Summary, IReadOnlyList<CpSynonymsRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpSeoSummary(int UrlCount, int IndexedReady, int PingJobs, int WarmJobs, string Source, string Message);
public sealed record CpSeoRowDigest(string Key, string Value);
public sealed record CpSeoDigestResult(CpSeoSummary Summary, IReadOnlyList<CpSeoRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpSocialHubSummary(int AccountCount, int DraftCount, int PublishedCount, int ErrorCount, string Source, string Message);
public sealed record CpSocialHubRowDigest(string Platform, string Username, string Status, string Title, string DraftStatus);
public sealed record CpSocialHubDigestResult(CpSocialHubSummary Summary, IReadOnlyList<CpSocialHubRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpTenantFeaturesSummary(int SiteCount, int FlagCount, int EnabledCount, int DisabledCount, string Source, string Message);
public sealed record CpTenantFeaturesRowDigest(string SiteKey, string FeatureKey, bool Enabled, long UpdatedAt);
public sealed record CpTenantFeaturesDigestResult(CpTenantFeaturesSummary Summary, IReadOnlyList<CpTenantFeaturesRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpCustomerBoardSummary(int UserCount, int WithEmail, int WithPhone, int RecentLogins, string Source, string Message);
public sealed record CpCustomerBoardRowDigest(int Id, string Email, string Name, string Phone, long RegTime);
public sealed record CpCustomerBoardDigestResult(CpCustomerBoardSummary Summary, IReadOnlyList<CpCustomerBoardRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpFulfillmentQueueSummary(int Queued, int Picking, int Shipping, int Delivered, string Source, string Message);
public sealed record CpFulfillmentQueueRowDigest(long Id, string OrderNumber, string CustomerName, string Status, string Priority, string Warehouse, string Carrier);
public sealed record CpFulfillmentQueueDigestResult(CpFulfillmentQueueSummary Summary, IReadOnlyList<CpFulfillmentQueueRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpSsoSamlSummary(int ProviderCount, int ActiveProviders, int SessionCount, int ActiveSessions, string Source, string Message);
public sealed record CpSsoSamlRowDigest(string ProviderName, string ProviderType, bool Active, string Email, string Status);
public sealed record CpSsoSamlDigestResult(CpSsoSamlSummary Summary, IReadOnlyList<CpSsoSamlRowDigest> Rows, int Count, string Source, string Message);

public sealed record CpEventBusSummary(int EventCount, int TypeCount, int TenantCount, int Last24h, string Source, string Message);
public sealed record CpEventBusRowDigest(long Id, string EventType, string TenantKey, string ActorType, string CreatedAt);
public sealed record CpEventBusDigestResult(CpEventBusSummary Summary, IReadOnlyList<CpEventBusRowDigest> Rows, int Count, string Source, string Message);

public sealed record ErpDeliveryNoteDigest(
    long Id,
    string NoteNo,
    long OrderId,
    string Carrier,
    string TrackingNo,
    string Status,
    long ShippedAt,
    long DeliveredAt,
    long TimeCreated);

public sealed record ErpDeliveryNoteListResult(
    IReadOnlyList<ErpDeliveryNoteDigest> Notes,
    int Count,
    string Source,
    string Message);

public sealed record ErpRfqDigest(
    long Id,
    string RfqNo,
    long SupplierId,
    string Title,
    decimal AmountEst,
    string CurrencyCode,
    string Status,
    long DueDate,
    long OrderId,
    long TimeCreated);

public sealed record ErpRfqListResult(
    IReadOnlyList<ErpRfqDigest> Rfqs,
    int Count,
    string Source,
    string Message);

public sealed record ErpThreeWayMatchDigest(
    long PoId,
    string PoNo,
    string PoStatus,
    decimal PoTotal,
    long PurchaseId,
    string InvoiceNumber,
    decimal InvoiceTotal,
    string PurchaseStatus,
    int ReceiptCount);

public sealed record ErpThreeWayMatchListResult(
    IReadOnlyList<ErpThreeWayMatchDigest> Rows,
    int Count,
    string Source,
    string Message);

public sealed record ErpContactDigest(
    long Id,
    string PartyType,
    string Name,
    string Company,
    string Email,
    string Phone,
    string Trn,
    string City,
    string CountryCode,
    long LinkedUserId,
    long LinkedSupplierId,
    bool Active,
    long TimeUpdated);

public sealed record ErpContactListResult(
    IReadOnlyList<ErpContactDigest> Contacts,
    int Count,
    string Source,
    string Message);

public sealed record ErpPaymentBatchDigest(
    long Id,
    string BatchNo,
    string BatchType,
    long AccountId,
    string AccountName,
    decimal TotalAmount,
    int LineCount,
    string Status,
    long ExecutionDate,
    long TimeUpdated);

public sealed record ErpPaymentBatchListResult(
    IReadOnlyList<ErpPaymentBatchDigest> Batches,
    int Count,
    string Source,
    string Message);

public sealed record ErpFiscalPeriodDigest(
    long Id,
    string YearMonth,
    string Status,
    bool SoftClosed,
    bool Locked,
    long TimeUpdated);

public sealed record ErpFiscalPeriodListResult(
    IReadOnlyList<ErpFiscalPeriodDigest> Periods,
    int Count,
    string Source,
    string Message);

public sealed record ErpAgendaEventDigest(
    long Id,
    string Title,
    string EventType,
    long StartAt,
    long EndAt,
    string EntityType,
    long EntityId,
    string Location,
    long TimeCreated);

public sealed record ErpAgendaEventListResult(
    IReadOnlyList<ErpAgendaEventDigest> Events,
    int Count,
    string Source,
    string Message);

public sealed record ErpDocumentDigest(
    long Id,
    string EntityType,
    long EntityId,
    string DocCategory,
    string FileName,
    long FileSize,
    string MimeType,
    long TimeCreated);

public sealed record ErpDocumentListResult(
    IReadOnlyList<ErpDocumentDigest> Documents,
    int Count,
    string Source,
    string Message);

public sealed record ErpExpenseReportDigest(
    long Id,
    string ReportNo,
    long StaffUserId,
    string Title,
    decimal TotalAmount,
    string Status,
    long PeriodFrom,
    long PeriodTo,
    long TimeUpdated);

public sealed record ErpExpenseReportListResult(
    IReadOnlyList<ErpExpenseReportDigest> Reports,
    int Count,
    string Source,
    string Message);
