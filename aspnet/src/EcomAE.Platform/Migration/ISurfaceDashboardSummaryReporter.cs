namespace EcomAE.Platform.Migration;

public interface ISurfaceDashboardSummaryReporter
{
    Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default);

    Task<ErpDashboardDigestResult> BuildErpAsync(CancellationToken cancellationToken = default);

    /// <summary>PHP <c>erp_dashboard_netsuite.php</c> company-home digest (prior period, trend, alerts, process analytics).</summary>
    Task<ErpWorkspaceHomeDigest> BuildErpWorkspaceHomeAsync(CancellationToken cancellationToken = default);

    Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default);

    Task<StorefrontAccountDigestResult> BuildStorefrontAccountAsync(int userId, int recentLimit = 10, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>my_balance.php</c> ledger rows from <c>shop_users_accounting</c>.</summary>
    Task<StorefrontAccountOperationsResult> ListStorefrontAccountOperationsAsync(int userId, int limit, CancellationToken cancellationToken = default);

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

    /// <summary>Read-only CP user detail console (PHP users/usermanager/user). Create / password / comment / lock write on ASP.NET.</summary>
    Task<CpUserDetailDigest?> GetCpUserDetailAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only CP shop_orders list + KPI (writes remain PHP OMS).</summary>
    Task<CpOrdersListResult> ListCpOrdersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only OMS detail console payload (PHP epc_orders_detail_pane). Writes remain PHP.</summary>
    Task<CpOrderDetailDigest?> GetCpOrderDetailAsync(long orderId, CancellationToken cancellationToken = default);

    Task<CpGroupListResult> ListCpGroupsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened user group (Open key <c>ugroup_id</c>) plus same-parent siblings. description is a short excerpt.</summary>
    Task<CpGroupDetailResult> BuildCpGroupsDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSupplierListResult> ListErpSuppliersAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpPurchaseListResult> ListErpPurchasesAsync(int limit, CancellationToken cancellationToken = default);

    Task<StorefrontGarageResult> ListStorefrontGarageAsync(int userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>shop_docpart_garage_orders</c> links for an order owned by the customer.</summary>
    Task<StorefrontGarageOrderLinksResult> ListStorefrontGarageOrderLinksAsync(int userId, long orderId, CancellationToken cancellationToken = default);
    Task<StorefrontGarageNotepadResult> ListStorefrontGarageNotepadAsync(int userId, long garageId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontReturnsResult> ListStorefrontReturnsAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontReturnDetailResult> GetStorefrontReturnAsync(int userId, long returnId, CancellationToken cancellationToken = default);
    Task<StorefrontCustomerRequestsResult> ListStorefrontCustomerRequestsAsync(int userId, int limit, CancellationToken cancellationToken = default);
    Task<StorefrontCustomerRequestDetailResult> GetStorefrontCustomerRequestAsync(int userId, long requestId, CancellationToken cancellationToken = default);

    Task<ErpCashAccountListResult> ListErpCashAccountsAsync(int limit, CancellationToken cancellationToken = default);

    Task<StorefrontProfileResult> BuildStorefrontProfileAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>editform.php</c> <c>reg_variants</c> + <c>reg_fields</c> (main_flag=0).</summary>
    Task<StorefrontRegCatalogResult> ListStorefrontRegCatalogAsync(CancellationToken cancellationToken = default);

    Task<ErpCashEntryListResult> ListErpCashEntriesAsync(int? accountId, int limit, CancellationToken cancellationToken = default);

    Task<ErpInvoiceListResult> ListErpInvoicesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpGlJournalListResult> ListErpGlJournalsAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpModuleListResult> ListCpModulesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened module (PHP <c>module_id</c>) plus same-position siblings. Body is a short excerpt.</summary>
    Task<CpModuleDetailResult> BuildCpModulesDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<CpConfigItemMetaListResult> ListCpConfigItemsMetaAsync(int limit, CancellationToken cancellationToken = default);

    Task<BosFleetReadinessResult> BuildBosFleetReadinessAsync(CancellationToken cancellationToken = default);

    Task<ErpCoaAccountListResult> ListErpCoaAccountsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened COA account (Open key <c>account_id</c>, remapped by <c>tab=coa</c>) plus same-type siblings. description is a short excerpt.</summary>
    Task<ErpCoaAccountDetailResult> BuildErpCoaAccountDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpWarehouseListResult> ListErpWarehousesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened warehouse (Open key <c>warehouse_id</c>) plus same-active siblings. name is a short excerpt.</summary>
    Task<ErpWarehouseDetailResult> BuildErpWarehouseDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSalesOrderListResult> ListErpSalesOrdersAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpInventoryItemPickerResult> ListErpInventoryItemsForPickerAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpMenuListResult> ListCpMenusAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened menu (PHP <c>menu_id</c>) plus same-frontend siblings. Structure is a short excerpt.</summary>
    Task<CpMenuDetailResult> BuildCpMenusDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<CpPageListResult> ListCpPagesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened content page (PHP <c>content_id</c>) plus same-parent siblings. Body is a short excerpt.</summary>
    Task<CpPageDetailResult> BuildCpPagesDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<CpAdminSessionListResult> ListCpAdminSessionsAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpStorageListResult> ListCpStoragesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened warehouse (PHP <c>id</c> / <c>storage_id</c>). users excerpt only; connection_options omitted.</summary>
    Task<CpStorageDetailResult> BuildCpStoragesDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<BosAuditLogListResult> ListBosAuditLogAsync(string? area, int limit, CancellationToken cancellationToken = default);

    Task<ErpPurchaseOrderListResult> ListErpPurchaseOrdersAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only inventory stock KPIs + on-hand rows (PHP <c>epc_erp_inventory_stock_report</c>).</summary>
    Task<ErpInventoryStockDigestResult> BuildErpInventoryStockDigestAsync(int limit, int? warehouseId = null, CancellationToken cancellationToken = default);

    Task<CpCurrencyListResult> ListCpCurrenciesAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpApiClientMetaListResult> ListCpApiClientsMetaAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened API client (PHP <c>api_client_id=</c> detail). Never loads <c>client_key_hash</c>.</summary>
    Task<CpApiClientDetailResult> BuildCpApiClientDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only Power BI config + reports (configure/embed writes remain PHP).</summary>
    Task<CpPowerBiDigestResult> BuildCpPowerBiDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened Power BI report (PHP <c>pbi_id</c>) plus site notes excerpt and category siblings.</summary>
    Task<CpPowerBiReportDetailResult> BuildCpPowerBiReportDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only mobile apps integrations_json.mobile (save_mobile writes remain PHP).</summary>
    Task<CpMobileAppsDigestResult> BuildCpMobileAppsDigestAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only Metabase config + dashboards (secret_key never returned).</summary>
    Task<CpMetabaseDigestResult> BuildCpMetabaseDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened Metabase dashboard (PHP <c>mb_id</c>) plus site URL and category siblings. secret_key omitted.</summary>
    Task<CpMetabaseDashboardDetailResult> BuildCpMetabaseDashboardDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only NL report definitions metadata (query/recipients omitted).</summary>
    Task<CpNlReportingDigestResult> ListCpNlReportDefinitionsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened NL report definition (PHP <c>report_id</c>) plus runs. Query excerpt only; recipients omitted.</summary>
    Task<CpNlReportDefinitionDetailResult> BuildCpNlReportDefinitionDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketing broadcast campaigns (bodies omitted; send remains PHP).</summary>
    Task<CpMarketingBroadcastDigestResult> BuildCpMarketingBroadcastDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened broadcast campaign plus send log (PHP <c>campaign_id</c>).</summary>
    Task<CpMarketingBroadcastDetailResult> BuildCpMarketingBroadcastDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only demo tenant registry (passwords never returned).</summary>
    Task<CpDemoTenantsDigestResult> ListCpDemoTenantsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only AI Parts Agent config + sessions (system_prompt / client_ip omitted).</summary>
    Task<CpPartsAgentDigestResult> BuildCpPartsAgentDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only POS settings + recent sales (terminal writes remain PHP).</summary>
    Task<CpPosOverviewDigestResult> BuildCpPosOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only tax toolkit catalog + tenant profile (rules_json / reg_number omitted).</summary>
    Task<CpTaxToolkitsDigestResult> BuildCpTaxToolkitsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened tax toolkit + installs/updates (PHP <c>toolkit_id=</c> detail).</summary>
    Task<CpTaxToolkitDetailResult> BuildCpTaxToolkitDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only SMS operators + WhatsApp log (parameters_values / tokens / raw phone omitted).</summary>
    Task<CpSmsWhatsappDigestResult> BuildCpSmsWhatsappDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM KPIs + leads (email/phone/notes omitted).</summary>
    Task<CpCrmBoardDigestResult> BuildCpCrmBoardDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened CRM lead (Open key <c>lead_id</c>) plus same-status siblings. notes is a short excerpt; email/phone omitted.</summary>
    Task<CpCrmLeadDetailResult> BuildCpCrmLeadDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only document templates (HTML/bank secrets omitted).</summary>
    Task<CpDocumentControlDigestResult> BuildCpDocumentControlDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only delivery/obtaining modes (parameters_values omitted).</summary>
    Task<CpDeliveryMethodsDigestResult> BuildCpDeliveryMethodsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened delivery mode (PHP <c>obtaining_mode_id</c>) plus same-available siblings. parameters_values is a short excerpt.</summary>
    Task<CpDeliveryModeDetailResult> BuildCpDeliveryModeDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only article cross pairs.</summary>
    Task<CpCrossesDigestResult> BuildCpCrossesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened cross pair (PHP <c>cross_id=</c> detail) plus same-article siblings.</summary>
    Task<CpCrossPairDetailResult> BuildCpCrossPairDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only HR KPIs + employees (salary/allowances/currency/payslip omitted).</summary>
    Task<CpHrOverviewDigestResult> BuildCpHrOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only production KPIs + work orders (cost columns omitted).</summary>
    Task<CpProductionOverviewDigestResult> BuildCpProductionOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened work order (PHP <c>wo_id=</c> detail) plus BOM lines.</summary>
    Task<CpProductionWorkOrderDetailResult> BuildCpProductionWorkOrderDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only projects KPIs + projects (timesheet rates omitted).</summary>
    Task<CpProjectsOverviewDigestResult> BuildCpProjectsOverviewDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened project (PHP <c>project_id=</c> detail) plus tasks and timesheets.</summary>
    Task<CpProjectDetailResult> BuildCpProjectDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only industry packs (JSON blobs omitted).</summary>
    Task<CpIndustryPacksDigestResult> BuildCpIndustryPacksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened industry pack (PHP <c>pack_id</c>) plus tenant assignments. Modules excerpt only.</summary>
    Task<CpIndustryPackDetailResult> BuildCpIndustryPackDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP legal entities + per-company industry_pack (PHP multi-company picker).</summary>
    Task<ErpCompaniesDigestResult> BuildErpCompaniesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery retail KPIs + vouchers (PII/cost omitted).</summary>
    Task<CpJewelleryRetailDigestResult> BuildCpJewelleryRetailDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened jewellery retail voucher (Open key <c>voc_id</c>) plus same-status siblings. narration is a short excerpt. PII omitted.</summary>
    Task<CpJewelleryVoucherDetailResult> BuildCpJewelleryVoucherDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only price lists (stats_json/error_text/stored_relpath omitted).</summary>
    Task<CpPriceListsDigestResult> BuildCpPriceListsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened price list (Open key <c>plist_id</c>) plus same-active siblings. stats_json and error_text are short excerpts. stored_relpath omitted.</summary>
    Task<CpPriceListDetailResult> BuildCpPriceListDetailAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>PHP <c>prices_manager.php</c> Docpart lists (<c>shop_docpart_prices</c> + linked warehouses).</summary>
    Task<CpDocpartPriceListsDigestResult> BuildCpDocpartPriceListsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDocpartPriceListDetail?> BuildCpDocpartPriceListDetailAsync(long priceId, CancellationToken cancellationToken = default);

    /// <summary>Read-only auto-price rules (config_json/notes/meta omitted).</summary>
    Task<CpAutoPriceDigestResult> BuildCpAutoPriceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened auto-price rule (Open key <c>aprice_id</c>) plus same-site siblings. notes is a short excerpt; config_json omitted.</summary>
    Task<CpAutoPriceRuleDetailResult> BuildCpAutoPriceDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only UAE tax legislation (erp_summary/pdf/passport omitted).</summary>
    Task<CpUaeTaxComplianceDigestResult> BuildCpUaeTaxComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened UAE tax legislation item (Open key <c>leg_id</c>) plus same-tax_category siblings. erp_summary is a short excerpt; pdf_url/passport/compliance_actions_json omitted.</summary>
    Task<CpUaeTaxItemDetailResult> BuildCpUaeTaxComplianceDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only budgets (note omitted).</summary>
    Task<CpBudgetsDigestResult> BuildCpBudgetsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened budget (PHP <c>budget_id</c>) plus monthly lines. Note is a short excerpt.</summary>
    Task<CpBudgetsDetailResult> BuildCpBudgetsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only carriers (contact PII omitted).</summary>
    Task<CpCarriersDigestResult> BuildCpCarriersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only payment gateways (parameters/credentials omitted).</summary>
    Task<CpPaymentGatewaysDigestResult> BuildCpPaymentGatewaysDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only workflows (trigger_config/JSON omitted).</summary>
    Task<CpWorkflowsDigestResult> BuildCpWorkflowsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened workflow (Open key <c>workflow_id</c>) plus same-trigger siblings. description is a short excerpt. trigger_config omitted.</summary>
    Task<CpWorkflowDetailResult> BuildCpWorkflowDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only purchase requisitions (justification/decision_note omitted).</summary>
    Task<CpPurchaseRequestsDigestResult> BuildCpPurchaseRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened requisition header + lines (PHP <c>rq=</c> detail).</summary>
    Task<CpPurchaseRequestDetailResult> BuildCpPurchaseRequestDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only promotions (epc_promo_promotions).</summary>
    Task<CpPromotionsDigestResult> BuildCpPromotionsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened promotion (PHP <c>promo_id=</c> detail, includes validity window).</summary>
    Task<CpPromotionDetailResult> BuildCpPromotionDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM opportunities (notes omitted).</summary>
    Task<CpCrmOpportunitiesDigestResult> BuildCpCrmOpportunitiesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened CRM opportunity plus activities (PHP <c>opp_id</c>).</summary>
    Task<CpCrmOpportunityDetailResult> BuildCpCrmOpportunityDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only integrations/webhooks (secrets/events omitted).</summary>
    Task<CpIntegrationsDigestResult> BuildCpIntegrationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only page builder layouts (layout_json/brand_json omitted).</summary>
    Task<CpPageBuilderDigestResult> BuildCpPageBuilderDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened page-builder layout (PHP <c>layout_id=</c> detail, includes JSON payloads).</summary>
    Task<CpPageBuilderLayoutDetailResult> BuildCpPageBuilderLayoutDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only product catalogue (shop_catalogue_products).</summary>
    Task<CpProductCatalogueDigestResult> BuildCpProductCatalogueDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened catalogue product (PHP <c>product_id</c>) plus same-category siblings. Product text is a short excerpt.</summary>
    Task<CpProductCatalogueDetailResult> BuildCpProductCatalogueDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only platform governance rules (description/config_json omitted).</summary>
    Task<CpPlatformGovernanceDigestResult> BuildCpPlatformGovernanceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened governance rule (PHP <c>rule_id</c>) plus category siblings. Description excerpt only; config_json omitted.</summary>
    Task<CpPlatformGovernanceRuleDetailResult> BuildCpPlatformGovernanceRuleDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only e-invoice documents (payload JSON/XML omitted).</summary>
    Task<CpEinvoiceDocumentsDigestResult> BuildCpEinvoiceDocumentsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened e-invoice (PHP <c>ei_id=</c> detail) plus lines and events.</summary>
    Task<CpEinvoiceDocumentDetailResult> BuildCpEinvoiceDocumentDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery repairs (customer PII/narration omitted).</summary>
    Task<CpJewelleryRepairsDigestResult> BuildCpJewelleryRepairsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened jewellery repair (Open key <c>repair_id</c>) plus same-status siblings. narration/stone_details are short excerpts. Phone/mobile omitted.</summary>
    Task<CpJewelleryRepairDetailResult> BuildCpJewelleryRepairDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM tickets (message bodies omitted).</summary>
    Task<CpCrmTicketsDigestResult> BuildCpCrmTicketsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened CRM ticket (PHP <c>ticket_id</c>) plus message excerpts. Full bodies omitted.</summary>
    Task<CpCrmTicketsDetailResult> BuildCpCrmTicketsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketing growth reviews (notes omitted).</summary>
    Task<CpMarketingGrowthDigestResult> BuildCpMarketingGrowthDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened marketing growth review (PHP <c>review_id</c>) plus strategy siblings. Notes excerpt only.</summary>
    Task<CpMarketingGrowthReviewDetailResult> BuildCpMarketingGrowthReviewDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only SOC 2 controls (description/implementation omitted).</summary>
    Task<CpSoc2ComplianceDigestResult> BuildCpSoc2ComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened SOC 2 control + evidence (PHP <c>soc2_id=</c> detail).</summary>
    Task<CpSoc2ControlDetailResult> BuildCpSoc2ControlDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only cost model item assignments.</summary>
    Task<CpCostModelsDigestResult> BuildCpCostModelsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened cost model assignment (PHP <c>costm_id=</c> detail) plus txns and closes.</summary>
    Task<CpCostModelItemDetailResult> BuildCpCostModelItemDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only financial periods (allocation/accrual JSON omitted).</summary>
    Task<CpFinAdvancedDigestResult> BuildCpFinAdvancedDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened financial-depth period (PHP <c>period_id</c>) plus company alloc/accrual/FX rows. JSON payloads omitted.</summary>
    Task<CpFinAdvancedPeriodDetailResult> BuildCpFinAdvancedPeriodDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only blockchain proofs (payload/merkle JSON omitted).</summary>
    Task<CpBlockchainProofsDigestResult> BuildCpBlockchainProofsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened blockchain proof (PHP <c>proof_id</c>). Payload excerpt only; merkle JSON omitted.</summary>
    Task<CpBlockchainProofDetailResult> BuildCpBlockchainProofDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only landed cost sheets (notes omitted).</summary>
    Task<CpLandedCostDigestResult> BuildCpLandedCostDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened landed cost sheet + expenses/lines (PHP <c>sheet_id=</c> detail).</summary>
    Task<CpLandedCostSheetDetailResult> BuildCpLandedCostSheetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only WMS work pool.</summary>
    Task<CpWarehouseWmsDigestResult> BuildCpWarehouseWmsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened WMS work (PHP <c>work_id</c>) plus wave siblings. List omits from/to location and LP.</summary>
    Task<CpWarehouseWmsDetailResult> BuildCpWarehouseWmsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only AI service queries (input/output text omitted).</summary>
    Task<CpAiServiceDigestResult> BuildCpAiServiceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened AI query (PHP <c>query_id</c>) plus service siblings. Input excerpt only; output omitted.</summary>
    Task<CpAiServiceQueryDetailResult> BuildCpAiServiceQueryDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only returns/RMA requests (description/notes omitted).</summary>
    Task<CpReturnsRmaDigestResult> BuildCpReturnsRmaDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened aftersales RMA header + items (PHP <c>rma_id=</c> detail).</summary>
    Task<CpReturnsRmaDetailResult> BuildCpReturnsRmaDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened shop return header + lines (PHP <c>return_id=</c> detail).</summary>
    Task<CpShopReturnDetailResult> BuildCpShopReturnDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only commerce isolation audit runs (report_json omitted).</summary>
    Task<CpIsolationAuditDigestResult> BuildCpIsolationAuditDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened isolation audit run (PHP <c>run_id</c>) plus same-day violation excerpts. report_json excerpt only.</summary>
    Task<CpIsolationAuditRunDetailResult> BuildCpIsolationAuditRunDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only AML KYC rows (notes/document paths omitted).</summary>
    Task<CpAmlComplianceDigestResult> BuildCpAmlComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened AML KYC + customer transactions (PHP <c>kyc_id=</c> detail).</summary>
    Task<CpAmlComplianceKycDetailResult> BuildCpAmlComplianceKycDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery karat/rate/barcode masters.</summary>
    Task<CpJewelleryMastersDigestResult> BuildCpJewelleryMastersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened jewellery karat (Open key <c>karat_id</c>) plus same-division siblings. description is a short excerpt.</summary>
    Task<CpJewelleryMastersKaratDetailResult> BuildCpJewelleryMastersDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only consolidation group entities.</summary>
    Task<CpConsolidationsDigestResult> BuildCpConsolidationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened consolidation entity (PHP <c>cons_id</c>) plus figures and IC rows.</summary>
    Task<CpConsolidationsDetailResult> BuildCpConsolidationsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM activities (notes omitted).</summary>
    Task<CpCrmActivitiesDigestResult> BuildCpCrmActivitiesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened CRM activity (PHP <c>activity_id</c>) plus related siblings. Notes excerpt only.</summary>
    Task<CpCrmActivitiesDetailResult> BuildCpCrmActivitiesDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only auth MFA enrollments (secrets/hashes omitted).</summary>
    Task<CpAuthMfaDigestResult> BuildCpAuthMfaDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only electronic reporting formats (preview omitted).</summary>
    Task<CpElectronicReportingDigestResult> BuildCpElectronicReportingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened electronic reporting format (PHP <c>format_id=</c> detail) plus fields and run previews.</summary>
    Task<CpElectronicReportingFormatDetailResult> BuildCpElectronicReportingFormatDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only collections/dunning queue (notes omitted).</summary>
    Task<CpCollectionsDunningDigestResult> BuildCpCollectionsDunningDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened dunning queue row + log (PHP <c>queue_id=</c> detail).</summary>
    Task<CpCollectionsDunningDetailResult> BuildCpCollectionsDunningDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketplace channels (config_json omitted).</summary>
    Task<CpMarketplaceChannelsDigestResult> BuildCpMarketplaceChannelsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only demand intelligence countries.</summary>
    Task<CpDemandIntelligenceDigestResult> BuildCpDemandIntelligenceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only credit limits (notes omitted).</summary>
    Task<CpCreditLimitsDigestResult> BuildCpCreditLimitsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened credit limit (Open key <c>credit_id</c>) plus same-status siblings. notes/hold_reason are short excerpts.</summary>
    Task<CpCreditLimitsDetailResult> BuildCpCreditLimitsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only insurance policies (notes/emails omitted).</summary>
    Task<CpInsuranceComplianceDigestResult> BuildCpInsuranceComplianceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened insurance policy + documents + claims (PHP <c>pol=</c> detail).</summary>
    Task<CpInsuranceComplianceDetailResult> BuildCpInsuranceComplianceDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP audit trail (detail/old/new JSON omitted).</summary>
    Task<CpAuditTrailDigestResult> BuildCpAuditTrailDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened audit event (PHP <c>event_id</c>).</summary>
    Task<CpAuditTrailDetailResult> BuildCpAuditTrailDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only document expiry register (notes/emails/paths omitted).</summary>
    Task<CpDocExpiryDigestResult> BuildCpDocExpiryDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened expiry document plus reminder log (PHP <c>doc</c>).</summary>
    Task<CpDocExpiryDetailResult> BuildCpDocExpiryDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only tenant config keys (config_value omitted).</summary>
    Task<CpTenantConfigDigestResult> BuildCpTenantConfigDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened tenant config row plus history (PHP <c>config_id</c>).</summary>
    Task<CpTenantConfigDetailResult> BuildCpTenantConfigDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only jewellery stock verification vouchers (remarks omitted).</summary>
    Task<CpJewelleryStockVerificationDigestResult> BuildCpJewelleryStockVerificationDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened jewellery stock verification (Open key <c>verify_id</c>) plus same-status siblings. remarks is a short excerpt.</summary>
    Task<CpJewelleryStockVerificationDetailResult> BuildCpJewelleryStockVerificationDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only bank statement lines for reconciliation.</summary>
    Task<ErpBankReconciliationDigestResult> BuildErpBankReconciliationDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened bank statement line (Open key <c>recon_line_id</c>, remapped by <c>tab=bank_recon</c>) plus same-account siblings. Surfaces line_date + time_created hidden from the list table.</summary>
    Task<ErpBankReconciliationLineDetailResult> BuildErpBankReconciliationLineDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only warehouse stock transfers (notes omitted).</summary>
    Task<ErpStockTransfersDigestResult> BuildErpStockTransfersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened stock transfer (Open key <c>transfer_id</c>) plus same-status siblings. notes is a short excerpt. Line bodies omitted.</summary>
    Task<ErpStockTransferDetailResult> BuildErpStockTransferDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only CRM/sales quotations (notes omitted).</summary>
    Task<ErpSalesQuotationsDigestResult> BuildErpSalesQuotationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened sales quotation (Open key <c>quote_id</c>) plus same-status siblings. notes is a short excerpt. Line bodies omitted.</summary>
    Task<ErpSalesQuotationDetailResult> BuildErpSalesQuotationDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP workspace favorites/shortcuts.</summary>
    Task<ErpWorkspaceFavoritesDigestResult> BuildErpWorkspaceFavoritesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened workspace favorite (Open key <c>favorite_id</c>, remapped by <c>tab=favorites</c>) plus same-surface siblings. icon_color plus hidden target_url/icon_class.</summary>
    Task<ErpWorkspaceFavoriteDetailResult> BuildErpWorkspaceFavoriteDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only fixed assets register (note omitted).</summary>
    Task<ErpFixedAssetsDigestResult> BuildErpFixedAssetsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened fixed asset (Open key <c>asset_id</c>) plus same-status siblings. note is a short excerpt.</summary>
    Task<ErpFixedAssetDetailResult> BuildErpFixedAssetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only process-flow cases from PHP <c>epc_pf_cases</c> (writes remain PHP).</summary>
    Task<ErpProcessFlowTasksDigestResult> BuildErpProcessFlowTasksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only ERP report-center registry (+ optional table-backed/computed run). CSV/export remain PHP.</summary>
    Task<ErpReportCenterDigestResult> BuildErpReportCenterDigestAsync(string? key, int limit, CancellationToken cancellationToken = default, int? companyId = null);

    /// <summary>Read-only AR/AP/inventory aging (PHP <c>epc_erp_aging.php</c>).</summary>
    Task<ErpAgingDigestResult> BuildErpAgingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only AR customer balances (PHP <c>epc_erp_receivables</c>).</summary>
    Task<ErpReceivablesDigestResult> BuildErpReceivablesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only inventory movement ledger (PHP <c>epc_erp_inventory_ledger</c>).</summary>
    Task<ErpInventoryMovementsDigestResult> BuildErpInventoryMovementsDigestAsync(int limit, int? itemId = null, int? warehouseId = null, CancellationToken cancellationToken = default);

    /// <summary>Opened inventory movement (Open key <c>movement_id</c>, remapped by <c>tab=ledger</c>) plus same-warehouse siblings. note is a short excerpt. Surfaces batch_no + total_cost hidden from the list table.</summary>
    Task<ErpInventoryMovementDetailResult> BuildErpInventoryMovementDetailAsync(long id, CancellationToken cancellationToken = default);

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

    /// <summary>Authenticated cart (<c>session_id=0</c>) or guest cart when <paramref name="sessionId"/> &gt; 0.</summary>
    Task<StorefrontCartListResult> ListStorefrontCartAsync(int userId, int limit, CancellationToken cancellationToken = default, long sessionId = 0);

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

    /// <summary>Opened PO request (PHP <c>po_req_id</c>) plus step excerpts. items/attachments JSON omitted.</summary>
    Task<CpPoApprovalsDetailResult> BuildCpPoApprovalsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpFinanceCloseDigestResult> BuildCpFinanceCloseDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened finance-close batch (PHP <c>batch_id=</c> detail) plus opening lines.</summary>
    Task<CpFinanceCloseBatchDetailResult> BuildCpFinanceCloseBatchDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpJewelleryFixingDigestResult> BuildCpJewelleryFixingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened jewellery fixing (Open key <c>fixing_id</c>) plus same-status siblings. remarks is a short excerpt.</summary>
    Task<CpJewelleryFixingDetailResult> BuildCpJewelleryFixingDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpWebTrackerDigestResult> BuildCpWebTrackerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpWebTrackerDashboardResult> BuildCpWebTrackerDashboardAsync(CpWebTrackerFilterQuery filters, CancellationToken cancellationToken = default);
    Task<CpWebTrackerSessionDetailResult> BuildCpWebTrackerSessionDetailAsync(long sessionId, string siteKey, bool isSuper, CancellationToken cancellationToken = default);
    Task<CpAbandonedCartsDigestResult> BuildCpAbandonedCartsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened abandoned cart line + sibling lines (PHP <c>cart_id=</c> detail).</summary>
    Task<CpAbandonedCartsDetailResult> BuildCpAbandonedCartsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpQuoteRequestsDigestResult> BuildCpQuoteRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened quote request + lines (PHP <c>quote_id=</c> detail).</summary>
    Task<CpQuoteRequestDetailResult> BuildCpQuoteRequestDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpPlatformCommunicationDigestResult> BuildCpPlatformCommunicationDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened platform communication task (PHP <c>task_id</c>) plus category siblings. Description excerpt only.</summary>
    Task<CpPlatformCommunicationTaskDetailResult> BuildCpPlatformCommunicationTaskDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpInfoBlocksDigestResult> BuildCpInfoBlocksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened info block (PHP <c>block_id</c>) plus placement siblings. Content excerpt only.</summary>
    Task<CpInfoBlocksBlockDetailResult> BuildCpInfoBlocksBlockDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpFreeToolsDigestResult> BuildCpFreeToolsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened free-tools account (PHP <c>account_id</c>) plus saved tools. token/pass_hash/del_code_hash/payload omitted.</summary>
    Task<CpFreeToolsAccountDetailResult> BuildCpFreeToolsAccountDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpConfigSandboxDigestResult> BuildCpConfigSandboxDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened config snapshot (PHP <c>snapshot_id</c>) plus change keys. config_data excerpt only; old/new values omitted.</summary>
    Task<CpConfigSandboxSnapshotDetailResult> BuildCpConfigSandboxSnapshotDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpMarketplaceAppsDigestResult> BuildCpMarketplaceAppsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened marketplace app (PHP <c>app_id</c>) plus installs/reviews. Config JSON and review_text omitted.</summary>
    Task<CpMarketplaceAppDetailResult> BuildCpMarketplaceAppDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpNotificationsDigestResult> BuildCpNotificationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened notification (PHP <c>notif_id</c>) plus category siblings. Body excerpt only; metadata omitted.</summary>
    Task<CpNotificationsDetailResult> BuildCpNotificationsDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>notifications_settings</c> email/SMS flags. Template bodies omitted.</summary>
    Task<CpNotificationSettingsDigestResult> BuildCpNotificationSettingsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPortalSettingsDigestResult> BuildCpPortalSettingsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDataMigrationsDigestResult> BuildCpDataMigrationsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened data migration plus row statuses (PHP <c>migration_id</c>). Raw/mapped JSON stay omitted from lines.</summary>
    Task<CpDataMigrationsDetailResult> BuildCpDataMigrationsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpGeoRegionsDigestResult> BuildCpGeoRegionsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened geo node (PHP <c>geo_id</c>) plus same-parent siblings. Caption is a short lang excerpt.</summary>
    Task<CpGeoRegionsDetailResult> BuildCpGeoRegionsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpProductFiltersDigestResult> BuildCpProductFiltersDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened product filter (PHP <c>filter_id</c>) plus same-manufacturer siblings. list_storages is a short excerpt.</summary>
    Task<CpProductFiltersDetailResult> BuildCpProductFiltersDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpSearchTabsDigestResult> BuildCpSearchTabsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened search tab (PHP <c>tab_id</c>) plus enabled siblings. parameters_values is a short excerpt.</summary>
    Task<CpSearchTabsDetailResult> BuildCpSearchTabsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpSystemRequestsDigestResult> BuildCpSystemRequestsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened VIN request (PHP <c>vin_id</c>) plus message excerpts and same-user siblings. Full HTML omitted.</summary>
    Task<CpSystemRequestsDetailResult> BuildCpSystemRequestsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpAdditionalTextsDigestResult> BuildCpAdditionalTextsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened additional text (PHP <c>text_id</c>) plus same-placement siblings. Content excerpt only.</summary>
    Task<CpAdditionalTextsDetailResult> BuildCpAdditionalTextsDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpSliderBannersDigestResult> BuildCpSliderBannersDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpStructureDumpsDigestResult> BuildCpStructureDumpsDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpCommunicationsTestDigestResult> BuildCpCommunicationsTestDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpLanguagesDigestResult> BuildCpLanguagesDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpPluginsManagerDigestResult> BuildCpPluginsManagerDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened plugin (Open key <c>plugin_id</c>) plus same-frontend siblings. data_value is a short excerpt.</summary>
    Task<CpPluginsManagerDetailResult> BuildCpPluginsManagerDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpTemplatesManagerDigestResult> BuildCpTemplatesManagerDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened storefront/CP template (Open key <c>tpl_id</c>) plus same-frontend siblings. data_value is a short excerpt.</summary>
    Task<CpTemplatesManagerDetailResult> BuildCpTemplatesManagerDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpDesignTokensDigestResult> BuildCpDesignTokensDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpSitemapDigestResult> BuildCpSitemapDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened sitemap content URL (Open key <c>sm_id</c>) plus same-published siblings. content HTML is a short excerpt.</summary>
    Task<CpSitemapDetailResult> BuildCpSitemapDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpFailoverStatusDigestResult> BuildCpFailoverStatusDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpOpsGuidesDigestResult> BuildCpOpsGuidesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened ops-guides menu item (PHP <c>item_id</c>) plus group siblings. Guide HTML omitted.</summary>
    Task<CpOpsGuideItemDetailResult> BuildCpOpsGuideItemDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<CpFileManagerDigestResult> BuildCpFileManagerDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpServerIpDigestResult> BuildCpServerIpDigestAsync(int limit, CancellationToken cancellationToken = default);
    Task<CpDebugConsoleDigestResult> BuildCpDebugConsoleDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Next-wave: commerce statistics KPIs + top article queries (ip omitted).</summary>
    Task<CpStatisticsDigestResult> BuildCpStatisticsDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Accessories listings digest. Listing and photo filename writes are live-gated.</summary>
    Task<CpAccessoriesDigestResult> BuildCpAccessoriesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened accessory listing (PHP <c>listing_id=</c> detail) plus photos.</summary>
    Task<CpAccessoriesListingDetailResult> BuildCpAccessoriesListingDetailAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: manufacturer synonyms digest (writes remain module-ajax dry-run).</summary>
    Task<CpSynonymsDigestResult> BuildCpSynonymsDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened manufacturer + synonym children (PHP <c>manufacturer_id=</c> detail, includes synonym id).</summary>
    Task<CpSynonymDetailResult> BuildCpSynonymDetailAsync(long manufacturerId, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: SEO content KPIs (sitemap/robots; ping/warm remain PHP).</summary>
    Task<CpSeoDigestResult> BuildCpSeoDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: social hub accounts/drafts (credentials omitted; publish dry-run).</summary>
    Task<CpSocialHubDigestResult> BuildCpSocialHubDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened social account (PHP <c>social_id</c>) plus drafts. Credentials and last_error omitted; caption excerpt only.</summary>
    Task<CpSocialHubAccountDetailResult> BuildCpSocialHubAccountDetailAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: tenant feature flags matrix (save dry-run).</summary>
    Task<CpTenantFeaturesDigestResult> BuildCpTenantFeaturesDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: customer board user peek (writes remain PHP).</summary>
    Task<CpCustomerBoardDigestResult> BuildCpCustomerBoardDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened customer-board user (PHP <c>user_id</c>) plus group binds. Password omitted.</summary>
    Task<CpCustomerBoardUserDetailResult> BuildCpCustomerBoardUserDetailAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>Next-wave: fulfillment queue digest (OMS stage writes remain dry-run).</summary>
    Task<CpFulfillmentQueueDigestResult> BuildCpFulfillmentQueueDigestAsync(int limit, CancellationToken cancellationToken = default, string? status = null);
    /// <summary>PHP <c>epc_fulfillment_get</c> read-only detail. Writes remain PHP / OMS dry-run.</summary>
    Task<CpFulfillmentDetailDigest?> GetCpFulfillmentDetailAsync(long fulfillmentId, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: SSO/SAML providers (certs/metadata omitted).</summary>
    Task<CpSsoSamlDigestResult> BuildCpSsoSamlDigestAsync(int limit, CancellationToken cancellationToken = default);
    /// <summary>Next-wave Super-only: MySQL epc_events bus peek (no Kafka/Rabbit).</summary>
    Task<CpEventBusDigestResult> BuildCpEventBusDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened MySQL <c>epc_events</c> row (PHP <c>event_id</c>). Payload excerpt only.</summary>
    Task<CpEventBusDetailResult> BuildCpEventBusDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only on-premises license registry (notes/fingerprint/ip omitted; license keys masked).</summary>
    Task<OnPremisesLicenseListResult> ListOnPremisesLicensesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ErpDeliveryNoteListResult> ListErpDeliveryNotesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened delivery note (Open key <c>delivery_note_id</c>) plus same-status siblings. notes is a short excerpt. pdf_path omitted.</summary>
    Task<ErpDeliveryNoteDetailResult> BuildErpDeliveryNoteDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpRfqListResult> ListErpRfqsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened supplier RFQ (Open key <c>rfq_id</c>) plus same-status siblings. description is a short excerpt.</summary>
    Task<ErpRfqDetailResult> BuildErpRfqDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpThreeWayMatchListResult> ListErpThreeWayMatchAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened three-way match PO (Open key <c>po_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpThreeWayMatchDetailResult> BuildErpThreeWayMatchDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpContactListResult> ListErpContactsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened contact (Open key <c>contact_id</c>) plus same-city siblings. notes is a short excerpt; email/phone omitted.</summary>
    Task<ErpContactDetailResult> BuildErpContactDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpPaymentBatchListResult> ListErpPaymentBatchesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened payment batch (Open key <c>batch_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpPaymentBatchDetailResult> BuildErpPaymentBatchDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpFiscalPeriodListResult> ListErpFiscalPeriodsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened fiscal period (Open key <c>period_id</c>, remapped by <c>tab=year_end</c>) plus same-status siblings. note is a short excerpt; checklist JSON omitted.</summary>
    Task<ErpFiscalPeriodDetailResult> BuildErpFiscalPeriodDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpAgendaEventListResult> ListErpAgendaEventsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened agenda event (Open key <c>event_id</c>, remapped by <c>tab=agenda</c>) plus same-type siblings. notes is a short excerpt.</summary>
    Task<ErpAgendaEventDetailResult> BuildErpAgendaEventDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpDocumentListResult> ListErpDocumentsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened document (Open key <c>document_id</c>, remapped by <c>tab=documents</c>) plus same-category siblings. notes is a short excerpt; file_path omitted.</summary>
    Task<ErpDocumentDetailResult> BuildErpDocumentDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpExpenseReportListResult> ListErpExpenseReportsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened expense report (Open key <c>expense_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpExpenseReportDetailResult> BuildErpExpenseReportDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only shop_offices + storage/geo maps (PHP offices.php).</summary>
    Task<CpOfficesDigestResult> BuildCpOfficesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened office (PHP <c>office_id</c>) plus same-city siblings. users is a short excerpt.</summary>
    Task<CpOfficeDetailResult> BuildCpOfficesDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only workshop jobs (customer phone/email omitted).</summary>
    Task<CpWorkshopDigestResult> BuildCpWorkshopDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened workshop job (Open key <c>job_id</c>) plus same-status siblings. notes/complaint are short excerpts; phone/email omitted.</summary>
    Task<CpWorkshopJobDetailResult> BuildCpWorkshopDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only KKT devices (PHP devices.php; customer contact omitted).</summary>
    Task<CpKktDigestResult> BuildCpKktDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only CP bulk-upload history (PHP bulk_upload_hub; file bodies omitted).</summary>
    Task<CpBulkUploadDigestResult> BuildCpBulkUploadDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened bulk-upload history row (Open key <c>upload_id</c>) plus same-priority siblings. result_json, csv_result, and cp_notes are short excerpts. File bodies omitted.</summary>
    Task<CpBulkUploadDetailResult> BuildCpBulkUploadDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only tenant SMTP settings (password/username omitted).</summary>
    Task<CpTenantEmailDigestResult> BuildCpTenantEmailDigestAsync(CancellationToken cancellationToken = default);

    /// <summary>Read-only department workflow board from PHP <c>epc_erp_workflow_tasks</c> (writes remain PHP).</summary>
    Task<ErpWorkflowTasksDigestResult> BuildErpWorkflowTasksDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Operational VAT 201 boxes (shop orders + purchases). FTA filing stays PHP.</summary>
    Task<ErpVatReturnDigestResult> BuildErpVatReturnDigestAsync(long? fromUnix = null, long? toUnix = null, CancellationToken cancellationToken = default);

    /// <summary>Read-only withholding codes + transactions (PHP <c>epc_wht_*</c>). Settle, code save, record, and certificate writes are live.</summary>
    Task<ErpWithholdingDigestResult> BuildErpWithholdingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened withholding transaction (PHP <c>txn_id</c>).</summary>
    Task<ErpWithholdingTxnDetailResult> BuildErpWithholdingTxnDetailAsync(long id, CancellationToken cancellationToken = default);

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

    /// <summary>Read-only staff profiles (PHP <c>epc_erp_staff_profiles</c>; writes remain PHP).</summary>
    Task<ErpStaffListResult> ListErpStaffAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened staff profile (Open key <c>staff_id</c>) plus same-department siblings. email/phone omitted.</summary>
    Task<ErpStaffProfileDetailResult> BuildErpStaffProfileDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only HR salary/leave rows (PHP <c>epc_erp_hr_list</c>; notes omitted).</summary>
    Task<ErpHrListResult> ListErpHrRecordsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Read-only contracts register (PHP <c>epc_erp_contracts</c>; body/OCR omitted).</summary>
    Task<ErpContractsListResult> ListErpContractsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened contract (Open key <c>contract_id</c>) plus same-status siblings. body_text and ocr_text are short excerpts.</summary>
    Task<ErpContractDetailResult> BuildErpContractDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only opening-balance batches (PHP <c>epc_erp_opening_batches</c>).</summary>
    Task<ErpOpeningListResult> ListErpOpeningBatchesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened opening batch (Open key <c>batch_id</c>, remapped by <c>tab=opening</c>) plus same-status siblings. note is a short excerpt; line meta_json omitted.</summary>
    Task<ErpOpeningBatchDetailResult> BuildErpOpeningBatchDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only marketing campaigns (PHP <c>epc_erp_marketing_campaigns</c>).</summary>
    Task<ErpMarketingListResult> ListErpMarketingCampaignsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened marketing campaign (Open key <c>campaign_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpMarketingCampaignDetailResult> BuildErpMarketingCampaignDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only payroll runs (PHP <c>epc_erp_payroll_runs</c>).</summary>
    Task<ErpPayrollListResult> ListErpPayrollRunsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened payroll run (Open key <c>payroll_id</c>) plus same-status siblings. note is a short excerpt.</summary>
    Task<ErpPayrollRunDetailResult> BuildErpPayrollRunDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only print templates (PHP <c>epc_erp_print_templates</c>; HTML/CSS omitted).</summary>
    Task<ErpPrintTemplatesListResult> ListErpPrintTemplatesAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened print template (Open key <c>template_id</c>, remapped by <c>tab=print_designer</c>) plus same-type siblings. HTML/CSS are short excerpts.</summary>
    Task<ErpPrintTemplateDetailResult> BuildErpPrintTemplateDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpOrderPlanningDigestResult> BuildErpOrderPlanningDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened recommendation (Open key <c>opl_rec_id</c>, remapped by <c>tab=order_planning</c> / <c>master_planning</c>) plus same-status siblings. Surfaces item id and time_updated hidden from the list table.</summary>
    Task<ErpOrderRecommendationDetailResult> BuildErpOrderPlanningRecommendationDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpProcurementCategoriesDigestResult> BuildErpProcurementCategoriesDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened procurement category (Open key <c>proc_cat_id</c>) plus same-parent siblings. company_id is hidden from the list.</summary>
    Task<ErpProcCategoryDetailResult> BuildErpProcCategoryDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened procurement policy (Open key <c>proc_pol_id</c>) plus same-category siblings. company_id and created time are hidden from the list.</summary>
    Task<ErpProcPolicyDetailResult> BuildErpProcPolicyDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpQualityDigestResult> BuildErpQualityDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened NCR (Open key <c>ncr_id</c>, remapped by <c>qv=ncr</c>) plus same-status siblings. Surfaces 280-char corrective-action excerpt and time_closed hidden from the list.</summary>
    Task<ErpQmNcrDetailResult> BuildErpQualityNcrDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpRfidDigestResult> BuildErpRfidDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened RFID scan session (Open key <c>session_id</c>, remapped by <c>tab=rfid</c>) plus same-status siblings. Reader IP/TID omitted.</summary>
    Task<ErpRfidSessionDetailResult> BuildErpRfidSessionDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpRecruitmentDigestResult> BuildErpRecruitmentDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened job requisition (Open key <c>hrt_job_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpRecruitmentJobDetailResult> BuildErpRecruitmentJobDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened applicant (Open key <c>applicant_id</c>) plus same-stage siblings. notes is a short excerpt. email/phone omitted.</summary>
    Task<ErpRecruitmentApplicantDetailResult> BuildErpRecruitmentApplicantDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpCustomerGroupsDigestResult> ListErpCustomerGroupsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened customer group (Open key <c>cgroup_id</c>) plus same-type siblings. description is a short excerpt.</summary>
    Task<ErpCustomerGroupDetailResult> BuildErpCustomerGroupDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpPerformanceDigestResult> BuildErpPerformanceDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened performance review (Open key <c>hrt_review_id</c>) plus same-status siblings. notes is a short excerpt.</summary>
    Task<ErpPerformanceReviewDetailResult> BuildErpPerformanceReviewDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpProductInfoDigestResult> BuildErpProductInfoDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened item master (Open key <c>pm_item_id</c>) plus same-type siblings. track_expiry is hidden from the list. notes/barcode omitted.</summary>
    Task<ErpProductInfoItemDetailResult> BuildErpProductInfoItemDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened field definition (Open key <c>pm_field_id</c>) plus same-type siblings. options_json is a short excerpt.</summary>
    Task<ErpProductInfoFieldDetailResult> BuildErpProductInfoFieldDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened variant (Open key <c>pm_variant_id</c>) plus same-item siblings. combo_json is a short excerpt.</summary>
    Task<ErpProductInfoVariantDetailResult> BuildErpProductInfoVariantDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpReportSchedulerDigestResult> BuildErpReportSchedulerDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened report schedule (Open key <c>rsched_id</c>) plus same-type siblings. recipients/body/subject/filters omitted.</summary>
    Task<ErpReportScheduleDetailResult> BuildErpReportScheduleDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpProjectAccountingDigestResult> BuildErpProjectAccountingDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened project budget (Open key <c>prja_budget_id</c>) plus same-project siblings. company_id is hidden from the list.</summary>
    Task<ErpPrjaBudgetDetailResult> BuildErpPrjaBudgetDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened project transaction (Open key <c>prja_txn_id</c>) plus same-project siblings. company_id is hidden from the list.</summary>
    Task<ErpPrjaTxnDetailResult> BuildErpPrjaTxnDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened recognition run (Open key <c>prja_rec_id</c>) plus same-project siblings. detail_json is a short excerpt.</summary>
    Task<ErpPrjaRecognitionDetailResult> BuildErpPrjaRecognitionDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpDocAttachmentsDigestResult> ListErpDocAttachmentsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened attachment (Open key <c>attach_id</c>, remapped by <c>tab=doc_attachment</c>) plus same-type siblings. description is a 280-char excerpt. file_path omitted.</summary>
    Task<ErpDocAttachmentDetailResult> BuildErpDocAttachmentDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ErpInventoryReportDigestResult> BuildErpInventoryReportDigestAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened inventory category (Open key <c>invrep_cat_id</c>) plus same-level siblings. company_id is hidden from the list.</summary>
    Task<ErpInventoryReportCategoryDetailResult> BuildErpInventoryReportCategoryDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened inventory snapshot (Open key <c>invrep_snap_id</c>) plus same-category siblings. company_id is hidden from the list.</summary>
    Task<ErpInventoryReportSnapshotDetailResult> BuildErpInventoryReportSnapshotDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only order→ERP pipeline log (PHP <c>epc_order_erp_log</c>; details JSON omitted).</summary>
    Task<ErpOrderPipelineListResult> ListErpOrderPipelineLogAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened pipeline log (Open key <c>pipeline_log_id</c>) plus same-order siblings. details is a 280-char excerpt — never the full JSON.</summary>
    Task<ErpOrderPipelineLogDetailResult> BuildErpOrderPipelineLogDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only inventory forecast (PHP <c>epc_inventory_forecast</c>; recompute writes on ASP.NET).</summary>
    Task<ErpInventoryForecastListResult> ListErpInventoryForecastAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened inventory forecast (Open key <c>inv_forecast_id</c>) plus same-status siblings. Surfaces site_key plus lead_time_days / safety_stock / eoq hidden from the list table.</summary>
    Task<ErpInventoryForecastDetailResult> BuildErpInventoryForecastDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only multi-entity groups + IC txns (PHP <c>epc_entity_groups</c>).</summary>
    Task<ErpMultiEntityListResult> ListErpMultiEntityAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened entity group (Open key <c>me_group_id</c>) plus members and same-status siblings. created_at is hidden from the list.</summary>
    Task<ErpEntityGroupDetailResult> BuildErpEntityGroupDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened inter-company txn (Open key <c>me_ic_id</c>) plus same-status siblings. description is a short excerpt hidden from the list table.</summary>
    Task<ErpIntercompanyTxnDetailResult> BuildErpIntercompanyTxnDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Read-only FX rates + multi-currency GL entries (PHP <c>epc_fx_rates</c>).</summary>
    Task<ErpMultiCurrencyGlListResult> ListErpMultiCurrencyGlAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Opened FX rate (Open key <c>mcgl_rate_id</c>) plus same-pair siblings. created time is hidden from the list.</summary>
    Task<ErpFxRateDetailResult> BuildErpFxRateDetailAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opened currency journal (Open key <c>mcgl_entry_id</c>) plus same-type siblings. Note excerpt and site are hidden from the list table.</summary>
    Task<ErpGlCurrencyEntryDetailResult> BuildErpGlCurrencyEntryDetailAsync(long id, CancellationToken cancellationToken = default);
}


