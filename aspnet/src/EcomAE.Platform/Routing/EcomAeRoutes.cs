namespace EcomAE.Platform.Routing;

public static class EcomAeRoutes
{
    public const string Health = "/health";
    /// <summary>Public SEO entry advertised in robots.txt; redirects to PHP sitemap-index.php.</summary>
    public const string SitemapXml = "/sitemap.xml";
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
    /// <summary>ASP.NET-primary intent with PHP retained as reference for gap-finding (not PHP deletion).</summary>
    public const string MigrationPhpReferenceMode = "/migration/php-reference-mode";
    public const string MigrationUmapiUsage = "/migration/umapi-usage";
    public const string MigrationPlatformJobs = "/migration/platform-jobs";
    public const string SurfaceParity = "/migration/surface-parity";
    public const string PresentationParity = "/migration/presentation-parity";
    public const string PhpModuleCatalog = "/migration/php-module-catalog";
    public const string LiveSurfaceLinks = "/migration/live-surface-links";
    /// <summary>Named live tenants that must keep PHP presentation identical (no ASP.NET hybrid).</summary>
    public const string LiveTenantPresentationLock = "/migration/live-tenant-presentation-lock";
    /// <summary>Operator board: phases toward 100% ASP.NET Core / 0 PHP (honest; cutoverAllowed=false).</summary>
    public const string AspNetZeroPhpPath = "/migration/aspnet-zero-php-path";
    /// <summary>On-premises ERP product track board (installer ≠ ERP-only SaaS; cutoverAllowed=false).</summary>
    public const string MigrationOnPremisesParity = "/migration/on-premises-parity";
    public const string MigrationConsole = "/migration/console";
    public const string MigrationCompare = "/migration/compare";
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
    /// <summary>CP landed cost sheets (notes omitted).</summary>
    public const string ControlPanelLandedCost = "/cp/landed-cost";
    /// <summary>CP landed-cost Blazor list (JSON digest remains <see cref="ControlPanelLandedCost"/>).</summary>
    public const string ControlPanelLandedCostApp = "/cp/landed-cost-app";
    /// <summary>CP warehouse WMS work pool.</summary>
    public const string ControlPanelWarehouseWms = "/cp/warehouse-wms";
    /// <summary>CP warehouse-wms Blazor list (JSON digest remains <see cref="ControlPanelWarehouseWms"/>).</summary>
    public const string ControlPanelWarehouseWmsApp = "/cp/warehouse-wms-app";
    /// <summary>CP AI service queries (input/output text omitted).</summary>
    public const string ControlPanelAiService = "/cp/ai-service";
    /// <summary>CP AI service Blazor list (JSON digest remains <see cref="ControlPanelAiService"/>).</summary>
    public const string ControlPanelAiServiceApp = "/cp/ai-service-app";
    /// <summary>CP returns/RMA requests (description/notes omitted).</summary>
    public const string ControlPanelReturnsRma = "/cp/returns-rma";
    /// <summary>CP returns-rma Blazor list (JSON digest remains <see cref="ControlPanelReturnsRma"/>).</summary>
    public const string ControlPanelReturnsRmaApp = "/cp/returns-rma-app";
    /// <summary>CP commerce isolation audit (report_json omitted).</summary>
    public const string ControlPanelIsolationAudit = "/cp/isolation-audit";
    /// <summary>CP isolation-audit Blazor list (JSON digest remains <see cref="ControlPanelIsolationAudit"/>).</summary>
    public const string ControlPanelIsolationAuditApp = "/cp/isolation-audit-app";
    /// <summary>CP AML compliance KYC/transactions (notes/document paths omitted).</summary>
    public const string ControlPanelAmlCompliance = "/cp/aml-compliance";
    /// <summary>CP aml-compliance Blazor list (JSON digest remains <see cref="ControlPanelAmlCompliance"/>).</summary>
    public const string ControlPanelAmlComplianceApp = "/cp/aml-compliance-app";
    /// <summary>CP jewellery masters (karat/rate/barcode).</summary>
    public const string ControlPanelJewelleryMasters = "/cp/jewellery-masters";
    /// <summary>CP jewellery-masters Blazor list (JSON digest remains <see cref="ControlPanelJewelleryMasters"/>).</summary>
    public const string ControlPanelJewelleryMastersApp = "/cp/jewellery-masters-app";
    /// <summary>CP consolidations group entities (memo omitted).</summary>
    public const string ControlPanelConsolidations = "/cp/consolidations";
    /// <summary>CP consolidations Blazor list (JSON digest remains <see cref="ControlPanelConsolidations"/>).</summary>
    public const string ControlPanelConsolidationsApp = "/cp/consolidations-app";
    /// <summary>CP CRM activities (notes omitted).</summary>
    public const string ControlPanelCrmActivities = "/cp/crm-activities";
    /// <summary>CP CRM activities Blazor list (JSON digest remains <see cref="ControlPanelCrmActivities"/>).</summary>
    public const string ControlPanelCrmActivitiesApp = "/cp/crm-activities-app";
    /// <summary>CP auth MFA enrollment/policy (secrets/hashes omitted).</summary>
    public const string ControlPanelAuthMfa = "/cp/auth-mfa";
    /// <summary>CP auth MFA Blazor list (JSON digest remains <see cref="ControlPanelAuthMfa"/>).</summary>
    public const string ControlPanelAuthMfaApp = "/cp/auth-mfa-app";
    /// <summary>CP electronic reporting formats (preview omitted).</summary>
    public const string ControlPanelElectronicReporting = "/cp/electronic-reporting";
    /// <summary>CP electronic reporting Blazor list (JSON digest remains <see cref="ControlPanelElectronicReporting"/>).</summary>
    public const string ControlPanelElectronicReportingApp = "/cp/electronic-reporting-app";
    /// <summary>CP collections/dunning queue (notes omitted).</summary>
    public const string ControlPanelCollectionsDunning = "/cp/collections-dunning";
    /// <summary>CP collections/dunning Blazor list (JSON digest remains <see cref="ControlPanelCollectionsDunning"/>).</summary>
    public const string ControlPanelCollectionsDunningApp = "/cp/collections-dunning-app";

    public const string ControlPanelMarketplaceChannels = "/cp/marketplace-channels";
    /// <summary>CP marketplace channels Blazor list (JSON digest remains <see cref="ControlPanelMarketplaceChannels"/>).</summary>
    public const string ControlPanelMarketplaceChannelsApp = "/cp/marketplace-channels-app";

    public const string ControlPanelDemandIntelligence = "/cp/demand-intelligence";
    /// <summary>CP demand intelligence Blazor list (JSON digest remains <see cref="ControlPanelDemandIntelligence"/>).</summary>
    public const string ControlPanelDemandIntelligenceApp = "/cp/demand-intelligence-app";

    public const string ControlPanelCreditLimits = "/cp/credit-limits";
    /// <summary>CP credit limits Blazor list (JSON digest remains <see cref="ControlPanelCreditLimits"/>).</summary>
    public const string ControlPanelCreditLimitsApp = "/cp/credit-limits-app";

    public const string ControlPanelInsuranceCompliance = "/cp/insurance-compliance";
    /// <summary>CP insurance compliance Blazor list (JSON digest remains <see cref="ControlPanelInsuranceCompliance"/>).</summary>
    public const string ControlPanelInsuranceComplianceApp = "/cp/insurance-compliance-app";

    public const string ControlPanelAuditTrail = "/cp/audit-trail";
    /// <summary>CP ERP audit trail Blazor list (JSON digest remains <see cref="ControlPanelAuditTrail"/>).</summary>
    public const string ControlPanelAuditTrailApp = "/cp/audit-trail-app";

    public const string ControlPanelDocExpiry = "/cp/doc-expiry";
    /// <summary>CP document expiry Blazor list (JSON digest remains <see cref="ControlPanelDocExpiry"/>).</summary>
    public const string ControlPanelDocExpiryApp = "/cp/doc-expiry-app";

    public const string ControlPanelTenantConfig = "/cp/tenant-config";
    /// <summary>CP tenant config Blazor list (JSON digest remains <see cref="ControlPanelTenantConfig"/>).</summary>
    public const string ControlPanelTenantConfigApp = "/cp/tenant-config-app";

    public const string ControlPanelJewelleryStockVerification = "/cp/jewellery-stock-verification";
    /// <summary>CP jewellery stock verification Blazor list (JSON digest remains <see cref="ControlPanelJewelleryStockVerification"/>).</summary>
    public const string ControlPanelJewelleryStockVerificationApp = "/cp/jewellery-stock-verification-app";
    public const string ControlPanelTaxExternalReporting = "/cp/tax-external-reporting";
    /// <summary>CP Tax external reporting Blazor list (JSON digest remains <see cref="ControlPanelTaxExternalReporting"/>).</summary>
    public const string ControlPanelTaxExternalReportingApp = "/cp/tax-external-reporting-app";
    public const string ControlPanelPoApprovals = "/cp/po-approvals";
    /// <summary>CP PO approvals Blazor list (JSON digest remains <see cref="ControlPanelPoApprovals"/>).</summary>
    public const string ControlPanelPoApprovalsApp = "/cp/po-approvals-app";
    public const string ControlPanelFinanceClose = "/cp/finance-close";
    /// <summary>CP Finance close Blazor list (JSON digest remains <see cref="ControlPanelFinanceClose"/>).</summary>
    public const string ControlPanelFinanceCloseApp = "/cp/finance-close-app";
    public const string ControlPanelJewelleryFixing = "/cp/jewellery-fixing";
    /// <summary>CP Jewellery fixing Blazor list (JSON digest remains <see cref="ControlPanelJewelleryFixing"/>).</summary>
    public const string ControlPanelJewelleryFixingApp = "/cp/jewellery-fixing-app";

    public const string ControlPanelWebTracker = "/cp/web-tracker";
    /// <summary>CP Web tracker Blazor list (JSON digest remains <see cref="ControlPanelWebTracker"/>).</summary>
    public const string ControlPanelWebTrackerApp = "/cp/web-tracker-app";
    public const string ControlPanelAbandonedCarts = "/cp/abandoned-carts";
    /// <summary>CP Abandoned carts Blazor list (JSON digest remains <see cref="ControlPanelAbandonedCarts"/>).</summary>
    public const string ControlPanelAbandonedCartsApp = "/cp/abandoned-carts-app";
    public const string ControlPanelQuoteRequests = "/cp/quote-requests";
    /// <summary>CP Quote requests Blazor list (JSON digest remains <see cref="ControlPanelQuoteRequests"/>).</summary>
    public const string ControlPanelQuoteRequestsApp = "/cp/quote-requests-app";
    public const string ControlPanelPlatformCommunication = "/cp/platform-communication";
    /// <summary>CP Platform communication Blazor list (JSON digest remains <see cref="ControlPanelPlatformCommunication"/>).</summary>
    public const string ControlPanelPlatformCommunicationApp = "/cp/platform-communication-app";
    public const string ControlPanelInfoBlocks = "/cp/info-blocks";
    /// <summary>CP Info blocks Blazor list (JSON digest remains <see cref="ControlPanelInfoBlocks"/>).</summary>
    public const string ControlPanelInfoBlocksApp = "/cp/info-blocks-app";

    public const string ControlPanelFreeTools = "/cp/free-tools";
    /// <summary>CP Free tools Blazor list (JSON digest remains <see cref="ControlPanelFreeTools"/>).</summary>
    public const string ControlPanelFreeToolsApp = "/cp/free-tools-app";
    public const string ControlPanelConfigSandbox = "/cp/config-sandbox";
    /// <summary>CP Config sandbox Blazor list (JSON digest remains <see cref="ControlPanelConfigSandbox"/>).</summary>
    public const string ControlPanelConfigSandboxApp = "/cp/config-sandbox-app";
    public const string ControlPanelMarketplaceApps = "/cp/marketplace-apps";
    /// <summary>CP Marketplace apps Blazor list (JSON digest remains <see cref="ControlPanelMarketplaceApps"/>).</summary>
    public const string ControlPanelMarketplaceAppsApp = "/cp/marketplace-apps-app";
    public const string ControlPanelNotifications = "/cp/notifications";
    /// <summary>CP Notifications Blazor list (JSON digest remains <see cref="ControlPanelNotifications"/>).</summary>
    public const string ControlPanelNotificationsApp = "/cp/notifications-app";
    public const string ControlPanelPortalSettings = "/cp/portal-settings";
    /// <summary>CP Portal settings Blazor list (JSON digest remains <see cref="ControlPanelPortalSettings"/>).</summary>
    public const string ControlPanelPortalSettingsApp = "/cp/portal-settings-app";
    public const string ControlPanelDataMigrations = "/cp/data-migrations";
    /// <summary>CP Data migrations Blazor list (JSON digest remains <see cref="ControlPanelDataMigrations"/>).</summary>
    public const string ControlPanelDataMigrationsApp = "/cp/data-migrations-app";

