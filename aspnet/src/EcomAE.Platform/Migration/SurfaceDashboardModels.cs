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
    decimal OrderSum);

public sealed record CpOrdersSummary(
    int Open,
    int Today,
    int PendingShip,
    string Source,
    string Message);

public sealed record CpOrdersListResult(
    CpOrdersSummary Summary,
    IReadOnlyList<CpShopOrderDigest> Orders,
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

public sealed record ErpCoaAccountDigest(
    long Id,
    string Code,
    string Name,
    string AccountType,
    string NormalSide,
    long ParentId,
    decimal OpeningBalance,
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
    int RateCount,
    int OpenShipments,
    string Source,
    string Message);

public sealed record CpCarrierDigest(
    long Id,
    string Code,
    string Name,
    string Mode,
    string Currency,
    decimal Rating,
    bool Active);

public sealed record CpCarriersDigestResult(
    CpCarriersSummary Summary,
    IReadOnlyList<CpCarrierDigest> Carriers,
    int Count,
    string Source,
    string Message);

public sealed record CpPaymentGatewaysSummary(
    int GatewayCount,
    int ActiveGateways,
    int SelectableGateways,
    int AccountCount,
    string Source,
    string Message);

public sealed record CpPaymentGatewayDigest(
    long Id,
    string Name,
    string Handler,
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

public sealed record CpIntegrationsSummary(
    int WebhookCount,
    int ActiveWebhooks,
    int DeliveryCount,
    int FailedDeliveries,
    string Source,
    string Message);

public sealed record CpIntegrationDigest(
    long Id,
    string TenantKey,
    string Url,
    bool Active,
    string Description,
    string CreatedAt);

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
    string Storage);

public sealed record StorefrontPartSearchResult(
    string Article,
    IReadOnlyList<StorefrontPartOfferDigest> Rows,
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
    decimal MinOrder);

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
    string CustomerName,
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
    long TimeCreated);

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
    string Summary,
    string IpAddress);

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

public sealed record CpQuoteRequestsSummary(
    int QuoteCount,
    int DraftCount,
    int SubmittedCount,
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

