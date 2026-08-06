namespace EcomAE.Platform.Migration;

public interface ISurfaceDashboardSummaryReporter
{
    Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default);

    Task<ErpDashboardDigestResult> BuildErpAsync(CancellationToken cancellationToken = default);

    Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default);

    Task<StorefrontAccountDigestResult> BuildStorefrontAccountAsync(int userId, int recentLimit = 10, CancellationToken cancellationToken = default);

    Task<PortalTenantListResult> ListPortalTenantsAsync(int limit, CancellationToken cancellationToken = default);

    Task<BosFleetHealthResult> BuildBosFleetHealthAsync(int sampleLimit, CancellationToken cancellationToken = default);

    Task<ErpAccountsSummaryResult> BuildErpAccountsAsync(CancellationToken cancellationToken = default);

    Task<StorefrontOrdersResult> ListStorefrontOrdersAsync(int userId, int limit, CancellationToken cancellationToken = default);

    Task<CpUserListResult> ListCpUsersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only CP shop_orders list + KPI (writes remain PHP OMS).</summary>
    Task<CpOrdersListResult> ListCpOrdersAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpGroupListResult> ListCpGroupsAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpSupplierListResult> ListErpSuppliersAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpPurchaseListResult> ListErpPurchasesAsync(int limit, CancellationToken cancellationToken = default);

    Task<StorefrontGarageResult> ListStorefrontGarageAsync(int userId, int limit, CancellationToken cancellationToken = default);

    Task<ErpCashAccountListResult> ListErpCashAccountsAsync(int limit, CancellationToken cancellationToken = default);

    Task<StorefrontProfileResult> BuildStorefrontProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryListResult> ListErpCashEntriesAsync(int? accountId, int limit, CancellationToken cancellationToken = default);

    Task<ErpInvoiceListResult> ListErpInvoicesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpGlJournalListResult> ListErpGlJournalsAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpModuleListResult> ListCpModulesAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpConfigItemMetaListResult> ListCpConfigItemsMetaAsync(int limit, CancellationToken cancellationToken = default);

    Task<BosFleetReadinessResult> BuildBosFleetReadinessAsync(CancellationToken cancellationToken = default);

    Task<ErpCoaAccountListResult> ListErpCoaAccountsAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpWarehouseListResult> ListErpWarehousesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpSalesOrderListResult> ListErpSalesOrdersAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpMenuListResult> ListCpMenusAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpPageListResult> ListCpPagesAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpAdminSessionListResult> ListCpAdminSessionsAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpStorageListResult> ListCpStoragesAsync(int limit, CancellationToken cancellationToken = default);

    Task<BosAuditLogListResult> ListBosAuditLogAsync(string? area, int limit, CancellationToken cancellationToken = default);

    Task<ErpPurchaseOrderListResult> ListErpPurchaseOrdersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only inventory stock KPIs + on-hand rows (PHP <c>epc_erp_inventory_stock_report</c>).</summary>
    Task<ErpInventoryStockDigestResult> BuildErpInventoryStockDigestAsync(int limit, int? warehouseId = null, CancellationToken cancellationToken = default);

    Task<CpCurrencyListResult> ListCpCurrenciesAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpApiClientMetaListResult> ListCpApiClientsMetaAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only Power BI config + reports (configure/embed writes remain PHP).</summary>
    Task<CpPowerBiDigestResult> BuildCpPowerBiDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only mobile apps integrations_json.mobile (save_mobile writes remain PHP).</summary>
    Task<CpMobileAppsDigestResult> BuildCpMobileAppsDigestAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only Metabase config + dashboards (secret_key never returned).</summary>
    Task<CpMetabaseDigestResult> BuildCpMetabaseDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only NL report definitions metadata (query/recipients omitted).</summary>
    Task<CpNlReportingDigestResult> ListCpNlReportDefinitionsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketing broadcast campaigns (bodies omitted; send remains PHP).</summary>
    Task<CpMarketingBroadcastDigestResult> BuildCpMarketingBroadcastDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only demo tenant registry (passwords never returned).</summary>
    Task<CpDemoTenantsDigestResult> ListCpDemoTenantsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only AI Parts Agent config + sessions (system_prompt / client_ip omitted).</summary>
    Task<CpPartsAgentDigestResult> BuildCpPartsAgentDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only POS settings + recent sales (terminal writes remain PHP).</summary>
    Task<CpPosOverviewDigestResult> BuildCpPosOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only tax toolkit catalog + tenant profile (rules_json / reg_number omitted).</summary>
    Task<CpTaxToolkitsDigestResult> BuildCpTaxToolkitsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only SMS operators + WhatsApp log (parameters_values / tokens / raw phone omitted).</summary>
    Task<CpSmsWhatsappDigestResult> BuildCpSmsWhatsappDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM KPIs + leads (email/phone/notes omitted).</summary>
    Task<CpCrmBoardDigestResult> BuildCpCrmBoardDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only document templates (HTML/bank secrets omitted).</summary>
    Task<CpDocumentControlDigestResult> BuildCpDocumentControlDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only delivery/obtaining modes (parameters_values omitted).</summary>
    Task<CpDeliveryMethodsDigestResult> BuildCpDeliveryMethodsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only article cross pairs.</summary>
    Task<CpCrossesDigestResult> BuildCpCrossesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only HR KPIs + employees (salary/allowances/currency/payslip omitted).</summary>
    Task<CpHrOverviewDigestResult> BuildCpHrOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only production KPIs + work orders (cost columns omitted).</summary>
    Task<CpProductionOverviewDigestResult> BuildCpProductionOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only projects KPIs + projects (timesheet rates omitted).</summary>
    Task<CpProjectsOverviewDigestResult> BuildCpProjectsOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only industry packs (JSON blobs omitted).</summary>
    Task<CpIndustryPacksDigestResult> BuildCpIndustryPacksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP legal entities + per-company industry_pack (PHP multi-company picker).</summary>
    Task<ErpCompaniesDigestResult> BuildErpCompaniesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery retail KPIs + vouchers (PII/cost omitted).</summary>
    Task<CpJewelleryRetailDigestResult> BuildCpJewelleryRetailDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only price lists (stats_json/error_text/stored_relpath omitted).</summary>
    Task<CpPriceListsDigestResult> BuildCpPriceListsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only auto-price rules (config_json/notes/meta omitted).</summary>
    Task<CpAutoPriceDigestResult> BuildCpAutoPriceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only UAE tax legislation (erp_summary/pdf/passport omitted).</summary>
    Task<CpUaeTaxComplianceDigestResult> BuildCpUaeTaxComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only budgets (note omitted).</summary>
    Task<CpBudgetsDigestResult> BuildCpBudgetsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only carriers (contact PII omitted).</summary>
    Task<CpCarriersDigestResult> BuildCpCarriersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only payment gateways (parameters/credentials omitted).</summary>
    Task<CpPaymentGatewaysDigestResult> BuildCpPaymentGatewaysDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only workflows (trigger_config/JSON omitted).</summary>
    Task<CpWorkflowsDigestResult> BuildCpWorkflowsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only purchase requisitions (justification/decision_note omitted).</summary>
    Task<CpPurchaseRequestsDigestResult> BuildCpPurchaseRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only promotions (epc_promo_promotions).</summary>
    Task<CpPromotionsDigestResult> BuildCpPromotionsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM opportunities (notes omitted).</summary>
    Task<CpCrmOpportunitiesDigestResult> BuildCpCrmOpportunitiesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only integrations/webhooks (secrets/events omitted).</summary>
    Task<CpIntegrationsDigestResult> BuildCpIntegrationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only page builder layouts (layout_json/brand_json omitted).</summary>
    Task<CpPageBuilderDigestResult> BuildCpPageBuilderDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only product catalogue (shop_catalogue_products).</summary>
    Task<CpProductCatalogueDigestResult> BuildCpProductCatalogueDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only platform governance rules (description/config_json omitted).</summary>
    Task<CpPlatformGovernanceDigestResult> BuildCpPlatformGovernanceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only e-invoice documents (payload JSON/XML omitted).</summary>
    Task<CpEinvoiceDocumentsDigestResult> BuildCpEinvoiceDocumentsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery repairs (customer PII/narration omitted).</summary>
    Task<CpJewelleryRepairsDigestResult> BuildCpJewelleryRepairsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM tickets (message bodies omitted).</summary>
    Task<CpCrmTicketsDigestResult> BuildCpCrmTicketsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketing growth reviews (notes omitted).</summary>
    Task<CpMarketingGrowthDigestResult> BuildCpMarketingGrowthDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only SOC 2 controls (description/implementation omitted).</summary>
    Task<CpSoc2ComplianceDigestResult> BuildCpSoc2ComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only cost model item assignments.</summary>
    Task<CpCostModelsDigestResult> BuildCpCostModelsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only financial periods (allocation/accrual JSON omitted).</summary>
    Task<CpFinAdvancedDigestResult> BuildCpFinAdvancedDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only blockchain proofs (payload/merkle JSON omitted).</summary>
    Task<CpBlockchainProofsDigestResult> BuildCpBlockchainProofsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only landed cost sheets (notes omitted).</summary>
    Task<CpLandedCostDigestResult> BuildCpLandedCostDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only WMS work pool.</summary>
    Task<CpWarehouseWmsDigestResult> BuildCpWarehouseWmsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only AI service queries (input/output text omitted).</summary>
    Task<CpAiServiceDigestResult> BuildCpAiServiceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only returns/RMA requests (description/notes omitted).</summary>
    Task<CpReturnsRmaDigestResult> BuildCpReturnsRmaDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only commerce isolation audit runs (report_json omitted).</summary>
    Task<CpIsolationAuditDigestResult> BuildCpIsolationAuditDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only AML KYC rows (notes/document paths omitted).</summary>
    Task<CpAmlComplianceDigestResult> BuildCpAmlComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery karat/rate/barcode masters.</summary>
    Task<CpJewelleryMastersDigestResult> BuildCpJewelleryMastersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only consolidation group entities.</summary>
    Task<CpConsolidationsDigestResult> BuildCpConsolidationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM activities (notes omitted).</summary>
    Task<CpCrmActivitiesDigestResult> BuildCpCrmActivitiesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only auth MFA enrollments (secrets/hashes omitted).</summary>
    Task<CpAuthMfaDigestResult> BuildCpAuthMfaDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only electronic reporting formats (preview omitted).</summary>
    Task<CpElectronicReportingDigestResult> BuildCpElectronicReportingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only collections/dunning queue (notes omitted).</summary>
    Task<CpCollectionsDunningDigestResult> BuildCpCollectionsDunningDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketplace channels (config_json omitted).</summary>
    Task<CpMarketplaceChannelsDigestResult> BuildCpMarketplaceChannelsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only demand intelligence countries.</summary>
    Task<CpDemandIntelligenceDigestResult> BuildCpDemandIntelligenceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only credit limits (notes omitted).</summary>
    Task<CpCreditLimitsDigestResult> BuildCpCreditLimitsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only insurance policies (notes/emails omitted).</summary>
    Task<CpInsuranceComplianceDigestResult> BuildCpInsuranceComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP audit trail (detail/old/new JSON omitted).</summary>
    Task<CpAuditTrailDigestResult> BuildCpAuditTrailDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only document expiry register (notes/emails/paths omitted).</summary>
    Task<CpDocExpiryDigestResult> BuildCpDocExpiryDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only tenant config keys (config_value omitted).</summary>
    Task<CpTenantConfigDigestResult> BuildCpTenantConfigDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery stock verification vouchers (remarks omitted).</summary>
    Task<CpJewelleryStockVerificationDigestResult> BuildCpJewelleryStockVerificationDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only bank statement lines for reconciliation.</summary>
    Task<ErpBankReconciliationDigestResult> BuildErpBankReconciliationDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only warehouse stock transfers (notes omitted).</summary>
    Task<ErpStockTransfersDigestResult> BuildErpStockTransfersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM/sales quotations (notes omitted).</summary>
    Task<ErpSalesQuotationsDigestResult> BuildErpSalesQuotationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP workspace favorites/shortcuts.</summary>
    Task<ErpWorkspaceFavoritesDigestResult> BuildErpWorkspaceFavoritesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only fixed assets register (note omitted).</summary>
    Task<ErpFixedAssetsDigestResult> BuildErpFixedAssetsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only process-flow cases from PHP <c>epc_pf_cases</c> (writes remain PHP).</summary>
    Task<ErpProcessFlowTasksDigestResult> BuildErpProcessFlowTasksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP report-center registry (+ optional table-backed/computed run). CSV/export remain PHP.</summary>
    Task<ErpReportCenterDigestResult> BuildErpReportCenterDigestAsync(string? key, int limit, CancellationToken cancellationToken = default, int? companyId = null);

    /// <summary>Read-only AR/AP/inventory aging (PHP <c>epc_erp_aging.php</c>).</summary>
    Task<ErpAgingDigestResult> BuildErpAgingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only inventory movement ledger (PHP <c>epc_erp_inventory_ledger</c>).</summary>
    Task<ErpInventoryMovementsDigestResult> BuildErpInventoryMovementsDigestAsync(int limit, int? itemId = null, int? warehouseId = null, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only warehouse part search (writes/cart remain PHP part_search).</summary>
    Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only warehouse part search filtered by brand (query <c>brand</c> / PHP <c>brend</c>).</summary>
    Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, string? brand, int limit, CancellationToken cancellationToken = default);

    /// <summary>Article-only search: warehouse manufacturers for normalized article (PHP brand picker).</summary>
    Task<StorefrontArticleBrandsResult> ListStorefrontArticleBrandsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Cross references for article (+ optional brand filter, PHP <c>ajax_epc_cross_search</c>).</summary>
    Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Cross references for brand+article (PHP part_search result crosses).</summary>
    Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, string? brand, int limit, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only authenticated customer cart (qty/checkout writes remain PHP).</summary>
    Task<StorefrontCartListResult> ListStorefrontCartAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<CpTaxExternalReportingDigestResult> BuildCpTaxExternalReportingDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPoApprovalsDigestResult> BuildCpPoApprovalsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpFinanceCloseDigestResult> BuildCpFinanceCloseDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpJewelleryFixingDigestResult> BuildCpJewelleryFixingDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpWebTrackerDigestResult> BuildCpWebTrackerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpAbandonedCartsDigestResult> BuildCpAbandonedCartsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpQuoteRequestsDigestResult> BuildCpQuoteRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPlatformCommunicationDigestResult> BuildCpPlatformCommunicationDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpInfoBlocksDigestResult> BuildCpInfoBlocksDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpFreeToolsDigestResult> BuildCpFreeToolsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpConfigSandboxDigestResult> BuildCpConfigSandboxDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpMarketplaceAppsDigestResult> BuildCpMarketplaceAppsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpNotificationsDigestResult> BuildCpNotificationsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPortalSettingsDigestResult> BuildCpPortalSettingsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDataMigrationsDigestResult> BuildCpDataMigrationsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpGeoRegionsDigestResult> BuildCpGeoRegionsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpProductFiltersDigestResult> BuildCpProductFiltersDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpSearchTabsDigestResult> BuildCpSearchTabsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpSystemRequestsDigestResult> BuildCpSystemRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpAdditionalTextsDigestResult> BuildCpAdditionalTextsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpSliderBannersDigestResult> BuildCpSliderBannersDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpStructureDumpsDigestResult> BuildCpStructureDumpsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpCommunicationsTestDigestResult> BuildCpCommunicationsTestDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpLanguagesDigestResult> BuildCpLanguagesDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPluginsManagerDigestResult> BuildCpPluginsManagerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpTemplatesManagerDigestResult> BuildCpTemplatesManagerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDesignTokensDigestResult> BuildCpDesignTokensDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpSitemapDigestResult> BuildCpSitemapDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpFailoverStatusDigestResult> BuildCpFailoverStatusDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpOpsGuidesDigestResult> BuildCpOpsGuidesDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpFileManagerDigestResult> BuildCpFileManagerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpServerIpDigestResult> BuildCpServerIpDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDebugConsoleDigestResult> BuildCpDebugConsoleDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only on-premises license registry (notes/fingerprint/ip omitted; license keys masked).</summary>
    Task<OnPremisesLicenseListResult> ListOnPremisesLicensesAsync(int limit, CancellationToken cancellationToken = default);
}
