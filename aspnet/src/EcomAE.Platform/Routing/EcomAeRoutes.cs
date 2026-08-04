namespace EcomAE.Platform.Routing;

public static class EcomAeRoutes
{
    public const string Health = "/health";
    public const string MigrationStatus = "/migration/status";
    public const string MigrationReadiness = "/migration/readiness";
    public const string MigrationCutoverPlan = "/migration/cutover-plan";
    public const string MigrationProgress = "/migration/progress";
    public const string ZeroPhpCompletion = "/migration/zero-php-completion";
    public const string PhpDecommissionReadiness = "/migration/php-decommission-readiness";
    public const string PythonSidecars = "/migration/python-sidecars";
    public const string MigrationRouteCutover = "/migration/route-cutover";
    public const string MigrationDataParity = "/migration/data-parity";
    public const string MigrationCutoverValidation = "/migration/cutover-validation";
    public const string MigrationUmapiUsage = "/migration/umapi-usage";
    public const string MigrationPlatformJobs = "/migration/platform-jobs";
    public const string SurfaceParity = "/migration/surface-parity";
    public const string PresentationParity = "/migration/presentation-parity";
    public const string PhpModuleCatalog = "/migration/php-module-catalog";
    public const string LiveSurfaceLinks = "/migration/live-surface-links";
    public const string MigrationConsole = "/migration/console";
    public const string SurfaceFieldParity = "/migration/surface-field-parity";
    public const string TenantContext = "/tenant/context";
    public const string TenantWorkspaceParity = "/tenant/workspace/parity";
    public const string LegacySessionProbe = "/auth/session/probe";
    public const string LegacySessionParity = "/auth/session/parity";
    public const string LegacyAdminLogin = "/auth/login/admin";
    public const string LegacyApiClientParity = "/auth/api-client/parity";
    public const string ControlPanel = "/cp";
    public const string ControlPanelApp = "/cp/app";
    public const string ControlPanelLogin = "/cp/login";
    public const string ErpLogin = "/erp/login";
    public const string BosLogin = "/bos/login";
    public const string StorefrontLogin = "/storefront/login";

    public const string ControlPanelParity = "/cp/parity";
    public const string ControlPanelDashboardSummary = "/cp/dashboard-summary";
    /// <summary>CP dashboard summary Blazor KPI UI (JSON digest remains <see cref="ControlPanelDashboardSummary"/>).</summary>
    public const string ControlPanelDashboardSummaryApp = "/cp/dashboard-summary-app";
    public const string ControlPanelTenants = "/cp/tenants";
    /// <summary>CP portal tenants Blazor list (JSON digest remains <see cref="ControlPanelTenants"/>).</summary>
    public const string ControlPanelTenantsApp = "/cp/tenants-app";
    public const string ControlPanelUsers = "/cp/users";
    public const string ControlPanelGroups = "/cp/groups";
    public const string ControlPanelModules = "/cp/modules";
    /// <summary>CP modules Blazor list (JSON digest remains <see cref="ControlPanelModules"/>).</summary>
    public const string ControlPanelModulesApp = "/cp/modules-app";
    public const string ControlPanelConfigItems = "/cp/config-items";
    /// <summary>CP config items Blazor list (JSON digest remains <see cref="ControlPanelConfigItems"/>; secrets never returned).</summary>
    public const string ControlPanelConfigItemsApp = "/cp/config-items-app";
    public const string ControlPanelMenus = "/cp/menus";
    /// <summary>CP menus Blazor list (JSON digest remains <see cref="ControlPanelMenus"/>).</summary>
    public const string ControlPanelMenusApp = "/cp/menus-app";
    public const string ControlPanelPages = "/cp/pages";
    /// <summary>CP content pages Blazor list (JSON digest remains <see cref="ControlPanelPages"/>).</summary>
    public const string ControlPanelPagesApp = "/cp/pages-app";
    public const string ControlPanelAdminSessions = "/cp/admin-sessions";
    /// <summary>CP admin sessions Blazor list (JSON digest remains <see cref="ControlPanelAdminSessions"/>; tokens never returned).</summary>
    public const string ControlPanelAdminSessionsApp = "/cp/admin-sessions-app";
    public const string ControlPanelStorages = "/cp/storages";
    /// <summary>CP storages Blazor list (JSON digest remains <see cref="ControlPanelStorages"/>).</summary>
    public const string ControlPanelStoragesApp = "/cp/storages-app";
    public const string ControlPanelCurrencies = "/cp/currencies";
    /// <summary>CP currencies Blazor list (JSON digest remains <see cref="ControlPanelCurrencies"/>).</summary>
    public const string ControlPanelCurrenciesApp = "/cp/currencies-app";
    public const string ControlPanelApiClients = "/cp/api-clients";
    /// <summary>CP API clients Blazor list (JSON digest remains <see cref="ControlPanelApiClients"/>; key hashes never returned).</summary>
    public const string ControlPanelApiClientsApp = "/cp/api-clients-app";
    /// <summary>CP Power BI config + reports metadata (Azure secrets never returned; writes remain PHP).</summary>
    public const string ControlPanelPowerBi = "/cp/power-bi";
    /// <summary>CP Power BI Blazor list (JSON digest remains <see cref="ControlPanelPowerBi"/>).</summary>
    public const string ControlPanelPowerBiApp = "/cp/power-bi-app";
    /// <summary>CP mobile apps config from integrations_json (push secrets never returned; writes remain PHP).</summary>
    public const string ControlPanelMobileApps = "/cp/mobile-apps";
    /// <summary>CP mobile apps Blazor summary (JSON digest remains <see cref="ControlPanelMobileApps"/>).</summary>
    public const string ControlPanelMobileAppsApp = "/cp/mobile-apps-app";