    // Wave 22 CMS/platform leftovers
    public const string ControlPanelGeoRegions = "/cp/geo-regions";
    /// <summary>CP Geo / regions Blazor list (JSON digest remains <see cref="ControlPanelGeoRegions"/>).</summary>
    public const string ControlPanelGeoRegionsApp = "/cp/geo-regions-app";
    public const string ControlPanelProductFilters = "/cp/product-filters";
    /// <summary>CP Product filters Blazor list (JSON digest remains <see cref="ControlPanelProductFilters"/>).</summary>
    public const string ControlPanelProductFiltersApp = "/cp/product-filters-app";
    public const string ControlPanelSearchTabs = "/cp/search-tabs";
    /// <summary>CP Search tabs Blazor list (JSON digest remains <see cref="ControlPanelSearchTabs"/>).</summary>
    public const string ControlPanelSearchTabsApp = "/cp/search-tabs-app";
    public const string ControlPanelSystemRequests = "/cp/system-requests";
    /// <summary>CP System requests Blazor list (JSON digest remains <see cref="ControlPanelSystemRequests"/>).</summary>
    public const string ControlPanelSystemRequestsApp = "/cp/system-requests-app";
    public const string ControlPanelAdditionalTexts = "/cp/additional-texts";
    /// <summary>CP Additional texts Blazor list (JSON digest remains <see cref="ControlPanelAdditionalTexts"/>).</summary>
    public const string ControlPanelAdditionalTextsApp = "/cp/additional-texts-app";
    public const string ControlPanelSliderBanners = "/cp/slider-banners";
    /// <summary>CP Slider / banners Blazor list (JSON digest remains <see cref="ControlPanelSliderBanners"/>).</summary>
    public const string ControlPanelSliderBannersApp = "/cp/slider-banners-app";
    public const string ControlPanelStructureDumps = "/cp/structure-dumps";
    /// <summary>CP Structure dumps Blazor list (JSON digest remains <see cref="ControlPanelStructureDumps"/>).</summary>
    public const string ControlPanelStructureDumpsApp = "/cp/structure-dumps-app";
    public const string ControlPanelCommunicationsTest = "/cp/communications-test";
    /// <summary>CP Communications test Blazor list (JSON digest remains <see cref="ControlPanelCommunicationsTest"/>).</summary>
    public const string ControlPanelCommunicationsTestApp = "/cp/communications-test-app";
    public const string ControlPanelLanguages = "/cp/languages";
    /// <summary>CP Languages Blazor list (JSON digest remains <see cref="ControlPanelLanguages"/>).</summary>
    public const string ControlPanelLanguagesApp = "/cp/languages-app";
    public const string ControlPanelPluginsManager = "/cp/plugins-manager";
    /// <summary>CP Plugins manager Blazor list (JSON digest remains <see cref="ControlPanelPluginsManager"/>).</summary>
    public const string ControlPanelPluginsManagerApp = "/cp/plugins-manager-app";
    public const string ControlPanelTemplatesManager = "/cp/templates-manager";
    /// <summary>CP Templates manager Blazor list (JSON digest remains <see cref="ControlPanelTemplatesManager"/>).</summary>
    public const string ControlPanelTemplatesManagerApp = "/cp/templates-manager-app";
    public const string ControlPanelDesignTokens = "/cp/design-tokens";
    /// <summary>CP Design tokens Blazor list (JSON digest remains <see cref="ControlPanelDesignTokens"/>).</summary>
    public const string ControlPanelDesignTokensApp = "/cp/design-tokens-app";
    public const string ControlPanelSitemap = "/cp/sitemap";
    /// <summary>CP Sitemap Blazor list (JSON digest remains <see cref="ControlPanelSitemap"/>).</summary>
    public const string ControlPanelSitemapApp = "/cp/sitemap-app";

    // Wave 23 remaining ops/guide surfaces
    public const string ControlPanelFailoverStatus = "/cp/failover-status";
    /// <summary>CP Failover status Blazor list (JSON digest remains <see cref="ControlPanelFailoverStatus"/>).</summary>
    public const string ControlPanelFailoverStatusApp = "/cp/failover-status-app";
    public const string ControlPanelOpsGuides = "/cp/ops-guides";
    /// <summary>CP Ops guides / CP menu map Blazor list (JSON digest remains <see cref="ControlPanelOpsGuides"/>).</summary>
    public const string ControlPanelOpsGuidesApp = "/cp/ops-guides-app";
    public const string ControlPanelFileManager = "/cp/file-manager";
    /// <summary>CP File manager Blazor list (JSON digest remains <see cref="ControlPanelFileManager"/>).</summary>
    public const string ControlPanelFileManagerApp = "/cp/file-manager-app";
    public const string ControlPanelServerIp = "/cp/server-ip";
    /// <summary>CP Server IP Blazor list (JSON digest remains <see cref="ControlPanelServerIp"/>).</summary>
    public const string ControlPanelServerIpApp = "/cp/server-ip-app";
    public const string ControlPanelDebugConsole = "/cp/debug-console";
    /// <summary>CP Debug console Blazor list (JSON digest remains <see cref="ControlPanelDebugConsole"/>).</summary>
    public const string ControlPanelDebugConsoleApp = "/cp/debug-console-app";

    /// <summary>Batch 4: read-only CP Orders/OMS Blazor list (writes remain PHP).</summary>
    public const string ControlPanelOrders = "/cp/orders";
    /// <summary>Batch 4: read-only shop_orders digest + KPI summary.</summary>
    public const string ControlPanelOrdersDigest = "/cp/orders-digest";
    /// <summary>Wave B dry-run OMS set_item_status (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsSetItemStatus = "/cp/orders/set-item-status";
    /// <summary>Wave B dry-run OMS set_items_status bulk (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsSetItemsStatus = "/cp/orders/set-items-status";
    /// <summary>Wave B dry-run OMS send_message (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsSendMessage = "/cp/orders/send-message";
    /// <summary>Wave B dry-run OMS set_courier (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsSetCourier = "/cp/orders/set-courier";
    /// <summary>Wave B dry-run OMS delete orders (PHP ajax_delete_orders.php remains authoritative).</summary>
    public const string ControlPanelOmsDeleteOrders = "/cp/orders/delete";
    /// <summary>Wave B dry-run OMS add comment to log (PHP ajax_add_comment_to_log.php remains authoritative).</summary>
    public const string ControlPanelOmsAddComment = "/cp/orders/add-comment";
    /// <summary>Wave B dry-run OMS set orders viewed (PHP ajax_set_orders_viewed.php remains authoritative).</summary>
    public const string ControlPanelOmsSetViewed = "/cp/orders/set-viewed";
    /// <summary>Wave B dry-run for PHP OMS update_item (writes=0; PHP authoritative).</summary>
    public const string ControlPanelOmsUpdateItem = "/cp/orders/update-item";
    /// <summary>Wave B dry-run for PHP ajax_order_pay_refund.php (writes=0; PHP authoritative).</summary>
    public const string ControlPanelOmsPayRefund = "/cp/orders/pay-refund";
    /// <summary>Wave B dry-run for PHP OMS update_items bulk (writes=0).</summary>
    public const string ControlPanelOmsUpdateItems = "/cp/orders/update-items";
    /// <summary>Wave B dry-run OMS supplier_fulfillment_set_stage (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsFulfillmentSetStage = "/cp/orders/fulfillment-set-stage";
    /// <summary>Wave B dry-run OMS supplier_fulfillment_advance (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsFulfillmentAdvance = "/cp/orders/fulfillment-advance";
    /// <summary>Wave B dry-run OMS refresh_item_cost (PHP ajax_epc_orders_oms.php remains authoritative).</summary>
    public const string ControlPanelOmsRefreshItemCost = "/cp/orders/refresh-item-cost";

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
    /// <summary>Wave B dry-run cash voucher amend (PHP cash_voucher_amend remains authoritative).</summary>
    public const string ErpCashEntriesAmend = "/erp/cash-entries/amend";
    /// <summary>Wave B dry-run cash voucher void (PHP cash_voucher_void remains authoritative).</summary>
    public const string ErpCashEntriesVoid = "/erp/cash-entries/void";
    /// <summary>Wave B dry-run for PHP cash_entry create (writes=0; PHP authoritative).</summary>
    public const string ErpCashEntriesCreate = "/erp/cash-entries/create";
    /// <summary>Wave B dry-run for PHP receipt_voucher (writes=0; PHP authoritative).</summary>
    public const string ErpCashEntriesReceiptVoucher = "/erp/cash-entries/receipt-voucher";
    /// <summary>Wave B dry-run for PHP payment_voucher (writes=0; PHP authoritative).</summary>
    public const string ErpCashEntriesPaymentVoucher = "/erp/cash-entries/payment-voucher";
    /// <summary>Wave B dry-run for PHP create_supplier (writes=0).</summary>
    public const string ErpSuppliersCreate = "/erp/suppliers/create";
    /// <summary>Wave B dry-run for PHP create_purchase (writes=0).</summary>
    public const string ErpPurchasesCreate = "/erp/purchases/create";
    /// <summary>Wave B dry-run for PHP purchase_delete draft (writes=0).</summary>
    public const string ErpPurchasesDelete = "/erp/purchases/delete";
    /// <summary>Wave B dry-run for PHP purchase_amend (writes=0).</summary>
    public const string ErpPurchasesAmend = "/erp/purchases/amend";
    /// <summary>Wave B dry-run for PHP so_delete draft (writes=0).</summary>
    public const string ErpSalesOrdersDelete = "/erp/sales-orders/delete";
    /// <summary>Wave B dry-run for PHP customer_master_save (writes=0).</summary>
    public const string ErpCustomersMasterSave = "/erp/customers/master-save";
    /// <summary>Wave B dry-run for PHP as_rma_create (writes=0).</summary>
    public const string ErpAftersalesRmaCreate = "/erp/aftersales/rma-create";
    /// <summary>Wave B dry-run for PHP purchase_from_order (writes=0).</summary>
    public const string ErpPurchasesFromOrder = "/erp/purchases/from-order";
    /// <summary>Wave B dry-run for PHP ccy_set_rate (writes=0).</summary>
    public const string ErpCcySetRate = "/erp/currency/set-rate";
    /// <summary>Wave B dry-run for PHP period_soft_close (writes=0).</summary>
    public const string ErpPeriodSoftClose = "/erp/periods/soft-close";
    /// <summary>Wave B dry-run for PHP period_lock (writes=0).</summary>
    public const string ErpPeriodLock = "/erp/periods/lock";
    /// <summary>Wave B dry-run for PHP customer_settlement (writes=0).</summary>
    public const string ErpCustomerSettlement = "/erp/customers/settlement";
    /// <summary>Wave B dry-run for PHP supplier_settlement (writes=0).</summary>
    public const string ErpSupplierSettlement = "/erp/suppliers/settlement";
    /// <summary>Wave B dry-run for PHP fiscal_set_lock (writes=0).</summary>
    public const string ErpFiscalSetLock = "/erp/fiscal/set-lock";
    /// <summary>Wave B dry-run for PHP period_reopen (writes=0).</summary>
    public const string ErpPeriodReopen = "/erp/periods/reopen";
    /// <summary>Wave B dry-run for PHP purchase_adjustment (writes=0).</summary>
    public const string ErpPurchasesAdjust = "/erp/purchases/adjust";
    /// <summary>Wave B dry-run for PHP order_settlement (writes=0).</summary>
    public const string ErpOrderSettlement = "/erp/orders/settlement";
    /// <summary>Wave B dry-run for PHP sync_suppliers (writes=0).</summary>
    public const string ErpSuppliersSync = "/erp/suppliers/sync";
    /// <summary>Wave B dry-run for PHP gl_post_sales (writes=0).</summary>
    public const string ErpGlPostSales = "/erp/gl-journals/post-sales";
    /// <summary>Wave B dry-run for PHP gl_sync_unposted (writes=0).</summary>
    public const string ErpGlSyncUnposted = "/erp/gl-journals/sync-unposted";
    /// <summary>Wave B dry-run for PHP workflow_status (writes=0).</summary>
    public const string ErpWorkflowStatus = "/erp/workflow/status";
    /// <summary>Wave B dry-run for PHP workflow_create (writes=0).</summary>
    public const string ErpWorkflowCreate = "/erp/workflow/create";
    /// <summary>Wave B dry-run for PHP marketing_create (writes=0).</summary>
    public const string ErpMarketingCreate = "/erp/marketing/create";
    /// <summary>Wave B dry-run for PHP sub_save (writes=0).</summary>
    public const string ErpSubscriptionsSave = "/erp/subscriptions/save";
    /// <summary>Wave B dry-run for PHP ctr_save (writes=0).</summary>
    public const string ErpContractsSave = "/erp/contracts/save";
    /// <summary>Wave B dry-run for PHP wms_receive (writes=0).</summary>
    public const string ErpWmsReceive = "/erp/wms/receive";
    /// <summary>Wave B dry-run for PHP wms_location_save (writes=0).</summary>
    public const string ErpWmsLocationSave = "/erp/wms/locations/save";
    /// <summary>Wave B dry-run for PHP coll_case_save (writes=0).</summary>
    public const string ErpCollectionsCaseSave = "/erp/collections/cases/save";
    /// <summary>Wave B dry-run for PHP proc_req_save (writes=0).</summary>
    public const string ErpProcurementReqSave = "/erp/procurement/requisitions/save";
    /// <summary>Wave B dry-run for PHP fin_period_status (writes=0).</summary>
    public const string ErpFinPeriodStatus = "/erp/fin/periods/status";
    /// <summary>Wave B dry-run for PHP wms_wave_create (writes=0).</summary>
    public const string ErpWmsWaveCreate = "/erp/wms/waves/create";
    /// <summary>Wave B dry-run for PHP wms_wave_release (writes=0).</summary>
    public const string ErpWmsWaveRelease = "/erp/wms/waves/release";
    /// <summary>Wave B dry-run for PHP wms_work_complete (writes=0).</summary>
    public const string ErpWmsWorkComplete = "/erp/wms/work/complete";
    /// <summary>Wave B dry-run for PHP sub_status (writes=0).</summary>
    public const string ErpSubscriptionsStatus = "/erp/subscriptions/status";
    /// <summary>Wave B dry-run for PHP coll_case_status (writes=0).</summary>
    public const string ErpCollectionsCaseStatus = "/erp/collections/cases/status";
    /// <summary>Wave B dry-run for PHP proc_req_submit (writes=0).</summary>
    public const string ErpProcurementReqSubmit = "/erp/procurement/requisitions/submit";
    /// <summary>Wave B dry-run for PHP proc_req_decision (writes=0).</summary>
    public const string ErpProcurementReqDecision = "/erp/procurement/requisitions/decision";
    /// <summary>Wave B dry-run for PHP wms_location_delete (writes=0).</summary>
    public const string ErpWmsLocationDelete = "/erp/wms/locations/delete";
    /// <summary>Wave B dry-run for PHP invoice_delete draft (writes=0).</summary>
    public const string ErpInvoicesDelete = "/erp/invoices/delete";
    /// <summary>Wave B dry-run for PHP create_account cash/bank (writes=0).</summary>
    public const string ErpCashAccountsCreate = "/erp/cash-accounts/create";
    /// <summary>Wave B dry-run for PHP create_coa (writes=0).</summary>
    public const string ErpCoaAccountsCreate = "/erp/coa-accounts/create";


