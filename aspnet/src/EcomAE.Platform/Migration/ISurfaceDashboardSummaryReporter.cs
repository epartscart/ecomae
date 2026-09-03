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

    /// <summary>Guest checkout lookup (PHP <c>ajax_check_order_not_authorized</c>). Always <c>user_id = 0</c>. Writes stay PHP.</summary>
    Task<StorefrontGuestOrderResult> GetStorefrontGuestOrderAsync(long orderId, string? email, string? phone, CancellationToken cancellationToken = default);

    /// <summary>Customer-scoped order lines (PHP <c>shop/orders/items</c>). Writes stay PHP.</summary>
    Task<StorefrontOrderItemsResult> ListStorefrontOrderItemsAsync(int userId, long orderId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontOrderMessagesResult> ListStorefrontOrderMessagesAsync(int userId, long orderId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Published markup-group CSV path (PHP prices_download tab).</summary>
    Task<StorefrontPriceListResult> GetStorefrontPriceListAsync(int userId, CancellationToken cancellationToken = default);

    Task<CpUserListResult> ListCpUsersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CP user detail console (PHP users/usermanager/user). Writes remain PHP-authoritative.</summary>
    Task<CpUserDetailDigest?> GetCpUserDetailAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only CP shop_orders list + KPI (writes remain PHP OMS).</summary>
    Task<CpOrdersListResult> ListCpOrdersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only OMS detail console payload (PHP epc_orders_detail_pane). Writes remain PHP.</summary>
    Task<CpOrderDetailDigest?> GetCpOrderDetailAsync(long orderId, CancellationToken cancellationToken = default);

    Task<CpGroupListResult> ListCpGroupsAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpSupplierListResult> ListErpSuppliersAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpPurchaseListResult> ListErpPurchasesAsync(int limit, CancellationToken cancellationToken = default);

    Task<StorefrontGarageResult> ListStorefrontGarageAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontGarageNotepadResult> ListStorefrontGarageNotepadAsync(int userId, long garageId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontReturnsResult> ListStorefrontReturnsAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontReturnDetailResult> GetStorefrontReturnAsync(int userId, long returnId, CancellationToken cancellationToken = default);
    Task<StorefrontCustomerRequestsResult> ListStorefrontCustomerRequestsAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontCustomerRequestDetailResult> GetStorefrontCustomerRequestAsync(int userId, long requestId, CancellationToken cancellationToken = default);

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

    Task<ErpInventoryItemPickerResult> ListErpInventoryItemsForPickerAsync(int limit, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// PHP CHPU stock probe: <c>LIMIT 1</c> indexed <c>article_search</c> hit for robots/JSON-LD.
    /// Must stay fast — brand+article pages skip blocking warehouse SSR and fill via AJAX.
    /// </summary>
    Task<StorefrontPartStockProbeResult> ProbeStorefrontPartStockAsync(string article, string? brand, CancellationToken cancellationToken = default);

    /// <summary>Article-only search: warehouse manufacturers for normalized article (PHP brand picker).</summary>
    Task<StorefrontArticleBrandsResult> ListStorefrontArticleBrandsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Cross references for article (+ optional brand filter, PHP <c>ajax_epc_cross_search</c>).</summary>
    Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Cross references for brand+article (PHP part_search result crosses).</summary>
    Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, string? brand, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast storefront cross search for CHPU (local CP analogs + batched cross stock).
    /// Mirrors PHP <c>ajax_epc_cross_search</c> so the UI can paint in ~1s and, when the
    /// typed article has no warehouse offers, still show stock for interchangeable numbers.
    /// When <paramref name="includeCrossbase"/> is true, merges PHP-parity crossbase.ru refs
    /// (disk cache first, then short HTTP fetch) without blocking the local path.
    /// </summary>
    Task<StorefrontCrossSearchResult> BuildStorefrontCrossSearchAsync(
        string article,
        string? brand,
        int limit,
        CancellationToken cancellationToken = default,
        bool includeCrossbase = false);

    /// <summary>Batch 4: read-only authenticated customer cart (qty/checkout writes remain PHP).</summary>
    Task<StorefrontCartListResult> ListStorefrontCartAsync(int userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Customer quote requests (PHP <c>my_quotes.php</c>); submit/accept remain PHP.</summary>
    Task<StorefrontQuoteListResult> ListStorefrontQuotesAsync(int userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Customer quote detail + lines (PHP quote detail); writes remain PHP.</summary>
    Task<StorefrontQuoteDetailDigest?> GetStorefrontQuoteAsync(int userId, int quoteId, CancellationToken cancellationToken = default);

    /// <summary>Catalogue product digest for storefront product-app.</summary>
    Task<StorefrontProductResult> GetStorefrontProductAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Catalogue products by ids (wishlist/compare cookies).</summary>
    Task<StorefrontProductListResult> ListStorefrontProductsByIdsAsync(IReadOnlyList<int> productIds, CancellationToken cancellationToken = default);

    /// <summary>Published own-catalogue category tree (PHP <c>dp_menu</c> / Catalog of products).</summary>
    Task<StorefrontCatalogueTreeResult> ListStorefrontCatalogueTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>Own-catalogue products by category and/or name search.</summary>
    Task<StorefrontCatalogueProductsResult> ListStorefrontCatalogueProductsAsync(
        int categoryId,
        string? categoryUrl,
        string? searchString,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_price_attr_search</c> against <c>epc_price_attr_index</c>.</summary>
    Task<StorefrontWarehouseAttrResult> ListStorefrontWarehouseAttrAsync(
        string? field,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Genuine OE brand keys (PHP <c>epc_genuine_build_frontend_index</c>).</summary>
    Task<StorefrontGenuineBrandsResult> ListStorefrontGenuineBrandsAsync(CancellationToken cancellationToken = default);

    /// <summary>Office/storage bunches for progressive supplier poll (PHP <c>office_storage_bunches</c>).</summary>
    Task<StorefrontOfficeStorageBunchesResult> ListStorefrontOfficeStorageBunchesAsync(string article, string? brand, CancellationToken cancellationToken = default);

    /// <summary>Proxy one progressive supplier poll (PHP <c>ajax_getProductsOfBunch</c>).</summary>
    Task<StorefrontProductsOfBunchResult> PollStorefrontProductsOfBunchAsync(string article, string? brand, int officeId, int storageId, string? queryJson, int geoId = 0, CancellationToken cancellationToken = default);

    /// <summary>Customer bulk-upload history (PHP <c>epc_bulk_upload_history</c>); process writes remain PHP.</summary>
    Task<StorefrontBulkUploadHistoryResult> ListStorefrontBulkUploadHistoryAsync(int userId, int limit, CancellationToken cancellationToken = default);

    Task<CpTaxExternalReportingDigestResult> BuildCpTaxExternalReportingDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPoApprovalsDigestResult> BuildCpPoApprovalsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpFinanceCloseDigestResult> BuildCpFinanceCloseDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpJewelleryFixingDigestResult> BuildCpJewelleryFixingDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpWebTrackerDigestResult> BuildCpWebTrackerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpWebTrackerDashboardResult> BuildCpWebTrackerDashboardAsync(CpWebTrackerFilterQuery filters, CancellationToken cancellationToken = default);
    Task<CpWebTrackerSessionDetailResult> BuildCpWebTrackerSessionDetailAsync(long sessionId, string siteKey, bool isSuper, CancellationToken cancellationToken = default);
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

    /// <summary>Next-wave: commerce statistics KPIs + top article queries (ip omitted).</summary>
    Task<CpStatisticsDigestResult> BuildCpStatisticsDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: accessories listings digest (photos/writes remain PHP dry-run).</summary>
    Task<CpAccessoriesDigestResult> BuildCpAccessoriesDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: manufacturer synonyms digest (writes remain module-ajax dry-run).</summary>
    Task<CpSynonymsDigestResult> BuildCpSynonymsDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: SEO content KPIs (sitemap/robots; ping/warm remain PHP).</summary>
    Task<CpSeoDigestResult> BuildCpSeoDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: social hub accounts/drafts (credentials omitted; publish dry-run).</summary>
    Task<CpSocialHubDigestResult> BuildCpSocialHubDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: tenant feature flags matrix (save dry-run).</summary>
    Task<CpTenantFeaturesDigestResult> BuildCpTenantFeaturesDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: customer board user peek (writes remain PHP).</summary>
    Task<CpCustomerBoardDigestResult> BuildCpCustomerBoardDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: fulfillment queue digest (OMS stage writes remain dry-run).</summary>
    Task<CpFulfillmentQueueDigestResult> BuildCpFulfillmentQueueDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: SSO/SAML providers (certs/metadata omitted).</summary>
    Task<CpSsoSamlDigestResult> BuildCpSsoSamlDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: MySQL epc_events bus peek (no Kafka/Rabbit).</summary>
    Task<CpEventBusDigestResult> BuildCpEventBusDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only on-premises license registry (notes/fingerprint/ip omitted; license keys masked).</summary>
    Task<OnPremisesLicenseListResult> ListOnPremisesLicensesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpDeliveryNoteListResult> ListErpDeliveryNotesAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpRfqListResult> ListErpRfqsAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpThreeWayMatchListResult> ListErpThreeWayMatchAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpContactListResult> ListErpContactsAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpPaymentBatchListResult> ListErpPaymentBatchesAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpFiscalPeriodListResult> ListErpFiscalPeriodsAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpAgendaEventListResult> ListErpAgendaEventsAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpDocumentListResult> ListErpDocumentsAsync(int limit, CancellationToken cancellationToken = default);
    Task<ErpExpenseReportListResult> ListErpExpenseReportsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only shop_offices + storage/geo maps (PHP offices.php).</summary>
    Task<CpOfficesDigestResult> BuildCpOfficesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only workshop jobs (customer phone/email omitted).</summary>
    Task<CpWorkshopDigestResult> BuildCpWorkshopDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only KKT devices (PHP devices.php; customer contact omitted).</summary>
    Task<CpKktDigestResult> BuildCpKktDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CP bulk-upload history (PHP bulk_upload_hub; file bodies omitted).</summary>
    Task<CpBulkUploadDigestResult> BuildCpBulkUploadDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only tenant SMTP settings (password/username omitted).</summary>
    Task<CpTenantEmailDigestResult> BuildCpTenantEmailDigestAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only department workflow board from PHP <c>epc_erp_workflow_tasks</c> (writes remain PHP).</summary>
    Task<ErpWorkflowTasksDigestResult> BuildErpWorkflowTasksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Operational VAT 201 boxes (shop orders + purchases). FTA filing stays PHP.</summary>
    Task<ErpVatReturnDigestResult> BuildErpVatReturnDigestAsync(long? fromUnix = null, long? toUnix = null, CancellationToken cancellationToken = default);

    /// <summary>Read-only withholding codes + transactions (PHP <c>epc_wht_*</c>; writes remain PHP).</summary>
    Task<ErpWithholdingDigestResult> BuildErpWithholdingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only petty cash floats (PHP <c>epc_erp_petty_cash</c>).</summary>
    Task<ErpPettyCashListResult> ListErpPettyCashAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only cash-flow forecasts + projection (PHP <c>epc_cft_forecast</c>).</summary>
    Task<ErpCashForecastDigestResult> BuildErpCashForecastDigestAsync(int limit, long? forecastId = null, CancellationToken cancellationToken = default);

    /// <summary>Read-only bank instruments LC/BG/SBLC (PHP <c>epc_cft_instrument</c>).</summary>
    Task<ErpBankInstrumentsDigestResult> BuildErpBankInstrumentsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only subscription billing list + MRR/ARR (PHP <c>epc_erp_subscriptions</c>).</summary>
    Task<ErpSubscriptionsDigestResult> BuildErpSubscriptionsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only supplier performance scorecards (PHP <c>epc_sp_scorecards</c>).</summary>
    Task<ErpSupplierPortalDigestResult> BuildErpSupplierPortalDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only virtual/exhibition warehouse locations + transfer history.</summary>
    Task<ErpVirtualWarehouseDigestResult> BuildErpVirtualWarehouseDigestAsync(int limit, CancellationToken cancellationToken = default);
}