    /// <summary>CP Metabase config + dashboards (secret_key never returned; writes remain PHP).</summary>
    public const string ControlPanelMetabase = "/cp/metabase";
    /// <summary>CP Metabase Blazor list (JSON digest remains <see cref="ControlPanelMetabase"/>).</summary>
    public const string ControlPanelMetabaseApp = "/cp/metabase-app";
    /// <summary>CP NL reporting definitions metadata (query/recipients payloads omitted; writes remain PHP).</summary>
    public const string ControlPanelNlReporting = "/cp/nl-reporting";
    /// <summary>CP NL reporting Blazor list (JSON digest remains <see cref="ControlPanelNlReporting"/>).</summary>
    public const string ControlPanelNlReportingApp = "/cp/nl-reporting-app";
    /// <summary>CP marketing broadcast campaigns metadata (bodies omitted; send remains PHP).</summary>
    public const string ControlPanelMarketingBroadcast = "/cp/marketing-broadcast";
    /// <summary>CP marketing broadcast Blazor list (JSON digest remains <see cref="ControlPanelMarketingBroadcast"/>).</summary>
    public const string ControlPanelMarketingBroadcastApp = "/cp/marketing-broadcast-app";
    /// <summary>CP demo tenants registry (passwords never returned; provision remains PHP).</summary>
    public const string ControlPanelDemoTenants = "/cp/demo-tenants";
    /// <summary>CP demo tenants Blazor list (JSON digest remains <see cref="ControlPanelDemoTenants"/>).</summary>
    public const string ControlPanelDemoTenantsApp = "/cp/demo-tenants-app";
    /// <summary>CP AI Parts Agent sessions metadata (system_prompt / client_ip / full transcripts omitted).</summary>
    public const string ControlPanelPartsAgentChats = "/cp/parts-agent-chats";
    /// <summary>CP Parts Agent Blazor list (JSON digest remains <see cref="ControlPanelPartsAgentChats"/>).</summary>
    public const string ControlPanelPartsAgentChatsApp = "/cp/parts-agent-chats-app";
    /// <summary>CP POS settings + recent sales metadata (terminal writes remain PHP).</summary>
    public const string ControlPanelPosOverview = "/cp/pos-overview";
    /// <summary>CP POS Blazor overview (JSON digest remains <see cref="ControlPanelPosOverview"/>).</summary>
    public const string ControlPanelPosOverviewApp = "/cp/pos-overview-app";
    /// <summary>CP tax toolkit catalog + tenant profile (rules_json / reg_number secrets omitted).</summary>
    public const string ControlPanelTaxToolkits = "/cp/tax-toolkits";
    /// <summary>CP tax toolkits Blazor list (JSON digest remains <see cref="ControlPanelTaxToolkits"/>).</summary>
    public const string ControlPanelTaxToolkitsApp = "/cp/tax-toolkits-app";
    /// <summary>CP SMS operators + WhatsApp notify log (parameters_values / tokens / raw phone omitted).</summary>
    public const string ControlPanelSmsWhatsapp = "/cp/sms-whatsapp";
    /// <summary>CP SMS/WhatsApp Blazor list (JSON digest remains <see cref="ControlPanelSmsWhatsapp"/>).</summary>
    public const string ControlPanelSmsWhatsappApp = "/cp/sms-whatsapp-app";
    /// <summary>CP CRM board KPIs + leads metadata (email/phone/notes omitted).</summary>
    public const string ControlPanelCrmBoard = "/cp/crm-board";
    /// <summary>CP CRM Blazor board (JSON digest remains <see cref="ControlPanelCrmBoard"/>).</summary>
    public const string ControlPanelCrmBoardApp = "/cp/crm-board-app";
    /// <summary>CP document control templates (HTML/bank secrets omitted).</summary>
    public const string ControlPanelDocumentControl = "/cp/document-control";
    /// <summary>CP document control Blazor list (JSON digest remains <see cref="ControlPanelDocumentControl"/>).</summary>
    public const string ControlPanelDocumentControlApp = "/cp/document-control-app";
    /// <summary>CP delivery/obtaining modes (parameters_values omitted).</summary>
    public const string ControlPanelDeliveryMethods = "/cp/delivery-methods";
    /// <summary>CP delivery methods Blazor list (JSON digest remains <see cref="ControlPanelDeliveryMethods"/>).</summary>
    public const string ControlPanelDeliveryMethodsApp = "/cp/delivery-methods-app";
    /// <summary>CP article crosses pairs (read-only analogs list).</summary>
    public const string ControlPanelCrosses = "/cp/crosses";
    /// <summary>CP crosses Blazor list (JSON digest remains <see cref="ControlPanelCrosses"/>).</summary>
    public const string ControlPanelCrossesApp = "/cp/crosses-app";
    /// <summary>CP HR overview KPIs + employees (salary/PII detail omitted).</summary>
    public const string ControlPanelHrOverview = "/cp/hr-overview";
    /// <summary>CP HR Blazor overview (JSON digest remains <see cref="ControlPanelHrOverview"/>).</summary>
    public const string ControlPanelHrOverviewApp = "/cp/hr-overview-app";
    /// <summary>CP production overview KPIs + work orders (cost columns omitted).</summary>
    public const string ControlPanelProductionOverview = "/cp/production-overview";
    /// <summary>CP production Blazor overview (JSON digest remains <see cref="ControlPanelProductionOverview"/>).</summary>
    public const string ControlPanelProductionOverviewApp = "/cp/production-overview-app";
    /// <summary>CP projects overview KPIs + projects (timesheet rates omitted).</summary>
    public const string ControlPanelProjectsOverview = "/cp/projects-overview";
    /// <summary>CP projects Blazor overview (JSON digest remains <see cref="ControlPanelProjectsOverview"/>).</summary>
    public const string ControlPanelProjectsOverviewApp = "/cp/projects-overview-app";
    /// <summary>CP industry packs metadata (JSON blobs omitted).</summary>
    public const string ControlPanelIndustryPacks = "/cp/industry-packs";
    /// <summary>CP industry packs Blazor list (JSON digest remains <see cref="ControlPanelIndustryPacks"/>).</summary>
    public const string ControlPanelIndustryPacksApp = "/cp/industry-packs-app";
    /// <summary>CP jewellery retail KPIs + vouchers (PII/cost omitted).</summary>
    public const string ControlPanelJewelleryRetail = "/cp/jewellery-retail";
    /// <summary>CP jewellery Blazor retail (JSON digest remains <see cref="ControlPanelJewelleryRetail"/>).</summary>
    public const string ControlPanelJewelleryRetailApp = "/cp/jewellery-retail-app";
    /// <summary>CP price lists metadata (stats_json/error_text/stored_relpath omitted).</summary>
    public const string ControlPanelPriceLists = "/cp/price-lists";
    /// <summary>CP price lists Blazor list (JSON digest remains <see cref="ControlPanelPriceLists"/>).</summary>
    public const string ControlPanelPriceListsApp = "/cp/price-lists-app";
    /// <summary>CP auto-price rules (config_json/notes/meta omitted).</summary>
    public const string ControlPanelAutoPrice = "/cp/auto-price";
    /// <summary>CP auto-price Blazor list (JSON digest remains <see cref="ControlPanelAutoPrice"/>).</summary>
    public const string ControlPanelAutoPriceApp = "/cp/auto-price-app";
    /// <summary>CP UAE tax compliance legislation (erp_summary/pdf/passport omitted).</summary>
    public const string ControlPanelUaeTaxCompliance = "/cp/uae-tax-compliance";
    /// <summary>CP UAE tax Blazor list (JSON digest remains <see cref="ControlPanelUaeTaxCompliance"/>).</summary>
    public const string ControlPanelUaeTaxComplianceApp = "/cp/uae-tax-compliance-app";
    /// <summary>CP budgets KPIs + headers (note omitted).</summary>
    public const string ControlPanelBudgets = "/cp/budgets";
    /// <summary>CP budgets Blazor list (JSON digest remains <see cref="ControlPanelBudgets"/>).</summary>
    public const string ControlPanelBudgetsApp = "/cp/budgets-app";
    /// <summary>CP carriers KPIs + directory (contact PII omitted).</summary>
    public const string ControlPanelCarriers = "/cp/carriers";
    /// <summary>CP carriers Blazor list (JSON digest remains <see cref="ControlPanelCarriers"/>).</summary>
    public const string ControlPanelCarriersApp = "/cp/carriers-app";
    /// <summary>CP payment gateways (parameters/credentials omitted).</summary>
    public const string ControlPanelPaymentGateways = "/cp/payment-gateways";
    /// <summary>CP payment gateways Blazor list (JSON digest remains <see cref="ControlPanelPaymentGateways"/>).</summary>
    public const string ControlPanelPaymentGatewaysApp = "/cp/payment-gateways-app";
    /// <summary>CP workflows KPIs + definitions (trigger_config/JSON omitted).</summary>
    public const string ControlPanelWorkflows = "/cp/workflows";
    /// <summary>CP workflows Blazor list (JSON digest remains <see cref="ControlPanelWorkflows"/>).</summary>
    public const string ControlPanelWorkflowsApp = "/cp/workflows-app";
    /// <summary>CP purchase requisitions (justification/decision_note omitted).</summary>
    public const string ControlPanelPurchaseRequests = "/cp/purchase-requests";
    /// <summary>CP purchase requests Blazor list (JSON digest remains <see cref="ControlPanelPurchaseRequests"/>).</summary>
    public const string ControlPanelPurchaseRequestsApp = "/cp/purchase-requests-app";
    /// <summary>CP promotions (campaign codes from epc_promo_promotions).</summary>
    public const string ControlPanelPromotions = "/cp/promotions";
    /// <summary>CP promotions Blazor list (JSON digest remains <see cref="ControlPanelPromotions"/>).</summary>
    public const string ControlPanelPromotionsApp = "/cp/promotions-app";
    /// <summary>CP CRM opportunities (notes omitted).</summary>
    public const string ControlPanelCrmOpportunities = "/cp/crm-opportunities";
    /// <summary>CP CRM opportunities Blazor list (JSON digest remains <see cref="ControlPanelCrmOpportunities"/>).</summary>
    public const string ControlPanelCrmOpportunitiesApp = "/cp/crm-opportunities-app";
    /// <summary>CP integrations / webhooks (secrets/events JSON omitted).</summary>
    public const string ControlPanelIntegrations = "/cp/integrations";
    /// <summary>CP integrations Blazor list (JSON digest remains <see cref="ControlPanelIntegrations"/>).</summary>
    public const string ControlPanelIntegrationsApp = "/cp/integrations-app";
    /// <summary>CP page builder layouts (layout_json/brand_json omitted).</summary>
    public const string ControlPanelPageBuilder = "/cp/page-builder";
    /// <summary>CP page builder Blazor list (JSON digest remains <see cref="ControlPanelPageBuilder"/>).</summary>
    public const string ControlPanelPageBuilderApp = "/cp/page-builder-app";
    /// <summary>CP product catalogue (shop_catalogue_products safe columns).</summary>
    public const string ControlPanelProductCatalogue = "/cp/product-catalogue";
    /// <summary>CP product catalogue Blazor list (JSON digest remains <see cref="ControlPanelProductCatalogue"/>).</summary>
    public const string ControlPanelProductCatalogueApp = "/cp/product-catalogue-app";
    /// <summary>CP platform governance rules (description/config_json omitted).</summary>
    public const string ControlPanelPlatformGovernance = "/cp/platform-governance";
    /// <summary>CP platform governance Blazor list (JSON digest remains <see cref="ControlPanelPlatformGovernance"/>).</summary>
    public const string ControlPanelPlatformGovernanceApp = "/cp/platform-governance-app";
    /// <summary>CP e-invoice documents (payload JSON/XML omitted).</summary>
    public const string ControlPanelEinvoiceDocuments = "/cp/einvoice-documents";
    /// <summary>CP e-invoice documents Blazor list (JSON digest remains <see cref="ControlPanelEinvoiceDocuments"/>).</summary>
    public const string ControlPanelEinvoiceDocumentsApp = "/cp/einvoice-documents-app";
    /// <summary>CP jewellery repairs (customer PII/narration omitted).</summary>
    public const string ControlPanelJewelleryRepairs = "/cp/jewellery-repairs";
    /// <summary>CP jewellery repairs Blazor list (JSON digest remains <see cref="ControlPanelJewelleryRepairs"/>).</summary>
    public const string ControlPanelJewelleryRepairsApp = "/cp/jewellery-repairs-app";
    /// <summary>CP CRM tickets (message bodies omitted).</summary>
    public const string ControlPanelCrmTickets = "/cp/crm-tickets";
    /// <summary>CP CRM tickets Blazor list (JSON digest remains <see cref="ControlPanelCrmTickets"/>).</summary>
    public const string ControlPanelCrmTicketsApp = "/cp/crm-tickets-app";
    /// <summary>CP marketing growth (task/KPI/review; notes omitted).</summary>
    public const string ControlPanelMarketingGrowth = "/cp/marketing-growth";
    /// <summary>CP marketing growth Blazor list (JSON digest remains <see cref="ControlPanelMarketingGrowth"/>).</summary>
    public const string ControlPanelMarketingGrowthApp = "/cp/marketing-growth-app";
    /// <summary>CP SOC 2 compliance controls (description/implementation omitted).</summary>
    public const string ControlPanelSoc2Compliance = "/cp/soc2-compliance";
    /// <summary>CP SOC 2 Blazor list (JSON digest remains <see cref="ControlPanelSoc2Compliance"/>).</summary>
    public const string ControlPanelSoc2ComplianceApp = "/cp/soc2-compliance-app";
    /// <summary>CP cost models (detail_json omitted).</summary>
    public const string ControlPanelCostModels = "/cp/cost-models";
    /// <summary>CP cost models Blazor list (JSON digest remains <see cref="ControlPanelCostModels"/>).</summary>
    public const string ControlPanelCostModelsApp = "/cp/cost-models-app";
    /// <summary>CP financial depth / periods (basis/schedule/lines JSON omitted).</summary>
    public const string ControlPanelFinAdvanced = "/cp/fin-advanced";
    /// <summary>CP fin-advanced Blazor list (JSON digest remains <see cref="ControlPanelFinAdvanced"/>).</summary>
    public const string ControlPanelFinAdvancedApp = "/cp/fin-advanced-app";
    /// <summary>CP blockchain proofs (payload/merkle JSON omitted).</summary>
    public const string ControlPanelBlockchainProofs = "/cp/blockchain-proofs";
    /// <summary>CP blockchain proofs Blazor list (JSON digest remains <see cref="ControlPanelBlockchainProofs"/>).</summary>
    public const string ControlPanelBlockchainProofsApp = "/cp/blockchain-proofs-app";
    /// <summary>Batch 4: read-only CP Orders/OMS Blazor list (writes remain PHP).</summary>
    public const string ControlPanelOrders = "/cp/orders";
    /// <summary>Batch 4: read-only shop_orders digest + KPI summary.</summary>
    public const string ControlPanelOrdersDigest = "/cp/orders-digest";
    /// <summary>Batch 4: users Blazor list (JSON digest remains <see cref="ControlPanelUsers"/>).</summary>
    public const string ControlPanelUsersApp = "/cp/users-app";
    /// <summary>Batch 4: groups Blazor list (JSON digest remains <see cref="ControlPanelGroups"/>).</summary>
    public const string ControlPanelGroupsApp = "/cp/groups-app";
    public const string Erp = "/erp";
    public const string ErpApp = "/erp/app";
    public const string ErpParity = "/erp/parity";
    public const string ErpDashboardSummary = "/erp/dashboard-summary";
    /// <summary>ERP dashboard summary Blazor KPI UI (JSON digest remains <see cref="ErpDashboardSummary"/>).</summary>
    public const string ErpDashboardSummaryApp = "/erp/dashboard-summary-app";
    public const string ErpAccountsSummary = "/erp/accounts-summary";
    /// <summary>ERP accounts summary Blazor KPI UI (JSON digest remains <see cref="ErpAccountsSummary"/>).</summary>
    public const string ErpAccountsSummaryApp = "/erp/accounts-summary-app";
    public const string ErpSuppliers = "/erp/suppliers";
    /// <summary>Suppliers Blazor list (JSON digest remains <see cref="ErpSuppliers"/>).</summary>
    public const string ErpSuppliersApp = "/erp/suppliers-app";
    public const string ErpPurchases = "/erp/purchases";
    /// <summary>Purchases Blazor list (JSON digest remains <see cref="ErpPurchases"/>).</summary>
    public const string ErpPurchasesApp = "/erp/purchases-app";
    public const string ErpCashAccounts = "/erp/cash-accounts";
    /// <summary>Cash &amp; bank Blazor list (JSON digests remain <see cref="ErpCashAccounts"/> / <see cref="ErpCashEntries"/>).</summary>
    public const string ErpCashAccountsApp = "/erp/cash-accounts-app";
    public const string ErpCashEntries = "/erp/cash-entries";
    /// <summary>ERP cash ledger Blazor list (JSON digest remains <see cref="ErpCashEntries"/>).</summary>
    public const string ErpCashEntriesApp = "/erp/cash-entries-app";
    public const string ErpInvoices = "/erp/invoices";
    /// <summary>Invoices Blazor list (JSON digest remains <see cref="ErpInvoices"/>).</summary>
    public const string ErpInvoicesApp = "/erp/invoices-app";
    public const string ErpGlJournals = "/erp/gl-journals";
    /// <summary>GL journals Blazor list (JSON digest remains <see cref="ErpGlJournals"/>).</summary>
    public const string ErpGlJournalsApp = "/erp/gl-journals-app";
    public const string ErpCoaAccounts = "/erp/coa-accounts";
    /// <summary>Chart of accounts Blazor list (JSON digest remains <see cref="ErpCoaAccounts"/>).</summary>
    public const string ErpCoaAccountsApp = "/erp/coa-accounts-app";
    public const string ErpWarehouses = "/erp/warehouses";
    /// <summary>Warehouses Blazor list (JSON digest remains <see cref="ErpWarehouses"/>).</summary>
    public const string ErpWarehousesApp = "/erp/warehouses-app";
    public const string ErpSalesOrders = "/erp/sales-orders";
    /// <summary>Batch 4: sales orders Blazor list (JSON digest remains <see cref="ErpSalesOrders"/>).</summary>
    public const string ErpSalesOrdersApp = "/erp/sales-orders-app";
    public const string ErpPurchaseOrders = "/erp/purchase-orders";
    /// <summary>Purchase orders Blazor list (JSON digest remains <see cref="ErpPurchaseOrders"/>).</summary>
    public const string ErpPurchaseOrdersApp = "/erp/purchase-orders-app";
    public const string ErpInventoryStock = "/erp/inventory-stock";
    /// <summary>ERP inventory stock Blazor KPI UI (JSON digest remains <see cref="ErpInventoryStock"/>).</summary>
    public const string ErpInventoryStockApp = "/erp/inventory-stock-app";
    /// <summary>ERP bank reconciliation (statement lines; matched_entry joins omitted).</summary>
    public const string ErpBankReconciliation = "/erp/bank-reconciliation";
    /// <summary>ERP bank reconciliation Blazor list (JSON digest remains <see cref="ErpBankReconciliation"/>).</summary>
    public const string ErpBankReconciliationApp = "/erp/bank-reconciliation-app";
    /// <summary>ERP stock transfers (notes omitted).</summary>
    public const string ErpStockTransfers = "/erp/stock-transfers";
    /// <summary>ERP stock transfers Blazor list (JSON digest remains <see cref="ErpStockTransfers"/>).</summary>
    public const string ErpStockTransfersApp = "/erp/stock-transfers-app";
    /// <summary>ERP sales quotations (notes omitted).</summary>
    public const string ErpSalesQuotations = "/erp/sales-quotations";
    /// <summary>ERP sales quotations Blazor list (JSON digest remains <see cref="ErpSalesQuotations"/>).</summary>
    public const string ErpSalesQuotationsApp = "/erp/sales-quotations-app";
    /// <summary>ERP workspace favorites/shortcuts.</summary>
    public const string ErpWorkspaceFavorites = "/erp/workspace-favorites";
    /// <summary>ERP workspace favorites Blazor list (JSON digest remains <see cref="ErpWorkspaceFavorites"/>).</summary>
    public const string ErpWorkspaceFavoritesApp = "/erp/workspace-favorites-app";
    /// <summary>ERP fixed assets register (note omitted).</summary>
    public const string ErpFixedAssets = "/erp/fixed-assets";
    /// <summary>ERP fixed assets Blazor list (JSON digest remains <see cref="ErpFixedAssets"/>).</summary>
    public const string ErpFixedAssetsApp = "/erp/fixed-assets-app";
    public const string Bos = "/bos";
    public const string BosApp = "/bos/app";
    public const string BosParity = "/bos/parity";
    public const string BosFleetSummary = "/bos/fleet-summary";
    /// <summary>BOS fleet summary Blazor KPI UI (JSON digest remains <see cref="BosFleetSummary"/>).</summary>
    public const string BosFleetSummaryApp = "/bos/fleet-summary-app";
    public const string BosTenants = "/bos/tenants";
    /// <summary>BOS fleet tenants Blazor list (JSON digest remains <see cref="BosTenants"/>).</summary>
    public const string BosTenantsApp = "/bos/tenants-app";
    public const string BosFleetHealth = "/bos/fleet-health";
    /// <summary>BOS fleet health Blazor KPI UI (JSON digest remains <see cref="BosFleetHealth"/>).</summary>
    public const string BosFleetHealthApp = "/bos/fleet-health-app";
    public const string BosFleetReadiness = "/bos/fleet-readiness";
    /// <summary>BOS fleet readiness Blazor KPI UI (JSON digest remains <see cref="BosFleetReadiness"/>).</summary>
    public const string BosFleetReadinessApp = "/bos/fleet-readiness-app";
    public const string BosAuditLog = "/bos/audit-log";
    /// <summary>BOS audit log Blazor list (JSON digest remains <see cref="BosAuditLog"/>).</summary>
    public const string BosAuditLogApp = "/bos/audit-log-app";
    public const string ApiPrefix = "/api";
    public const string ApiMigrationStatus = "/api/migration/status";
    public const string CatalogStatus = "/api/v1/catalog/status";
    public const string CatalogManufacturers = "/api/v1/catalog/manufacturers";
    public const string CatalogModels = "/api/v1/catalog/models";
    public const string CatalogModifications = "/api/v1/catalog/modifications";
    public const string CatalogBrands = "/api/v1/catalog/brands";
    public const string CatalogSuppliers = "/api/v1/catalog/suppliers";
    public const string CatalogVin = "/api/v1/catalog/vin";
    public const string CatalogEngines = "/api/v1/catalog/engines";
    public const string CatalogAnalogs = "/api/v1/catalog/analogs";
    public const string CatalogArticleBrands = "/api/v1/catalog/article-brands";
    public const string CatalogCategories = "/api/v1/catalog/categories";
    public const string CatalogProducts = "/api/v1/catalog/products";
    public const string CatalogEngineSearch = "/api/v1/catalog/engine-search";
    public const string CatalogArticleLinks = "/api/v1/catalog/article-links";
    public const string CatalogArticle = "/api/v1/catalog/article";
    public const string CatalogArticles = "/api/v1/catalog/articles";
    public const string CatalogEngine = "/api/v1/catalog/engine";
    public const string CatalogBrandParts = "/api/v1/catalog/brand-parts";
    public const string CatalogParity = "/api/v1/catalog/parity";
    public const string PriceLookup = "/api/v1/price/lookup";
    public const string PriceLookupParity = "/api/v1/price/parity";
    public const string StorefrontParity = "/storefront/parity";
    public const string StorefrontApp = "/storefront/app";
    /// <summary>Batch 4: storefront part search Blazor results (PHP part_search remains authoritative for cart/tabs).</summary>
    public const string StorefrontSearchApp = "/storefront/search-app";
    /// <summary>Storefront warehouse-offer search JSON digest (Blazor UI remains <see cref="StorefrontSearchApp"/>).</summary>
    public const string StorefrontSearch = "/storefront/search";
    /// <summary>Batch 4: authenticated cart Blazor summary (qty/checkout writes remain PHP /shop/cart).</summary>
    public const string StorefrontCartApp = "/storefront/cart-app";
    /// <summary>Storefront authenticated cart JSON digest (Blazor UI remains <see cref="StorefrontCartApp"/>).</summary>
    public const string StorefrontCart = "/storefront/cart";
    public const string StorefrontAccount = "/storefront/account";
    public const string StorefrontAccountSummary = "/storefront/account-summary";
    /// <summary>Storefront account summary Blazor KPI UI (JSON digest remains <see cref="StorefrontAccountSummary"/>).</summary>
    public const string StorefrontAccountSummaryApp = "/storefront/account-summary-app";
    public const string StorefrontOrders = "/storefront/orders";
    /// <summary>Storefront customer orders Blazor list (JSON digest remains <see cref="StorefrontOrders"/>).</summary>
    public const string StorefrontOrdersApp = "/storefront/orders-app";
    public const string StorefrontGarage = "/storefront/garage";
    /// <summary>Storefront garage Blazor list (JSON digest remains <see cref="StorefrontGarage"/>).</summary>
    public const string StorefrontGarageApp = "/storefront/garage-app";
    public const string StorefrontProfile = "/storefront/profile";
    /// <summary>Storefront profile Blazor read UI (JSON digest remains <see cref="StorefrontProfile"/>).</summary>
    public const string StorefrontProfileApp = "/storefront/profile-app";

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