    /// <summary>Wave B dry-run GL manual journal (PHP gl_manual_entry remains authoritative).</summary>
    public const string ErpGlJournalsManual = "/erp/gl-journals/manual";
    /// <summary>Wave B dry-run GL reverse journal (PHP gl_reverse_journal remains authoritative).</summary>
    public const string ErpGlJournalsReverse = "/erp/gl-journals/reverse";
    /// <summary>Wave B dry-run purchase void (PHP purchase_void remains authoritative).</summary>
    public const string ErpPurchasesVoid = "/erp/purchases/void";
    /// <summary>Wave B dry-run invoice cancel (PHP invoice_cancel remains authoritative).</summary>
    public const string ErpInvoicesCancel = "/erp/invoices/cancel";
    /// <summary>Wave B dry-run sales order cancel (PHP so_cancel remains authoritative).</summary>
    public const string ErpSalesOrdersCancel = "/erp/sales-orders/cancel";
    /// <summary>Wave B dry-run draft PO delete (PHP po_delete remains authoritative).</summary>
    public const string ErpPurchaseOrdersDelete = "/erp/purchase-orders/delete";
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
    /// <summary>Read-only process-flow cases (PHP <c>epc_pf_cases</c>; writes remain PHP processflow UI).</summary>
    public const string ErpProcessFlowTasks = "/erp/process-flow-tasks";
    /// <summary>Process-flow tasks Blazor list (JSON digest remains <see cref="ErpProcessFlowTasks"/>).</summary>
    public const string ErpProcessFlowTasksApp = "/erp/process-flow-tasks-app";
    /// <summary>Read-only ERP report-center registry (PHP <c>epc_rc_registry</c>; CSV/export remain PHP).</summary>
    public const string ErpReportCenter = "/erp/report-center";
    /// <summary>Report-center Blazor list (JSON digest remains <see cref="ErpReportCenter"/>).</summary>
    public const string ErpReportCenterApp = "/erp/report-center-app";
    /// <summary>Read-only AR/AP/inventory aging (PHP <c>epc_erp_aging.php</c>).</summary>
    public const string ErpAging = "/erp/aging";
    /// <summary>Aging Blazor list (JSON digest remains <see cref="ErpAging"/>).</summary>
    public const string ErpAgingApp = "/erp/aging-app";
    /// <summary>Read-only inventory movement ledger (PHP <c>epc_erp_inventory_ledger</c>).</summary>
    public const string ErpStockMovements = "/erp/stock-movements";
    /// <summary>Stock movements Blazor list (JSON digest remains <see cref="ErpStockMovements"/>).</summary>
    public const string ErpStockMovementsApp = "/erp/stock-movements-app";
    /// <summary>ERP fixed assets Blazor list (JSON digest remains <see cref="ErpFixedAssets"/>).</summary>
    public const string ErpFixedAssetsApp = "/erp/fixed-assets-app";
    /// <summary>ERP On-Premises deployment Blazor overview (PHP erp_tabs_on_premises.php remains primary until dual-sample).</summary>
    public const string ErpOnPremisesApp = "/erp/on-premises-app";
    /// <summary>Wave B dry-run for PHP inv_sync_warehouses (writes=0).</summary>
    public const string ErpAjaxInvSyncWarehouses = "/erp/ajax/inv-sync-warehouses";
    /// <summary>Wave B dry-run for PHP inv_create_warehouse (writes=0).</summary>
    public const string ErpAjaxInvCreateWarehouse = "/erp/ajax/inv-create-warehouse";
    /// <summary>Wave B dry-run for PHP inv_create_item (writes=0).</summary>
    public const string ErpAjaxInvCreateItem = "/erp/ajax/inv-create-item";
    /// <summary>Wave B dry-run for PHP inv_set_reorder_level (writes=0).</summary>
    public const string ErpAjaxInvSetReorderLevel = "/erp/ajax/inv-set-reorder-level";
    /// <summary>Wave B dry-run for PHP inv_record_movement (writes=0).</summary>
    public const string ErpAjaxInvRecordMovement = "/erp/ajax/inv-record-movement";
    /// <summary>Wave B dry-run for PHP inv_scan_lookup (writes=0).</summary>
    public const string ErpAjaxInvScanLookup = "/erp/ajax/inv-scan-lookup";
    /// <summary>Wave B dry-run for PHP inv_transfer (writes=0).</summary>
    public const string ErpAjaxInvTransfer = "/erp/ajax/inv-transfer";
    /// <summary>Wave B dry-run for PHP inv_import_csv (writes=0).</summary>
    public const string ErpAjaxInvImportCsv = "/erp/ajax/inv-import-csv";
    /// <summary>Wave B dry-run for PHP inv_run_closing (writes=0).</summary>
    public const string ErpAjaxInvRunClosing = "/erp/ajax/inv-run-closing";
    /// <summary>Wave B dry-run for PHP hr_emp_save (writes=0).</summary>
    public const string ErpAjaxHrEmpSave = "/erp/ajax/hr-emp-save";
    /// <summary>Wave B dry-run for PHP hr_attendance (writes=0).</summary>
    public const string ErpAjaxHrAttendance = "/erp/ajax/hr-attendance";
    /// <summary>Wave B dry-run for PHP hr_leave_request (writes=0).</summary>
    public const string ErpAjaxHrLeaveRequest = "/erp/ajax/hr-leave-request";
    /// <summary>Wave B dry-run for PHP hr_leave_status (writes=0).</summary>
    public const string ErpAjaxHrLeaveStatus = "/erp/ajax/hr-leave-status";
    /// <summary>Wave B dry-run for PHP hr_expense_save (writes=0).</summary>
    public const string ErpAjaxHrExpenseSave = "/erp/ajax/hr-expense-save";
    /// <summary>Wave B dry-run for PHP hr_expense_status (writes=0).</summary>
    public const string ErpAjaxHrExpenseStatus = "/erp/ajax/hr-expense-status";
    /// <summary>Wave B dry-run for PHP hr_update_days (writes=0).</summary>
    public const string ErpAjaxHrUpdateDays = "/erp/ajax/hr-update-days";
    /// <summary>Wave B dry-run for PHP einvoice_create (writes=0).</summary>
    public const string ErpAjaxEinvoiceCreate = "/erp/ajax/einvoice-create";
    /// <summary>Wave B dry-run for PHP einvoice_save_seller (writes=0).</summary>
    public const string ErpAjaxEinvoiceSaveSeller = "/erp/ajax/einvoice-save-seller";
    /// <summary>Wave B dry-run for PHP einvoice_save_buyer (writes=0).</summary>
    public const string ErpAjaxEinvoiceSaveBuyer = "/erp/ajax/einvoice-save-buyer";
    /// <summary>Wave B dry-run for PHP einvoice_save_asp (writes=0).</summary>
    public const string ErpAjaxEinvoiceSaveAsp = "/erp/ajax/einvoice-save-asp";
    /// <summary>Wave B dry-run for PHP einvoice_submit (writes=0).</summary>
    public const string ErpAjaxEinvoiceSubmit = "/erp/ajax/einvoice-submit";
    /// <summary>Wave B dry-run for PHP einvoice_credit_note (writes=0).</summary>
    public const string ErpAjaxEinvoiceCreditNote = "/erp/ajax/einvoice-credit-note";
    /// <summary>Wave B dry-run for PHP einvoice_poll_asp (writes=0).</summary>
    public const string ErpAjaxEinvoicePollAsp = "/erp/ajax/einvoice-poll-asp";
    /// <summary>Wave B dry-run for PHP order_fulfillment_bootstrap (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentBootstrap = "/erp/ajax/order-fulfillment-bootstrap";
    /// <summary>Wave B dry-run for PHP order_fulfillment_status (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentStatus = "/erp/ajax/order-fulfillment-status";
    /// <summary>Wave B dry-run for PHP order_fulfillment_sync (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentSync = "/erp/ajax/order-fulfillment-sync";
    /// <summary>Wave B dry-run for PHP order_fulfillment_post_po (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentPostPo = "/erp/ajax/order-fulfillment-post-po";
    /// <summary>Wave B dry-run for PHP order_fulfillment_post_sales (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentPostSales = "/erp/ajax/order-fulfillment-post-sales";
    /// <summary>Wave B dry-run for PHP order_fulfillment_auto_post (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentAutoPost = "/erp/ajax/order-fulfillment-auto-post";
    /// <summary>Wave B dry-run for PHP order_fulfillment_swap_supplier (writes=0).</summary>
    public const string ErpAjaxOrderFulfillmentSwapSupplier = "/erp/ajax/order-fulfillment-swap-supplier";
    /// <summary>Wave B dry-run for PHP pm_save (writes=0).</summary>
    public const string ErpAjaxPmSave = "/erp/ajax/pm-save";
    /// <summary>Wave B dry-run for PHP pm_toggle (writes=0).</summary>
    public const string ErpAjaxPmToggle = "/erp/ajax/pm-toggle";
    /// <summary>Wave B dry-run for PHP pm_budget_save (writes=0).</summary>
    public const string ErpAjaxPmBudgetSave = "/erp/ajax/pm-budget-save";
    /// <summary>Wave B dry-run for PHP pm_budget_line_save (writes=0).</summary>
    public const string ErpAjaxPmBudgetLineSave = "/erp/ajax/pm-budget-line-save";
    /// <summary>Wave B dry-run for PHP pm_listing_save (writes=0).</summary>
    public const string ErpAjaxPmListingSave = "/erp/ajax/pm-listing-save";
    /// <summary>Wave B dry-run for PHP pm_listing_attach (writes=0).</summary>
    public const string ErpAjaxPmListingAttach = "/erp/ajax/pm-listing-attach";
    /// <summary>Wave B dry-run for PHP pm_cheque_save (writes=0).</summary>
    public const string ErpAjaxPmChequeSave = "/erp/ajax/pm-cheque-save";
    /// <summary>Wave B dry-run for PHP mfgr_wc_save (writes=0).</summary>
    public const string ErpAjaxMfgrWcSave = "/erp/ajax/mfgr-wc-save";
    /// <summary>Wave B dry-run for PHP mfgr_route_save (writes=0).</summary>
    public const string ErpAjaxMfgrRouteSave = "/erp/ajax/mfgr-route-save";
    /// <summary>Wave B dry-run for PHP mfgr_mrp_run (writes=0).</summary>
    public const string ErpAjaxMfgrMrpRun = "/erp/ajax/mfgr-mrp-run";
    /// <summary>Wave B dry-run for PHP mfgr_planned_firm (writes=0).</summary>
    public const string ErpAjaxMfgrPlannedFirm = "/erp/ajax/mfgr-planned-firm";
    /// <summary>Wave B dry-run for PHP qm_plan_save (writes=0).</summary>
    public const string ErpAjaxQmPlanSave = "/erp/ajax/qm-plan-save";
    /// <summary>Wave B dry-run for PHP qm_test_add (writes=0).</summary>
    public const string ErpAjaxQmTestAdd = "/erp/ajax/qm-test-add";
    /// <summary>Wave B dry-run for PHP qm_order_create (writes=0).</summary>
    public const string ErpAjaxQmOrderCreate = "/erp/ajax/qm-order-create";
    /// <summary>Wave B dry-run for PHP qm_order_record (writes=0).</summary>
    public const string ErpAjaxQmOrderRecord = "/erp/ajax/qm-order-record";
    /// <summary>Wave B dry-run for PHP qm_ncr_create (writes=0).</summary>
    public const string ErpAjaxQmNcrCreate = "/erp/ajax/qm-ncr-create";
    /// <summary>Wave B dry-run for PHP qm_ncr_update (writes=0).</summary>
    public const string ErpAjaxQmNcrUpdate = "/erp/ajax/qm-ncr-update";
    /// <summary>Wave B dry-run for PHP rbac_priv_save (writes=0).</summary>
    public const string ErpAjaxRbacPrivSave = "/erp/ajax/rbac-priv-save";
    /// <summary>Wave B dry-run for PHP rbac_duty_save (writes=0).</summary>
    public const string ErpAjaxRbacDutySave = "/erp/ajax/rbac-duty-save";
    /// <summary>Wave B dry-run for PHP rbac_duty_priv (writes=0).</summary>
    public const string ErpAjaxRbacDutyPriv = "/erp/ajax/rbac-duty-priv";
    /// <summary>Wave B dry-run for PHP ajax_newsletter_subscribe.php (writes=0).</summary>
    public const string StorefrontNewsletterSubscribe = "/storefront/newsletter/subscribe";
    /// <summary>Wave B dry-run for PHP content/shop/catalogue/evaluations/ajax_add_evaluation.php (writes=0).</summary>
    public const string StorefrontAddEvaluation = "/storefront/evaluations/add";
    /// <summary>Wave B dry-run for PHP content/shop/finance/ajax_create_operation.php (writes=0).</summary>
    public const string StorefrontCreateOperation = "/storefront/finance/create-operation";
    /// <summary>Wave B dry-run for PHP content/shop/order_process/ajax_check_order_not_authorized.php (writes=0).</summary>
    public const string StorefrontCheckOrderNotAuthorized = "/storefront/orders/check-not-authorized";
    /// <summary>Wave B dry-run for PHP content/users/ajax_set_user_option.php (writes=0).</summary>
    public const string StorefrontSetUserOption = "/storefront/users/set-option";
    /// <summary>Wave B dry-run for PHP modules/shop/geo/ajax_set_my_city.php (writes=0).</summary>
    public const string StorefrontSetMyCity = "/storefront/geo/set-my-city";
    /// <summary>Wave B dry-run for PHP modules/login/code/frontAjax/ajax_sendCode.php (writes=0).</summary>
    public const string StorefrontLoginSendCode = "/storefront/login/send-code";
    /// <summary>Wave B dry-run for PHP modules/login/code/frontAjax/ajax_checkCode.php (writes=0).</summary>
    public const string StorefrontLoginCheckCode = "/storefront/login/check-code";
    /// <summary>Wave B dry-run for PHP cp/content/shop/returns/ajax/ajax_return_action.php (writes=0).</summary>
    public const string CpReturnAction = "/cp/returns/action";
    /// <summary>Wave B dry-run for PHP cp/content/requests/ajax_set_users_vin_viewed.php (writes=0).</summary>
    public const string CpSetUsersVinViewed = "/cp/requests/set-vin-viewed";
    /// <summary>Wave B dry-run for PHP cp/content/users/ajax_set_user_comment.php (writes=0).</summary>
    public const string CpSetUserComment = "/cp/users/set-comment";
    /// <summary>Wave B dry-run for PHP cp/content/shop/prices_upload/ajax_5_import_csv_to_db.php (writes=0).</summary>
    public const string CpPricesImportCsv = "/cp/prices/import-csv";
    /// <summary>Wave B dry-run for PHP cp/content/shop/prices_upload/ajax_6_complete_session.php (writes=0).</summary>
    public const string CpPricesCompleteSession = "/cp/prices/complete-session";

    /// <summary>Wave B dry-run for PHP period_log (writes=0).</summary>
    public const string ErpAjaxPeriodLog = "/erp/ajax/period-log";
    /// <summary>Wave B dry-run for PHP opl_autoplan (writes=0).</summary>
    public const string ErpAjaxOplAutoplan = "/erp/ajax/opl-autoplan";
    /// <summary>Wave B dry-run for PHP opl_seed_demo (writes=0).</summary>
    public const string ErpAjaxOplSeedDemo = "/erp/ajax/opl-seed-demo";
    /// <summary>Wave B dry-run for PHP opl_clear_demo (writes=0).</summary>
    public const string ErpAjaxOplClearDemo = "/erp/ajax/opl-clear-demo";
    /// <summary>Wave B dry-run for PHP pf_set_dept_head (writes=0).</summary>
    public const string ErpAjaxPfSetDeptHead = "/erp/ajax/pf-set-dept-head";
    /// <summary>Wave B dry-run for PHP pf_case_reassign (writes=0).</summary>
    public const string ErpAjaxPfCaseReassign = "/erp/ajax/pf-case-reassign";
    /// <summary>Wave B dry-run for PHP pf_case_cancel (writes=0).</summary>
    public const string ErpAjaxPfCaseCancel = "/erp/ajax/pf-case-cancel";
    /// <summary>Wave B dry-run for PHP pf_seed_demo (writes=0).</summary>
    public const string ErpAjaxPfSeedDemo = "/erp/ajax/pf-seed-demo";
    /// <summary>Wave B dry-run for PHP pf_clear_demo (writes=0).</summary>
    public const string ErpAjaxPfClearDemo = "/erp/ajax/pf-clear-demo";
    /// <summary>Wave B dry-run for PHP pf_sync_orders (writes=0).</summary>
    public const string ErpAjaxPfSyncOrders = "/erp/ajax/pf-sync-orders";
    /// <summary>Wave B dry-run for PHP demo_seed_sales (writes=0).</summary>
    public const string ErpAjaxDemoSeedSales = "/erp/ajax/demo-seed-sales";
    /// <summary>Wave B dry-run for PHP demo_clear_sales (writes=0).</summary>
    public const string ErpAjaxDemoClearSales = "/erp/ajax/demo-clear-sales";
    /// <summary>Wave B dry-run for PHP ctr_ocr (writes=0).</summary>
    public const string ErpAjaxCtrOcr = "/erp/ajax/ctr-ocr";
    /// <summary>Wave B dry-run for PHP docx_save (writes=0).</summary>
    public const string ErpAjaxDocxSave = "/erp/ajax/docx-save";
    /// <summary>Wave B dry-run for PHP docx_delete (writes=0).</summary>
    public const string ErpAjaxDocxDelete = "/erp/ajax/docx-delete";
    /// <summary>Wave B dry-run for PHP docx_run_reminders (writes=0).</summary>
    public const string ErpAjaxDocxRunReminders = "/erp/ajax/docx-run-reminders";
    /// <summary>Wave B dry-run for PHP ins_save (writes=0).</summary>
    public const string ErpAjaxInsSave = "/erp/ajax/ins-save";
    /// <summary>Wave B dry-run for PHP ins_delete (writes=0).</summary>
    public const string ErpAjaxInsDelete = "/erp/ajax/ins-delete";
    /// <summary>Wave B dry-run for PHP ins_doc_add (writes=0).</summary>
    public const string ErpAjaxInsDocAdd = "/erp/ajax/ins-doc-add";
    /// <summary>Wave B dry-run for PHP ins_doc_delete (writes=0).</summary>
    public const string ErpAjaxInsDocDelete = "/erp/ajax/ins-doc-delete";
    /// <summary>Wave B dry-run for PHP ins_claim_add (writes=0).</summary>
    public const string ErpAjaxInsClaimAdd = "/erp/ajax/ins-claim-add";
    /// <summary>Wave B dry-run for PHP fin_periods_generate (writes=0).</summary>
    public const string ErpAjaxFinPeriodsGenerate = "/erp/ajax/fin-periods-generate";
    /// <summary>Wave B dry-run for PHP fin_fx_revalue (writes=0).</summary>
    public const string ErpAjaxFinFxRevalue = "/erp/ajax/fin-fx-revalue";
    /// <summary>Wave B dry-run for PHP fin_alloc_save (writes=0).</summary>
    public const string ErpAjaxFinAllocSave = "/erp/ajax/fin-alloc-save";
    /// <summary>Wave B dry-run for PHP fin_alloc_run (writes=0).</summary>
    public const string ErpAjaxFinAllocRun = "/erp/ajax/fin-alloc-run";
    /// <summary>Wave B dry-run for PHP fin_accrual_save (writes=0).</summary>
    public const string ErpAjaxFinAccrualSave = "/erp/ajax/fin-accrual-save";
    /// <summary>Wave B dry-run for PHP coll_hold_set (writes=0).</summary>
    public const string ErpAjaxCollHoldSet = "/erp/ajax/coll-hold-set";
    /// <summary>Wave B dry-run for PHP bplan_line_add (writes=0).</summary>
    public const string ErpAjaxBplanLineAdd = "/erp/ajax/bplan-line-add";
    /// <summary>Wave B dry-run for PHP bplan_position_add (writes=0).</summary>
    public const string ErpAjaxBplanPositionAdd = "/erp/ajax/bplan-position-add";
    /// <summary>Wave B dry-run for PHP hrt_job_save (writes=0).</summary>
    public const string ErpAjaxHrtJobSave = "/erp/ajax/hrt-job-save";
    /// <summary>Wave B dry-run for PHP hrt_applicant_add (writes=0).</summary>
    public const string ErpAjaxHrtApplicantAdd = "/erp/ajax/hrt-applicant-add";
    /// <summary>Wave B dry-run for PHP hrt_applicant_stage (writes=0).</summary>
    public const string ErpAjaxHrtApplicantStage = "/erp/ajax/hrt-applicant-stage";
    /// <summary>Wave B dry-run for PHP hrt_review_save (writes=0).</summary>
    public const string ErpAjaxHrtReviewSave = "/erp/ajax/hrt-review-save";
    /// <summary>Wave B dry-run for PHP hrt_goal_add (writes=0).</summary>
    public const string ErpAjaxHrtGoalAdd = "/erp/ajax/hrt-goal-add";
    /// <summary>Wave B dry-run for PHP hrt_review_finalize (writes=0).</summary>
    public const string ErpAjaxHrtReviewFinalize = "/erp/ajax/hrt-review-finalize";
    /// <summary>Wave B dry-run for PHP cft_forecast_save (writes=0).</summary>
    public const string ErpAjaxCftForecastSave = "/erp/ajax/cft-forecast-save";
    /// <summary>Wave B dry-run for PHP cft_line_add (writes=0).</summary>
    public const string ErpAjaxCftLineAdd = "/erp/ajax/cft-line-add";
    /// <summary>Wave B dry-run for PHP cft_instrument_save (writes=0).</summary>
    public const string ErpAjaxCftInstrumentSave = "/erp/ajax/cft-instrument-save";
    /// <summary>Wave B dry-run for PHP cft_instrument_status (writes=0).</summary>
    public const string ErpAjaxCftInstrumentStatus = "/erp/ajax/cft-instrument-status";
    /// <summary>Wave B dry-run for PHP wht_code_save (writes=0).</summary>
    public const string ErpAjaxWhtCodeSave = "/erp/ajax/wht-code-save";
    /// <summary>Wave B dry-run for PHP wht_record (writes=0).</summary>
    public const string ErpAjaxWhtRecord = "/erp/ajax/wht-record";
    /// <summary>Wave B dry-run for PHP wht_certificate (writes=0).</summary>
    public const string ErpAjaxWhtCertificate = "/erp/ajax/wht-certificate";
    /// <summary>Wave B dry-run for PHP wht_settle (writes=0).</summary>
    public const string ErpAjaxWhtSettle = "/erp/ajax/wht-settle";
    /// <summary>Wave B dry-run for PHP er_format_save (writes=0).</summary>
    public const string ErpAjaxErFormatSave = "/erp/ajax/er-format-save";
    /// <summary>Wave B dry-run for PHP er_field_add (writes=0).</summary>
    public const string ErpAjaxErFieldAdd = "/erp/ajax/er-field-add";
    /// <summary>Wave B dry-run for PHP prja_budget_save (writes=0).</summary>
    public const string ErpAjaxPrjaBudgetSave = "/erp/ajax/prja-budget-save";
    /// <summary>Wave B dry-run for PHP prja_txn_add (writes=0).</summary>
    public const string ErpAjaxPrjaTxnAdd = "/erp/ajax/prja-txn-add";
    /// <summary>Wave B dry-run for PHP prja_recognize (writes=0).</summary>
    public const string ErpAjaxPrjaRecognize = "/erp/ajax/prja-recognize";
    /// <summary>Wave B dry-run for PHP costm_item_set (writes=0).</summary>
    public const string ErpAjaxCostmItemSet = "/erp/ajax/costm-item-set";
    /// <summary>Wave B dry-run for PHP costm_txn_add (writes=0).</summary>
    public const string ErpAjaxCostmTxnAdd = "/erp/ajax/costm-txn-add";
    /// <summary>Wave B dry-run for PHP costm_close_run (writes=0).</summary>
    public const string ErpAjaxCostmCloseRun = "/erp/ajax/costm-close-run";
    /// <summary>Wave B dry-run for PHP intg_entity_save (writes=0).</summary>
    public const string ErpAjaxIntgEntitySave = "/erp/ajax/intg-entity-save";
    /// <summary>Wave B dry-run for PHP intg_sub_save (writes=0).</summary>
    public const string ErpAjaxIntgSubSave = "/erp/ajax/intg-sub-save";
    /// <summary>Wave B dry-run for PHP intg_event_raise (writes=0).</summary>
    public const string ErpAjaxIntgEventRaise = "/erp/ajax/intg-event-raise";
    /// <summary>Wave B dry-run for PHP fy_create (writes=0).</summary>
    public const string ErpAjaxFyCreate = "/erp/ajax/fy-create";
    /// <summary>Wave B dry-run for PHP fy_close (writes=0).</summary>
    public const string ErpAjaxFyClose = "/erp/ajax/fy-close";
    /// <summary>Wave B dry-run for PHP fy_reopen (writes=0).</summary>
    public const string ErpAjaxFyReopen = "/erp/ajax/fy-reopen";
    /// <summary>Wave B dry-run for PHP fy_period_status (writes=0).</summary>
    public const string ErpAjaxFyPeriodStatus = "/erp/ajax/fy-period-status";
    /// <summary>Wave B dry-run for PHP plt_job_save (writes=0).</summary>
    public const string ErpAjaxPltJobSave = "/erp/ajax/plt-job-save";
    /// <summary>Wave B dry-run for PHP plt_job_run (writes=0).</summary>
    public const string ErpAjaxPltJobRun = "/erp/ajax/plt-job-run";
    /// <summary>Wave B dry-run for PHP plt_feature_save (writes=0).</summary>
    public const string ErpAjaxPltFeatureSave = "/erp/ajax/plt-feature-save";
    /// <summary>Wave B dry-run for PHP oa_party_save (writes=0).</summary>
    public const string ErpAjaxOaPartySave = "/erp/ajax/oa-party-save";
    /// <summary>Wave B dry-run for PHP oa_address_save (writes=0).</summary>
    public const string ErpAjaxOaAddressSave = "/erp/ajax/oa-address-save";
    /// <summary>Wave B dry-run for PHP oa_contact_save (writes=0).</summary>
    public const string ErpAjaxOaContactSave = "/erp/ajax/oa-contact-save";
    /// <summary>Wave B dry-run for PHP oa_calendar_save (writes=0).</summary>
    public const string ErpAjaxOaCalendarSave = "/erp/ajax/oa-calendar-save";
    /// <summary>Wave B dry-run for PHP oa_holiday_add (writes=0).</summary>
    public const string ErpAjaxOaHolidayAdd = "/erp/ajax/oa-holiday-add";
    /// <summary>Wave B dry-run for PHP rbac_role_save (writes=0).</summary>
    public const string ErpAjaxRbacRoleSave = "/erp/ajax/rbac-role-save";
    /// <summary>Wave B dry-run for PHP rbac_role_duty (writes=0).</summary>
    public const string ErpAjaxRbacRoleDuty = "/erp/ajax/rbac-role-duty";
    /// <summary>Wave B dry-run for PHP rbac_user_role (writes=0).</summary>
    public const string ErpAjaxRbacUserRole = "/erp/ajax/rbac-user-role";
    /// <summary>Wave B dry-run for PHP rtl_channel_save (writes=0).</summary>
    public const string ErpAjaxRtlChannelSave = "/erp/ajax/rtl-channel-save";
    /// <summary>Wave B dry-run for PHP cp/content/content/ajax_create_sitemap.php (writes=0).</summary>
    public const string CpCreateSitemap = "/cp/content/create-sitemap";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_save_string_translation.php (writes=0).</summary>
    public const string CpLangSaveTranslation = "/cp/lang/save-translation";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_save_string_description.php (writes=0).</summary>
    public const string CpLangSaveDescription = "/cp/lang/save-description";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_create_new_string.php (writes=0).</summary>
    public const string CpLangCreateString = "/cp/lang/create-string";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_delete_not_used_found.php (writes=0).</summary>
    public const string CpLangDeleteNotUsed = "/cp/lang/delete-not-used";
    /// <summary>Wave B dry-run for PHP cp/content/packs_control/ajax_delete_pack.php (writes=0).</summary>
    public const string CpPacksDelete = "/cp/packs/delete";
    /// <summary>Wave B dry-run for PHP cp/content/shop/channels/ajax_channels.php (writes=0).</summary>
    public const string CpChannelsWrite = "/cp/channels/write";
    /// <summary>Wave B dry-run for PHP cp/content/shop/logistics/ajax_logistics.php (writes=0).</summary>
    public const string CpLogisticsWrite = "/cp/logistics/write";
    /// <summary>Wave B dry-run for PHP cp/content/shop/payments/ajax_payments.php (writes=0).</summary>
    public const string CpPaymentsWrite = "/cp/payments/write";
    /// <summary>Wave B dry-run for PHP cp/content/shop/workshop/ajax_workshop_endpoint.php (writes=0).</summary>
    public const string CpWorkshopWrite = "/cp/workshop/write";
    /// <summary>Wave B dry-run for PHP cp/content/shop/catalogue/categories_templates/ajax_templates_actions.php (writes=0).</summary>
    public const string CpTemplatesActions = "/cp/catalogue/templates-actions";
    /// <summary>Wave B dry-run for PHP cp/content/shop/prices_upload/price_review/ajax_price_review.php (writes=0).</summary>
    public const string CpPriceReviewWrite = "/cp/prices/review";
    /// <summary>Wave B dry-run for PHP cp/content/shop/prices_upload/price_review/ajax_create_csv.php (writes=0).</summary>
    public const string CpPriceReviewCreateCsv = "/cp/prices/review-create-csv";
    /// <summary>Wave B dry-run for PHP cp/content/shop/accessories/ajax_epc_accessories_photos.php (writes=0).</summary>
    public const string CpAccessoriesPhotos = "/cp/accessories/photos";
    /// <summary>Wave B dry-run for PHP cp/content/control/version_control/ajax/ajax_clear_updates_dir.php (writes=0).</summary>
    public const string CpVersionClearUpdates = "/cp/version/clear-updates";
    /// <summary>Wave B dry-run for PHP content/shop/bulk_upload/ajax_process.php (writes=0).</summary>
    public const string StorefrontBulkUploadProcess = "/storefront/bulk-upload/process";

    /// <summary>Wave B dry-run for PHP concurrency_status (writes=0).</summary>
    public const string ErpAjaxConcurrencyStatus = "/erp/ajax/concurrency-status";
    /// <summary>Wave B dry-run for PHP settlement_open_docs (writes=0).</summary>
    public const string ErpAjaxSettlementOpenDocs = "/erp/ajax/settlement-open-docs";
    /// <summary>Wave B dry-run for PHP dashboard (writes=0).</summary>
    public const string ErpAjaxDashboard = "/erp/ajax/dashboard";
    /// <summary>Wave B dry-run for PHP command_center (writes=0).</summary>
    public const string ErpAjaxCommandCenter = "/erp/ajax/command-center";
    /// <summary>Wave B dry-run for PHP cc_kpi_tiles (writes=0).</summary>
    public const string ErpAjaxCcKpiTiles = "/erp/ajax/cc-kpi-tiles";
    /// <summary>Wave B dry-run for PHP cc_approval_queue (writes=0).</summary>
    public const string ErpAjaxCcApprovalQueue = "/erp/ajax/cc-approval-queue";
    /// <summary>Wave B dry-run for PHP period_list (writes=0).</summary>
    public const string ErpAjaxPeriodList = "/erp/ajax/period-list";
    /// <summary>Wave B dry-run for PHP period_checklist (writes=0).</summary>
    public const string ErpAjaxPeriodChecklist = "/erp/ajax/period-checklist";
    /// <summary>Wave B dry-run for PHP period_summary (writes=0).</summary>
    public const string ErpAjaxPeriodSummary = "/erp/ajax/period-summary";
    /// <summary>Wave B dry-run for PHP fx_revaluation_preview (writes=0).</summary>
    public const string ErpAjaxFxRevaluationPreview = "/erp/ajax/fx-revaluation-preview";
    /// <summary>Wave B dry-run for PHP bos_compliance_fetch (writes=0).</summary>
    public const string ErpAjaxBosComplianceFetch = "/erp/ajax/bos-compliance-fetch";
    /// <summary>Wave B dry-run for PHP rtl_assortment_set (writes=0).</summary>
    public const string ErpAjaxRtlAssortmentSet = "/erp/ajax/rtl-assortment-set";
    /// <summary>Wave B dry-run for PHP rtl_discount_save (writes=0).</summary>
    public const string ErpAjaxRtlDiscountSave = "/erp/ajax/rtl-discount-save";
    /// <summary>Wave B dry-run for PHP rtl_pos_sale (writes=0).</summary>
    public const string ErpAjaxRtlPosSale = "/erp/ajax/rtl-pos-sale";
    /// <summary>Wave B dry-run for PHP ins_claim_status (writes=0).</summary>
    public const string ErpAjaxInsClaimStatus = "/erp/ajax/ins-claim-status";
    /// <summary>Wave B dry-run for PHP prj_save (writes=0).</summary>
    public const string ErpAjaxPrjSave = "/erp/ajax/prj-save";
    /// <summary>Wave B dry-run for PHP prj_task_save (writes=0).</summary>
    public const string ErpAjaxPrjTaskSave = "/erp/ajax/prj-task-save";
    /// <summary>Wave B dry-run for PHP prj_log_time (writes=0).</summary>
    public const string ErpAjaxPrjLogTime = "/erp/ajax/prj-log-time";
    /// <summary>Wave B dry-run for PHP cons_entity_save (writes=0).</summary>
    public const string ErpAjaxConsEntitySave = "/erp/ajax/cons-entity-save";
    /// <summary>Wave B dry-run for PHP cons_entity_delete (writes=0).</summary>
    public const string ErpAjaxConsEntityDelete = "/erp/ajax/cons-entity-delete";
    /// <summary>Wave B dry-run for PHP cons_figures_save (writes=0).</summary>
    public const string ErpAjaxConsFiguresSave = "/erp/ajax/cons-figures-save";
    /// <summary>Wave B dry-run for PHP cons_ic_save (writes=0).</summary>
    public const string ErpAjaxConsIcSave = "/erp/ajax/cons-ic-save";
    /// <summary>Wave B dry-run for PHP cons_ic_delete (writes=0).</summary>
    public const string ErpAjaxConsIcDelete = "/erp/ajax/cons-ic-delete";
    /// <summary>Wave B dry-run for PHP mfg_bom_save (writes=0).</summary>
    public const string ErpAjaxMfgBomSave = "/erp/ajax/mfg-bom-save";
    /// <summary>Wave B dry-run for PHP mfg_wo_create (writes=0).</summary>
    public const string ErpAjaxMfgWoCreate = "/erp/ajax/mfg-wo-create";
    /// <summary>Wave B dry-run for PHP mfg_wo_issue (writes=0).</summary>
    public const string ErpAjaxMfgWoIssue = "/erp/ajax/mfg-wo-issue";
    /// <summary>Wave B dry-run for PHP mfg_wo_complete (writes=0).</summary>
    public const string ErpAjaxMfgWoComplete = "/erp/ajax/mfg-wo-complete";
    /// <summary>Wave B dry-run for PHP payroll_generate (writes=0).</summary>
    public const string ErpAjaxPayrollGenerate = "/erp/ajax/payroll-generate";
    /// <summary>Wave B dry-run for PHP payroll_approve (writes=0).</summary>
    public const string ErpAjaxPayrollApprove = "/erp/ajax/payroll-approve";
    /// <summary>Wave B dry-run for PHP payroll_pay (writes=0).</summary>
    public const string ErpAjaxPayrollPay = "/erp/ajax/payroll-pay";
    /// <summary>Wave B dry-run for PHP payroll_update_days (writes=0).</summary>
    public const string ErpAjaxPayrollUpdateDays = "/erp/ajax/payroll-update-days";
    /// <summary>Wave B dry-run for PHP uae_tax_fta_fetch (writes=0).</summary>
    public const string ErpAjaxUaeTaxFtaFetch = "/erp/ajax/uae-tax-fta-fetch";
    /// <summary>Wave B dry-run for PHP aml_check (writes=0).</summary>
    public const string ErpAjaxAmlCheck = "/erp/ajax/aml-check";
    /// <summary>Wave B dry-run for PHP aml_report_generate (writes=0).</summary>
    public const string ErpAjaxAmlReportGenerate = "/erp/ajax/aml-report-generate";
    /// <summary>Wave B dry-run for PHP aml_seed_rules (writes=0).</summary>
    public const string ErpAjaxAmlSeedRules = "/erp/ajax/aml-seed-rules";
    /// <summary>Wave B dry-run for PHP uae_tax_legislation_regen_summaries (writes=0).</summary>
    public const string ErpAjaxUaeTaxLegislationRegenSummaries = "/erp/ajax/uae-tax-legislation-regen-summaries";
    /// <summary>Wave B dry-run for PHP uae_tax_legislation_ask (writes=0).</summary>
    public const string ErpAjaxUaeTaxLegislationAsk = "/erp/ajax/uae-tax-legislation-ask";
    /// <summary>Wave B dry-run for PHP uae_tax_save_ct_adjustments (writes=0).</summary>
    public const string ErpAjaxUaeTaxSaveCtAdjustments = "/erp/ajax/uae-tax-save-ct-adjustments";
    /// <summary>Wave B dry-run for PHP uae_tax_legislation_checklist_set (writes=0).</summary>
    public const string ErpAjaxUaeTaxLegislationChecklistSet = "/erp/ajax/uae-tax-legislation-checklist-set";
    /// <summary>Wave B dry-run for PHP invoice_save (writes=0).</summary>
    public const string ErpAjaxInvoiceSave = "/erp/ajax/invoice-save";
    /// <summary>Wave B dry-run for PHP invoice_list (writes=0).</summary>
    public const string ErpAjaxInvoiceList = "/erp/ajax/invoice-list";
    /// <summary>Wave B dry-run for PHP invoice_from_order (writes=0).</summary>
    public const string ErpAjaxInvoiceFromOrder = "/erp/ajax/invoice-from-order";
    /// <summary>Wave B dry-run for PHP ai_query (writes=0).</summary>
    public const string ErpAjaxAiQuery = "/erp/ajax/ai-query";
    /// <summary>Wave B dry-run for PHP integrity_scan (writes=0).</summary>
    public const string ErpAjaxIntegrityScan = "/erp/ajax/integrity-scan";
    /// <summary>Wave B dry-run for PHP integrity_apply_fks (writes=0).</summary>
    public const string ErpAjaxIntegrityApplyFks = "/erp/ajax/integrity-apply-fks";
    /// <summary>Wave B dry-run for PHP fa_create_asset (writes=0).</summary>
    public const string ErpAjaxFaCreateAsset = "/erp/ajax/fa-create-asset";
    /// <summary>Wave B dry-run for PHP fa_run_depreciation (writes=0).</summary>
    public const string ErpAjaxFaRunDepreciation = "/erp/ajax/fa-run-depreciation";
    /// <summary>Wave B dry-run for PHP opening_create_batch (writes=0).</summary>
    public const string ErpAjaxOpeningCreateBatch = "/erp/ajax/opening-create-batch";
    /// <summary>Wave B dry-run for PHP opening_add_coa_line (writes=0).</summary>
    public const string ErpAjaxOpeningAddCoaLine = "/erp/ajax/opening-add-coa-line";
    /// <summary>Wave B dry-run for PHP opening_add_inv_line (writes=0).</summary>
    public const string ErpAjaxOpeningAddInvLine = "/erp/ajax/opening-add-inv-line";
    /// <summary>Wave B dry-run for PHP opening_post_batch (writes=0).</summary>
    public const string ErpAjaxOpeningPostBatch = "/erp/ajax/opening-post-batch";
    /// <summary>Wave B dry-run for PHP save_rfq (writes=0).</summary>
    public const string ErpAjaxSaveRfq = "/erp/ajax/save-rfq";
    /// <summary>Wave B dry-run for PHP delivery_note_create (writes=0).</summary>
    public const string ErpAjaxDeliveryNoteCreate = "/erp/ajax/delivery-note-create";
    /// <summary>Wave B dry-run for PHP save_contact (writes=0).</summary>
    public const string ErpAjaxSaveContact = "/erp/ajax/save-contact";
    /// <summary>Wave B dry-run for PHP sync_contacts (writes=0).</summary>
    public const string ErpAjaxSyncContacts = "/erp/ajax/sync-contacts";
    /// <summary>Wave B dry-run for PHP document_upload (writes=0).</summary>
    public const string ErpAjaxDocumentUpload = "/erp/ajax/document-upload";
    /// <summary>Wave B dry-run for PHP document_delete (writes=0).</summary>
    public const string ErpAjaxDocumentDelete = "/erp/ajax/document-delete";
    /// <summary>Wave B dry-run for PHP save_company (writes=0).</summary>
    public const string ErpAjaxSaveCompany = "/erp/ajax/save-company";
    /// <summary>Wave B dry-run for PHP save_template (writes=0).</summary>
    public const string ErpAjaxSaveTemplate = "/erp/ajax/save-template";
    /// <summary>Wave B dry-run for PHP upload_logo (writes=0).</summary>
    public const string ErpAjaxUploadLogo = "/erp/ajax/upload-logo";
    /// <summary>Wave B dry-run for PHP upload_attachment (writes=0).</summary>
    public const string ErpAjaxUploadAttachment = "/erp/ajax/upload-attachment";
    /// <summary>Wave B dry-run for PHP delete_attachment (writes=0).</summary>
    public const string ErpAjaxDeleteAttachment = "/erp/ajax/delete-attachment";
    /// <summary>Wave B dry-run for PHP sync_einvoice_seller (writes=0).</summary>
    public const string ErpAjaxSyncEinvoiceSeller = "/erp/ajax/sync-einvoice-seller";
    /// <summary>Wave B dry-run for PHP expense_report_save (writes=0).</summary>
    public const string ErpAjaxExpenseReportSave = "/erp/ajax/expense-report-save";
    /// <summary>Wave B dry-run for PHP po_save (writes=0).</summary>
    public const string ErpAjaxPoSave = "/erp/ajax/po-save";
    /// <summary>Wave B dry-run for PHP po_status (writes=0).</summary>
    public const string ErpAjaxPoStatus = "/erp/ajax/po-status";
    /// <summary>Wave B dry-run for PHP po_receive_lines (writes=0).</summary>
    public const string ErpAjaxPoReceiveLines = "/erp/ajax/po-receive-lines";
    /// <summary>Wave B dry-run for PHP po_to_invoice (writes=0).</summary>
    public const string ErpAjaxPoToInvoice = "/erp/ajax/po-to-invoice";
    /// <summary>Wave B dry-run for PHP customer_create (writes=0).</summary>
    public const string ErpAjaxCustomerCreate = "/erp/ajax/customer-create";
    /// <summary>Wave B dry-run for PHP so_save (writes=0).</summary>
    public const string ErpAjaxSoSave = "/erp/ajax/so-save";
    /// <summary>Wave B dry-run for PHP so_status (writes=0).</summary>
    public const string ErpAjaxSoStatus = "/erp/ajax/so-status";
    /// <summary>Wave B dry-run for PHP so_to_invoice (writes=0).</summary>
    public const string ErpAjaxSoToInvoice = "/erp/ajax/so-to-invoice";
    /// <summary>Wave B dry-run for PHP transfer_voucher (writes=0).</summary>
    public const string ErpAjaxTransferVoucher = "/erp/ajax/transfer-voucher";
    /// <summary>Wave B dry-run for PHP payment_batch_save (writes=0).</summary>
    public const string ErpAjaxPaymentBatchSave = "/erp/ajax/payment-batch-save";
    /// <summary>Wave B dry-run for PHP petty_cash_save (writes=0).</summary>
    public const string ErpAjaxPettyCashSave = "/erp/ajax/petty-cash-save";
    /// <summary>Wave B dry-run for PHP agenda_save (writes=0).</summary>
    public const string ErpAjaxAgendaSave = "/erp/ajax/agenda-save";
    /// <summary>Wave B dry-run for PHP kb_save (writes=0).</summary>
    public const string ErpAjaxKbSave = "/erp/ajax/kb-save";
    /// <summary>Wave B dry-run for PHP multi_entity_save (writes=0).</summary>
    public const string ErpAjaxMultiEntitySave = "/erp/ajax/multi-entity-save";
    /// <summary>Wave B dry-run for PHP cs_save_declaration (writes=0).</summary>
    public const string ErpAjaxCsSaveDeclaration = "/erp/ajax/cs-save-declaration";
    /// <summary>Wave B dry-run for PHP cs_submit_declaration (writes=0).</summary>
    public const string ErpAjaxCsSubmitDeclaration = "/erp/ajax/cs-submit-declaration";
    /// <summary>Wave B dry-run for PHP cs_delete_declaration (writes=0).</summary>
    public const string ErpAjaxCsDeleteDeclaration = "/erp/ajax/cs-delete-declaration";
    /// <summary>Wave B dry-run for PHP cs_list_declarations (writes=0).</summary>
    public const string ErpAjaxCsListDeclarations = "/erp/ajax/cs-list-declarations";
    /// <summary>Wave B dry-run for PHP cs_import_declaration_pdf (writes=0).</summary>
    public const string ErpAjaxCsImportDeclarationPdf = "/erp/ajax/cs-import-declaration-pdf";
    /// <summary>Wave B dry-run for PHP shortcut_list (writes=0).</summary>
    public const string ErpAjaxShortcutList = "/erp/ajax/shortcut-list";
    /// <summary>Wave B dry-run for PHP shortcut_add (writes=0).</summary>
    public const string ErpAjaxShortcutAdd = "/erp/ajax/shortcut-add";
    /// <summary>Wave B dry-run for PHP shortcut_delete (writes=0).</summary>
    public const string ErpAjaxShortcutDelete = "/erp/ajax/shortcut-delete";
    /// <summary>Wave B dry-run for PHP shortcut_delete_key (writes=0).</summary>
    public const string ErpAjaxShortcutDeleteKey = "/erp/ajax/shortcut-delete-key";
    /// <summary>Wave B dry-run for PHP shortcut_reset (writes=0).</summary>
    public const string ErpAjaxShortcutReset = "/erp/ajax/shortcut-reset";
    /// <summary>Wave B dry-run for PHP shortcut_reorder (writes=0).</summary>
    public const string ErpAjaxShortcutReorder = "/erp/ajax/shortcut-reorder";
    /// <summary>Wave B dry-run for PHP erp_fav_add (writes=0).</summary>
    public const string ErpAjaxErpFavAdd = "/erp/ajax/erp-fav-add";
    /// <summary>Wave B dry-run for PHP erp_fav_remove (writes=0).</summary>
    public const string ErpAjaxErpFavRemove = "/erp/ajax/erp-fav-remove";
    /// <summary>Wave B dry-run for PHP erp_global_search (writes=0).</summary>
    public const string ErpAjaxErpGlobalSearch = "/erp/ajax/erp-global-search";
    /// <summary>Wave B dry-run for PHP jw_repair_create (writes=0).</summary>
    public const string ErpAjaxJwRepairCreate = "/erp/ajax/jw-repair-create";
    /// <summary>Wave B dry-run for PHP jw_repair_update_status (writes=0).</summary>
    public const string ErpAjaxJwRepairUpdateStatus = "/erp/ajax/jw-repair-update-status";
    /// <summary>Wave B dry-run for PHP jw_seed_sample_data (writes=0).</summary>
    public const string ErpAjaxJwSeedSampleData = "/erp/ajax/jw-seed-sample-data";
    /// <summary>Wave B dry-run for PHP ai_assistant_query (writes=0).</summary>
    public const string ErpAjaxAiAssistantQuery = "/erp/ajax/ai-assistant-query";
    /// <summary>Wave B dry-run for PHP print_designer_save (writes=0).</summary>
    public const string ErpAjaxPrintDesignerSave = "/erp/ajax/print-designer-save";
    /// <summary>Wave B dry-run for PHP workflow_save (writes=0).</summary>
    public const string ErpAjaxWorkflowSave = "/erp/ajax/workflow-save";
    /// <summary>Wave B dry-run for PHP workflow_run (writes=0).</summary>
    public const string ErpAjaxWorkflowRun = "/erp/ajax/workflow-run";
    /// <summary>Wave B dry-run for PHP automation_activate (writes=0).</summary>
    public const string ErpAjaxAutomationActivate = "/erp/ajax/automation-activate";
    /// <summary>Wave B dry-run for PHP automation_deactivate (writes=0).</summary>
    public const string ErpAjaxAutomationDeactivate = "/erp/ajax/automation-deactivate";
    /// <summary>Wave B dry-run for PHP automation_install_template (writes=0).</summary>
    public const string ErpAjaxAutomationInstallTemplate = "/erp/ajax/automation-install-template";
    /// <summary>Wave B dry-run for PHP automation_enable_category (writes=0).</summary>
    public const string ErpAjaxAutomationEnableCategory = "/erp/ajax/automation-enable-category";
    /// <summary>Wave B dry-run for PHP automation_tick (writes=0).</summary>
    public const string ErpAjaxAutomationTick = "/erp/ajax/automation-tick";
    /// <summary>Wave B dry-run for PHP tenant_config_save (writes=0).</summary>
    public const string ErpAjaxTenantConfigSave = "/erp/ajax/tenant-config-save";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_set_is_custom.php (writes=0).</summary>
    public const string CpLangSetIsCustom = "/cp/lang/set-is-custom";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_set_is_error.php (writes=0).</summary>
    public const string CpLangSetIsError = "/cp/lang/set-is-error";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_set_same.php (writes=0).</summary>
    public const string CpLangSetSame = "/cp/lang/set-same";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_set_used_found.php (writes=0).</summary>
    public const string CpLangSetUsedFound = "/cp/lang/set-used-found";
    /// <summary>Wave B dry-run for PHP cp/content/lang/ajax_search_used_found.php (writes=0).</summary>
    public const string CpLangSearchUsedFound = "/cp/lang/search-used-found";
    /// <summary>Wave B dry-run for PHP cp/content/control/version_control/ajax/ajax_get_update_pack.php (writes=0).</summary>
    public const string CpVersionGetUpdatePack = "/cp/version/get-update-pack";
    /// <summary>Wave B dry-run for PHP content/shop/docpart/ajax_get_article_list.php (writes=0).</summary>
    public const string StorefrontGetArticleList = "/storefront/catalogue/get-article-list";
    /// <summary>Wave B dry-run for PHP content/shop/returns/ajax/ajax_load_returns_data.php (writes=0).</summary>
    public const string StorefrontLoadReturnsData = "/storefront/returns/load-data";

    /// <summary>Full ajax_erp.php action catalog (dedicated + registry coverage). cutoverAllowed=false.</summary>
    public const string ErpAjaxWriteCatalog = "/erp/ajax-writes/catalog";
    /// <summary>Generic Wave B dry-run for any catalogued ajax_erp.php action (writes=0; PHP authoritative).</summary>
    public const string ErpAjaxWriteRegistryDryRun = "/erp/ajax-writes/dry-run/{action}";
    /// <summary>Wave B dry-run for PHP deploy/on-premises/setup-wizard.php (writes=0).</summary>
    public const string ErpOnPremisesSetupWizardDryRun = "/erp/on-premises/setup-wizard-dry-run";
    /// <summary>Wave B dry-run for PHP deploy/on-premises/backup.php (writes=0).</summary>
    public const string ErpOnPremisesBackupDryRun = "/erp/on-premises/backup-dry-run";
    /// <summary>Wave B dry-run for PHP edit_lock_acquire (writes=0).</summary>
    public const string ErpAjaxEditLockAcquire = "/erp/ajax/edit-lock-acquire";
    /// <summary>Wave B dry-run for PHP edit_lock_heartbeat (writes=0).</summary>
    public const string ErpAjaxEditLockHeartbeat = "/erp/ajax/edit-lock-heartbeat";
    /// <summary>Wave B dry-run for PHP edit_lock_release (writes=0).</summary>
    public const string ErpAjaxEditLockRelease = "/erp/ajax/edit-lock-release";
    /// <summary>Wave B dry-run for PHP presence_heartbeat (writes=0).</summary>
    public const string ErpAjaxPresenceHeartbeat = "/erp/ajax/presence-heartbeat";
    /// <summary>Wave B dry-run for PHP bos_compliance_add_obligation (writes=0).</summary>
    public const string ErpAjaxBosComplianceAddObligation = "/erp/ajax/bos-compliance-add-obligation";
    /// <summary>Wave B dry-run for PHP bos_compliance_disable_obligation (writes=0).</summary>
    public const string ErpAjaxBosComplianceDisableObligation = "/erp/ajax/bos-compliance-disable-obligation";
    /// <summary>Wave B dry-run for PHP bos_compliance_file (writes=0).</summary>
    public const string ErpAjaxBosComplianceFile = "/erp/ajax/bos-compliance-file";
    /// <summary>Wave B dry-run for PHP bos_compliance_save_retention (writes=0).</summary>
    public const string ErpAjaxBosComplianceSaveRetention = "/erp/ajax/bos-compliance-save-retention";
    /// <summary>Wave B dry-run for PHP bos_wf_save_rule (writes=0).</summary>
    public const string ErpAjaxBosWfSaveRule = "/erp/ajax/bos-wf-save-rule";
    /// <summary>Wave B dry-run for PHP bos_wf_disable_rule (writes=0).</summary>
    public const string ErpAjaxBosWfDisableRule = "/erp/ajax/bos-wf-disable-rule";
    /// <summary>Wave B dry-run for PHP bos_wf_decide (writes=0).</summary>
    public const string ErpAjaxBosWfDecide = "/erp/ajax/bos-wf-decide";
    /// <summary>Wave B dry-run for PHP bos_wf_raise_test (writes=0).</summary>
    public const string ErpAjaxBosWfRaiseTest = "/erp/ajax/bos-wf-raise-test";
    /// <summary>Wave B dry-run for PHP bos_intel_toggle_control (writes=0).</summary>
    public const string ErpAjaxBosIntelToggleControl = "/erp/ajax/bos-intel-toggle-control";
    /// <summary>Wave B dry-run for PHP bos_vat_refund_save (writes=0).</summary>
    public const string ErpAjaxBosVatRefundSave = "/erp/ajax/bos-vat-refund-save";
    /// <summary>Wave B dry-run for PHP bos_vat_refund_status (writes=0).</summary>
    public const string ErpAjaxBosVatRefundStatus = "/erp/ajax/bos-vat-refund-status";
    /// <summary>Wave B dry-run for PHP opl_params_save (writes=0).</summary>
    public const string ErpAjaxOplParamsSave = "/erp/ajax/opl-params-save";
    /// <summary>Wave B dry-run for PHP opl_set_status (writes=0).</summary>
    public const string ErpAjaxOplSetStatus = "/erp/ajax/opl-set-status";
    /// <summary>Wave B dry-run for PHP opl_confirm_all (writes=0).</summary>
    public const string ErpAjaxOplConfirmAll = "/erp/ajax/opl-confirm-all";
    /// <summary>Wave B dry-run for PHP opl_create_pos (writes=0).</summary>
    public const string ErpAjaxOplCreatePos = "/erp/ajax/opl-create-pos";
    /// <summary>Wave B dry-run for PHP pf_process_save (writes=0).</summary>
    public const string ErpAjaxPfProcessSave = "/erp/ajax/pf-process-save";
    /// <summary>Wave B dry-run for PHP pf_step_save (writes=0).</summary>
    public const string ErpAjaxPfStepSave = "/erp/ajax/pf-step-save";
    /// <summary>Wave B dry-run for PHP pf_step_delete (writes=0).</summary>
    public const string ErpAjaxPfStepDelete = "/erp/ajax/pf-step-delete";
    /// <summary>Wave B dry-run for PHP pf_case_start (writes=0).</summary>
    public const string ErpAjaxPfCaseStart = "/erp/ajax/pf-case-start";
    /// <summary>Wave B dry-run for PHP pf_case_act (writes=0).</summary>
    public const string ErpAjaxPfCaseAct = "/erp/ajax/pf-case-act";
    /// <summary>Wave B dry-run for PHP sub_generate (writes=0).</summary>
    public const string ErpAjaxSubGenerate = "/erp/ajax/sub-generate";
    /// <summary>Wave B dry-run for PHP sub_invoice_paid (writes=0).</summary>
    public const string ErpAjaxSubInvoicePaid = "/erp/ajax/sub-invoice-paid";
    /// <summary>Wave B dry-run for PHP ctr_status (writes=0).</summary>
    public const string ErpAjaxCtrStatus = "/erp/ajax/ctr-status";
    /// <summary>Wave B dry-run for PHP ctr_sign (writes=0).</summary>
    public const string ErpAjaxCtrSign = "/erp/ajax/ctr-sign";
    /// <summary>Wave B dry-run for PHP coll_case_promise (writes=0).</summary>
    public const string ErpAjaxCollCasePromise = "/erp/ajax/coll-case-promise";
    /// <summary>Wave B dry-run for PHP coll_activity_log (writes=0).</summary>
    public const string ErpAjaxCollActivityLog = "/erp/ajax/coll-activity-log";
    /// <summary>Wave B dry-run for PHP coll_dunning_run (writes=0).</summary>
    public const string ErpAjaxCollDunningRun = "/erp/ajax/coll-dunning-run";
    /// <summary>Wave B dry-run for PHP proc_category_save (writes=0).</summary>
    public const string ErpAjaxProcCategorySave = "/erp/ajax/proc-category-save";
    /// <summary>Wave B dry-run for PHP proc_policy_save (writes=0).</summary>
    public const string ErpAjaxProcPolicySave = "/erp/ajax/proc-policy-save";
    /// <summary>Wave B dry-run for PHP proc_req_add_line (writes=0).</summary>
    public const string ErpAjaxProcReqAddLine = "/erp/ajax/proc-req-add-line";
    /// <summary>Wave B dry-run for PHP proc_req_convert (writes=0).</summary>
    public const string ErpAjaxProcReqConvert = "/erp/ajax/proc-req-convert";
    /// <summary>Wave B dry-run for PHP bplan_save (writes=0).</summary>
    public const string ErpAjaxBplanSave = "/erp/ajax/bplan-save";
    /// <summary>Wave B dry-run for PHP bplan_advance (writes=0).</summary>
    public const string ErpAjaxBplanAdvance = "/erp/ajax/bplan-advance";
    /// <summary>Wave B dry-run for PHP aml_kyc_save (writes=0).</summary>
    public const string ErpAjaxAmlKycSave = "/erp/ajax/aml-kyc-save";
    /// <summary>Wave B dry-run for PHP aml_alert_status (writes=0).</summary>
    public const string ErpAjaxAmlAlertStatus = "/erp/ajax/aml-alert-status";
    /// <summary>Wave B dry-run for PHP aml_settings_save (writes=0).</summary>
    public const string ErpAjaxAmlSettingsSave = "/erp/ajax/aml-settings-save";
    /// <summary>Wave B dry-run for PHP bank_import (writes=0).</summary>
    public const string ErpAjaxBankImport = "/erp/ajax/bank-import";
    /// <summary>Wave B dry-run for PHP bank_reconcile (writes=0).</summary>
    public const string ErpAjaxBankReconcile = "/erp/ajax/bank-reconcile";
    /// <summary>Wave B dry-run for PHP fx_post_revaluation (writes=0).</summary>
    public const string ErpAjaxFxPostRevaluation = "/erp/ajax/fx-post-revaluation";
    /// <summary>Wave B dry-run for PHP supplier_payment (writes=0).</summary>
    public const string ErpAjaxSupplierPayment = "/erp/ajax/supplier-payment";

    /// <summary>Wave B dry-run for PHP api/v1/on-premises/health.php (writes=0; PHP authoritative).</summary>
    public const string ErpOnPremisesHealthDryRun = "/erp/on-premises/health-dry-run";
    /// <summary>Wave B dry-run for PHP api/v1/licenses/activate.php (writes=0; PHP authoritative).</summary>
    public const string ErpOnPremisesLicenseActivateDryRun = "/erp/on-premises/license-activate-dry-run";
    /// <summary>Read-only on-premises license registry digest (notes/fingerprint/ip omitted; keys masked).</summary>
    public const string ErpOnPremisesLicenses = "/erp/on-premises/licenses";
    /// <summary>Full bos/ajax_epc_bos.php action catalog. cutoverAllowed=false.</summary>
    public const string BosAjaxWriteCatalog = "/bos/ajax-writes/catalog";
    /// <summary>Generic Wave B dry-run for any catalogued BOS ajax action (writes=0).</summary>
    public const string BosAjaxWriteRegistryDryRun = "/bos/ajax-writes/dry-run/{action}";
    /// <summary>Wave B dry-run for BOS PHP mfa_policy (writes=0).</summary>
    public const string BosAjaxMfaPolicy = "/bos/ajax/mfa-policy";
    /// <summary>Wave B dry-run for BOS PHP design_tokens (writes=0).</summary>
    public const string BosAjaxDesignTokens = "/bos/ajax/design-tokens";
    /// <summary>Wave B dry-run for BOS PHP credit_limit (writes=0).</summary>
    public const string BosAjaxCreditLimit = "/bos/ajax/credit-limit";
    /// <summary>Wave B dry-run for BOS PHP run_audit (writes=0).</summary>
    public const string BosAjaxRunAudit = "/bos/ajax/run-audit";
    /// <summary>Wave B dry-run for BOS PHP save (writes=0).</summary>
    public const string BosAjaxSave = "/bos/ajax/save";
    /// <summary>Wave B dry-run for BOS PHP update (writes=0).</summary>
    public const string BosAjaxUpdate = "/bos/ajax/update";
    /// <summary>Wave B dry-run for BOS PHP delete (writes=0).</summary>
    public const string BosAjaxDelete = "/bos/ajax/delete";
    /// <summary>Wave B dry-run for BOS PHP get_tokens (writes=0).</summary>
    public const string BosAjaxGetTokens = "/bos/ajax/get-tokens";
    /// <summary>Wave B dry-run for BOS PHP save_token (writes=0).</summary>
    public const string BosAjaxSaveToken = "/bos/ajax/save-token";
    /// <summary>Wave B dry-run for BOS PHP prefs_get (writes=0).</summary>
    public const string BosAjaxPrefsGet = "/bos/ajax/prefs-get";
    /// <summary>Wave B dry-run for BOS PHP prefs_save (writes=0).</summary>
    public const string BosAjaxPrefsSave = "/bos/ajax/prefs-save";
    /// <summary>Wave B dry-run for BOS PHP status (writes=0).</summary>
    public const string BosAjaxStatus = "/bos/ajax/status";
    /// <summary>Wave B dry-run for BOS PHP run_all (writes=0).</summary>
    public const string BosAjaxRunAll = "/bos/ajax/run-all";
    /// <summary>Wave B dry-run for BOS PHP set_limit (writes=0).</summary>
    public const string BosAjaxSetLimit = "/bos/ajax/set-limit";
    /// <summary>Wave B dry-run for BOS PHP order_status (writes=0).</summary>
    public const string BosAjaxOrderStatus = "/bos/ajax/order-status";
    /// <summary>Wave B dry-run for BOS PHP create (writes=0).</summary>
    public const string BosAjaxCreate = "/bos/ajax/create";
    /// <summary>Wave B dry-run for BOS PHP approve (writes=0).</summary>
    public const string BosAjaxApprove = "/bos/ajax/approve";
    /// <summary>Wave B dry-run for BOS PHP key_generate (writes=0).</summary>
    public const string BosAjaxKeyGenerate = "/bos/ajax/key-generate";
    /// <summary>Wave B dry-run for BOS PHP key_revoke (writes=0).</summary>
    public const string BosAjaxKeyRevoke = "/bos/ajax/key-revoke";
    /// <summary>Wave B dry-run for BOS PHP create_wave (writes=0).</summary>
    public const string BosAjaxCreateWave = "/bos/ajax/create-wave";
    /// <summary>Wave B dry-run for BOS PHP seed_hs (writes=0).</summary>
    public const string BosAjaxSeedHs = "/bos/ajax/seed-hs";
    /// <summary>Wave B dry-run for BOS PHP groups (writes=0).</summary>
    public const string BosAjaxGroups = "/bos/ajax/groups";
    /// <summary>Wave B dry-run for BOS PHP group (writes=0).</summary>
    public const string BosAjaxGroup = "/bos/ajax/group";
    /// <summary>Wave B dry-run for BOS PHP set_rate (writes=0).</summary>
    public const string BosAjaxSetRate = "/bos/ajax/set-rate";
    /// <summary>Wave B dry-run for BOS PHP seed_rates (writes=0).</summary>
    public const string BosAjaxSeedRates = "/bos/ajax/seed-rates";
    /// <summary>Wave B dry-run for BOS PHP provider_get (writes=0).</summary>
    public const string BosAjaxProviderGet = "/bos/ajax/provider-get";
    /// <summary>Wave B dry-run for BOS PHP provider_create (writes=0).</summary>
    public const string BosAjaxProviderCreate = "/bos/ajax/provider-create";
    /// <summary>Wave B dry-run for BOS PHP provider_toggle (writes=0).</summary>
    public const string BosAjaxProviderToggle = "/bos/ajax/provider-toggle";
    /// <summary>Wave B dry-run for BOS PHP provider_delete (writes=0).</summary>
    public const string BosAjaxProviderDelete = "/bos/ajax/provider-delete";
    /// <summary>Wave B dry-run for BOS PHP create_run (writes=0).</summary>
    public const string BosAjaxCreateRun = "/bos/ajax/create-run";
    /// <summary>Wave B dry-run for BOS PHP approve_run (writes=0).</summary>
    public const string BosAjaxApproveRun = "/bos/ajax/approve-run";
    /// <summary>Wave B dry-run for BOS PHP run_details (writes=0).</summary>
    public const string BosAjaxRunDetails = "/bos/ajax/run-details";
    /// <summary>Wave B dry-run for BOS PHP profile_create (writes=0).</summary>
    public const string BosAjaxProfileCreate = "/bos/ajax/profile-create";
    /// <summary>Wave B dry-run for BOS PHP add_invoice (writes=0).</summary>
    public const string BosAjaxAddInvoice = "/bos/ajax/add-invoice";
    /// <summary>Wave B dry-run for BOS PHP update_status (writes=0).</summary>
    public const string BosAjaxUpdateStatus = "/bos/ajax/update-status";
    /// <summary>Wave B dry-run for BOS PHP rma_create (writes=0).</summary>
    public const string BosAjaxRmaCreate = "/bos/ajax/rma-create";
    /// <summary>Wave B dry-run for BOS PHP rma_transition (writes=0).</summary>
    public const string BosAjaxRmaTransition = "/bos/ajax/rma-transition";
    /// <summary>Wave B dry-run for BOS PHP rma_list (writes=0).</summary>
    public const string BosAjaxRmaList = "/bos/ajax/rma-list";
    /// <summary>Wave B dry-run for BOS PHP rma_detail (writes=0).</summary>
    public const string BosAjaxRmaDetail = "/bos/ajax/rma-detail";
    /// <summary>Wave B dry-run for BOS PHP seed (writes=0).</summary>
    public const string BosAjaxSeed = "/bos/ajax/seed";
    /// <summary>Wave B dry-run for BOS PHP create_group (writes=0).</summary>
    public const string BosAjaxCreateGroup = "/bos/ajax/create-group";
    /// <summary>Wave B dry-run for BOS PHP members (writes=0).</summary>
    public const string BosAjaxMembers = "/bos/ajax/members";
    /// <summary>Wave B dry-run for BOS PHP add_member (writes=0).</summary>
    public const string BosAjaxAddMember = "/bos/ajax/add-member";
    /// <summary>Wave B dry-run for BOS PHP folders (writes=0).</summary>
    public const string BosAjaxFolders = "/bos/ajax/folders";
    /// <summary>Wave B dry-run for BOS PHP create_folder (writes=0).</summary>
    public const string BosAjaxCreateFolder = "/bos/ajax/create-folder";
    /// <summary>Wave B dry-run for BOS PHP plans (writes=0).</summary>
    public const string BosAjaxPlans = "/bos/ajax/plans";
    /// <summary>Wave B dry-run for BOS PHP create_plan (writes=0).</summary>
    public const string BosAjaxCreatePlan = "/bos/ajax/create-plan";
    /// <summary>Wave B dry-run for BOS PHP invoices (writes=0).</summary>
    public const string BosAjaxInvoices = "/bos/ajax/invoices";
    /// <summary>Wave B dry-run for BOS PHP controls (writes=0).</summary>
    public const string BosAjaxControls = "/bos/ajax/controls";
    /// <summary>Wave B dry-run for BOS PHP update_control (writes=0).</summary>
    public const string BosAjaxUpdateControl = "/bos/ajax/update-control";
    /// <summary>Wave B dry-run for BOS PHP add_evidence (writes=0).</summary>
    public const string BosAjaxAddEvidence = "/bos/ajax/add-evidence";
    /// <summary>Wave B dry-run for BOS PHP evidence (writes=0).</summary>
    public const string BosAjaxEvidence = "/bos/ajax/evidence";
    /// <summary>Wave B dry-run for BOS PHP create_policy (writes=0).</summary>
    public const string BosAjaxCreatePolicy = "/bos/ajax/create-policy";
    /// <summary>Wave B dry-run for PHP cp/content/shop/pos/ajax_pos.php?action=open_session (writes=0).</summary>
    public const string CpPosOpenSession = "/cp/pos/open-session";
    /// <summary>Wave B dry-run for PHP cp/content/shop/pos/ajax_pos.php?action=close_session (writes=0).</summary>
    public const string CpPosCloseSession = "/cp/pos/close-session";
    /// <summary>Wave B dry-run for PHP cp/content/shop/pos/ajax_pos.php?action=complete_sale (writes=0).</summary>
    public const string CpPosCompleteSale = "/cp/pos/complete-sale";
    /// <summary>Wave B dry-run for PHP cp/content/shop/pos/ajax_pos.php?action=save_settings (writes=0).</summary>
    public const string CpPosSaveSettings = "/cp/pos/save-settings";
    /// <summary>Wave B dry-run for PHP cp/content/control/portal/ajax_portal.php?action=save_settings (writes=0).</summary>
    public const string CpPortalSaveSettings = "/cp/portal/save-settings";
    /// <summary>Wave B dry-run for PHP cp/content/control/portal/ajax_portal.php?action=deploy_site (writes=0).</summary>
    public const string CpPortalDeploySite = "/cp/portal/deploy-site";
    /// <summary>Wave B dry-run for PHP cp/content/shop/crm/ajax_crm.php (writes=0).</summary>
    public const string CpCrmAction = "/cp/crm/action";
    /// <summary>Wave C catalog of CP module ajax write surfaces (procurement/document_control/customer_mgmt/auto_price/CRM).</summary>
    public const string CpModuleAjaxWriteCatalog = "/cp/module-ajax/writes/catalog";
    /// <summary>Wave C registry dry-run for any catalogued CP module ajax action (writes=0).</summary>
    public const string CpModuleAjaxWriteRegistryDryRun = "/cp/module-ajax/dry-run/{module}/{action}";
    /// <summary>Wave C dedicated dry-run for classified CP module ajax write actions (writes=0).</summary>
    public const string CpModuleAjaxWriteDedicatedDryRun = "/cp/module-ajax/{module}/{action}/dry-run";
    /// <summary>Wave B dry-run for PHP deploy/on-premises/activate-license.php (writes=0).</summary>
    public const string OnPremisesActivateLicenseCli = "/erp/on-premises/activate-license-cli-dry-run";
    /// <summary>Wave B dry-run for PHP deploy/on-premises/health-check.php (writes=0).</summary>
    public const string OnPremisesHealthCheckPack = "/erp/on-premises/health-check-pack-dry-run";
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
    /// <summary>
    /// www.ecomae.com marketing Blazor preview (animated epm-hub). Live marketing home/pages remain PHP.
    /// </summary>
    public const string MarketingApp = "/marketing/app";
    /// <summary>JSON lock: live ecomae.com marketing presentation stays PHP until dual-sample + approval.</summary>
    public const string MarketingPresentationLock = "/migration/marketing-presentation-lock";
    /// <summary>Batch 4: storefront part search Blazor results (PHP part_search remains authoritative for cart/tabs).</summary>
    public const string StorefrontSearchApp = "/storefront/search-app";
    /// <summary>Storefront warehouse-offer search JSON digest (Blazor UI remains <see cref="StorefrontSearchApp"/>).</summary>
    public const string StorefrontSearch = "/storefront/search";
    /// <summary>Batch 4: authenticated cart Blazor summary (qty/checkout writes remain PHP /shop/cart).</summary>
    public const string StorefrontCartApp = "/storefront/cart-app";
    /// <summary>Storefront authenticated cart JSON digest (Blazor UI remains <see cref="StorefrontCartApp"/>).</summary>
    public const string StorefrontCart = "/storefront/cart";
    /// <summary>Wave B checkout readiness Blazor scaffold (writes remain PHP).</summary>
    public const string StorefrontCheckoutApp = "/storefront/checkout-app";
    /// <summary>Storefront checkout readiness JSON digest over authenticated cart.</summary>
    public const string StorefrontCheckout = "/storefront/checkout";
    /// <summary>Wave B dry-run cart qty write (PHP ajax_change_count_need.php remains authoritative).</summary>
    public const string StorefrontCartChangeCountNeed = "/storefront/cart/change-count-need";
    /// <summary>Wave B dry-run cart checked_for_order toggle (PHP ajax_check_for_order.php remains authoritative).</summary>
    public const string StorefrontCartCheckForOrder = "/storefront/cart/check-for-order";
    /// <summary>Wave B dry-run cart delete (PHP ajax_delete_cart_record.php remains authoritative).</summary>
    public const string StorefrontCartDelete = "/storefront/cart/delete";
    /// <summary>Wave B dry-run add-to-cart type-2 (PHP ajax_add_to_basket.php remains authoritative).</summary>
    public const string StorefrontCartAdd = "/storefront/cart/add";
    /// <summary>Wave B dry-run garage notepad add (PHP ajax_add_to_notepad.php remains authoritative).</summary>
    public const string StorefrontGarageNotepadAdd = "/storefront/garage/notepad-add";
    /// <summary>Wave B dry-run quote submit (PHP ajax_quote_submit.php remains authoritative).</summary>
    public const string StorefrontQuoteSubmit = "/storefront/quotes/submit";
    /// <summary>Wave B dry-run quote accept (PHP ajax_quote_accept.php remains authoritative; cart INSERT stays PHP).</summary>
    public const string StorefrontQuoteAccept = "/storefront/quotes/accept";
    /// <summary>Wave B dry-run quote add-item (PHP ajax_add_to_quote.php remains authoritative; check_hash stays PHP).</summary>
    public const string StorefrontQuoteAddItem = "/storefront/quotes/add-item";
    /// <summary>Wave B dry-run quote add-manual (PHP ajax_add_to_quote_manual.php remains authoritative).</summary>
    public const string StorefrontQuoteAddManual = "/storefront/quotes/add-manual";
    /// <summary>Wave B dry-run garage set-active (PHP ajax_operations_cars.php action=active_car remains authoritative).</summary>
    public const string StorefrontGarageSetActive = "/storefront/garage/set-active";
    /// <summary>Wave B dry-run garage delete (PHP ajax_operations_cars.php action=delete_car remains authoritative).</summary>
    public const string StorefrontGarageDelete = "/storefront/garage/delete";
    /// <summary>Wave B dry-run garage check_car toggle (PHP ajax_operations_cars.php action=check_car remains authoritative).</summary>
    public const string StorefrontGarageCheckCar = "/storefront/garage/check-car";
    /// <summary>Wave B dry-run for PHP ajax_checkout_create.php (writes=0; PHP authoritative).</summary>
    public const string StorefrontCheckoutCreate = "/storefront/checkout/create";
    /// <summary>Wave B dry-run customer order message (PHP ajax_send_message.php customer path remains authoritative).</summary>
    public const string StorefrontOrderSendMessage = "/storefront/orders/send-message";
    /// <summary>Marketing platform overview Blazor scaffold (PHP /platform remains primary until dual-sample).</summary>
    public const string MarketingPlatformApp = "/marketing/platform";
    /// <summary>Marketing about Blazor scaffold (PHP /platform/about remains primary until dual-sample).</summary>
    public const string MarketingAboutApp = "/marketing/about";
    /// <summary>Marketing FAQ Blazor scaffold (PHP /platform/faq remains primary until dual-sample).</summary>
    public const string MarketingFaqApp = "/marketing/faq";
    /// <summary>Marketing pricing Blazor scaffold (PHP /platform/pricing remains primary until dual-sample).</summary>
    public const string MarketingPricingApp = "/marketing/pricing";
    /// <summary>Marketing contact Blazor scaffold (PHP /platform/contact remains primary until dual-sample).</summary>
    public const string MarketingContactApp = "/marketing/contact";
    /// <summary>Marketing industries Blazor scaffold (PHP /platform/industries remains primary until dual-sample).</summary>
    public const string MarketingIndustriesApp = "/marketing/industries";
    /// <summary>Marketing capabilities Blazor scaffold (PHP /platform/capabilities remains primary until dual-sample).</summary>
    public const string MarketingCapabilitiesApp = "/marketing/capabilities";
    /// <summary>Marketing demo Blazor scaffold (PHP /platform/demo remains primary until dual-sample).</summary>
    public const string MarketingDemoApp = "/marketing/demo";
    /// <summary>Marketing free-tools Blazor scaffold (PHP /platform/free-tools remains primary until dual-sample).</summary>
    public const string MarketingFreeToolsApp = "/marketing/free-tools";
    /// <summary>Marketing platform-guides Blazor scaffold (PHP /platform/platform-guides remains primary until dual-sample).</summary>
    public const string MarketingPlatformGuidesApp = "/marketing/platform-guides";
    /// <summary>Marketing customer-results Blazor scaffold (PHP /platform/customer-results remains primary until dual-sample).</summary>
    public const string MarketingCustomerResultsApp = "/marketing/customer-results";
    /// <summary>Marketing business-continuity Blazor scaffold (PHP /platform/business-continuity remains primary until dual-sample).</summary>
    public const string MarketingBusinessContinuityApp = "/marketing/business-continuity";
    /// <summary>Marketing API services Blazor scaffold (PHP /platform/api-services remains primary until dual-sample).</summary>
    public const string MarketingApiServicesApp = "/marketing/api-services";
    /// <summary>Marketing API documentation Blazor scaffold (PHP /platform/api-documentation remains primary until dual-sample).</summary>
    public const string MarketingApiDocumentationApp = "/marketing/api-documentation";
    /// <summary>Marketing Auto Price AI Blazor scaffold (PHP /platform/auto-price-ai remains primary until dual-sample).</summary>
    public const string MarketingAutoPriceAiApp = "/marketing/auto-price-ai";
    /// <summary>Marketing compare Blazor scaffold (PHP /compare remains primary until dual-sample).</summary>
    public const string MarketingCompareApp = "/marketing/compare";
    /// <summary>Marketing brochure Blazor scaffold (PHP /brochure remains primary until dual-sample).</summary>
    public const string MarketingBrochureApp = "/marketing/brochure";
    /// <summary>Marketing legal Blazor scaffold (PHP /legal remains primary until dual-sample).</summary>
    public const string MarketingLegalApp = "/marketing/legal";
    /// <summary>Marketing BOS knowledge Blazor scaffold (PHP /bos remains primary until dual-sample; not product /BOS/).</summary>
    public const string MarketingBosApp = "/marketing/bos";
    /// <summary>Marketing blockchain Blazor scaffold (PHP /blockchain remains primary until dual-sample).</summary>
    public const string MarketingBlockchainApp = "/marketing/blockchain";
    /// <summary>Marketing documentation Blazor scaffold (PHP /documentation remains primary until dual-sample).</summary>
    public const string MarketingDocumentationApp = "/marketing/documentation";
    /// <summary>Marketing solutions Blazor scaffold (PHP /solutions remains primary until dual-sample).</summary>
    public const string MarketingSolutionsApp = "/marketing/solutions";
    /// <summary>Marketing privacy Blazor scaffold (PHP /privacy remains primary until dual-sample).</summary>
    public const string MarketingPrivacyApp = "/marketing/privacy";
    /// <summary>Marketing terms Blazor scaffold (PHP /terms remains primary until dual-sample).</summary>
    public const string MarketingTermsApp = "/marketing/terms";
    /// <summary>Marketing cookie-policy Blazor scaffold (PHP /cookie-policy remains primary until dual-sample).</summary>
    public const string MarketingCookiePolicyApp = "/marketing/cookie-policy";
    /// <summary>Marketing security-policy Blazor scaffold (PHP /security-policy remains primary until dual-sample).</summary>
    public const string MarketingSecurityPolicyApp = "/marketing/security-policy";
    /// <summary>Marketing right to use Blazor scaffold (PHP /right-to-use remains primary until dual-sample).</summary>
    public const string MarketingRightToUseApp = "/marketing/right-to-use";
    /// <summary>Marketing trademark Blazor scaffold (PHP /trademark remains primary until dual-sample).</summary>
    public const string MarketingTrademarkApp = "/marketing/trademark";
    /// <summary>Marketing copyright Blazor scaffold (PHP /copyright remains primary until dual-sample).</summary>
    public const string MarketingCopyrightApp = "/marketing/copyright";
    /// <summary>Marketing data protection Blazor scaffold (PHP /data-protection remains primary until dual-sample).</summary>
    public const string MarketingDataProtectionApp = "/marketing/data-protection";
    /// <summary>Marketing acceptable use Blazor scaffold (PHP /acceptable-use remains primary until dual-sample).</summary>
    public const string MarketingAcceptableUseApp = "/marketing/acceptable-use";
    /// <summary>Marketing confidentiality Blazor scaffold (PHP /confidentiality remains primary until dual-sample).</summary>
    public const string MarketingConfidentialityApp = "/marketing/confidentiality";
    /// <summary>Marketing intellectual property Blazor scaffold (PHP /intellectual-property remains primary until dual-sample).</summary>
    public const string MarketingIntellectualPropertyApp = "/marketing/intellectual-property";
    /// <summary>Marketing blockchain disclaimer Blazor scaffold (PHP /blockchain-disclaimer remains primary until dual-sample).</summary>
    public const string MarketingBlockchainDisclaimerApp = "/marketing/blockchain-disclaimer";
    /// <summary>Marketing dmca Blazor scaffold (PHP /dmca remains primary until dual-sample).</summary>
    public const string MarketingDmcaApp = "/marketing/dmca";
    /// <summary>Marketing full CP brochure Blazor scaffold (PHP /brochure/cp remains primary until dual-sample).</summary>
    public const string MarketingBrochureCpApp = "/marketing/brochure-cp";
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

    /// <summary>
    /// Canonical shell URL forms (case / trailing-slash). Blazor apps own these routes;
    /// modules must not MapGet the same paths (AmbiguousMatchException).
    /// </summary>
    public static readonly string[] ControlPanelAliases = [ControlPanel, "/cp/", "/CP", "/CP/"];

    /// <inheritdoc cref="ControlPanelAliases"/>
    public static readonly string[] ErpAliases = [Erp, "/erp/", "/ERP", "/ERP/"];

    /// <inheritdoc cref="ControlPanelAliases"/>
    public static readonly string[] BosAliases = [Bos, "/bos/", "/BOS", "/BOS/"];

    public static readonly string[] ProtectedSurfaces =
    [
        ControlPanel,
        Erp,
        Bos
    ];
}
