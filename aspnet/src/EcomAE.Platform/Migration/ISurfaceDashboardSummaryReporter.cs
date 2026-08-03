namespace EcomAE.Platform.Migration;

public interface ISurfaceDashboardSummaryReporter
{
    Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default);

    Task<ErpDashboardSummary> BuildErpAsync(CancellationToken cancellationToken = default);

    Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default);

    Task<StorefrontAccountSummary> BuildStorefrontAccountAsync(int userId, CancellationToken cancellationToken = default);

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

    Task<ErpInventoryStockSummaryResult> BuildErpInventoryStockSummaryAsync(CancellationToken cancellationToken = default);

    Task<CpCurrencyListResult> ListCpCurrenciesAsync(int limit, CancellationToken cancellationToken = default);

    Task<CpApiClientMetaListResult> ListCpApiClientsMetaAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only warehouse part search (writes/cart remain PHP part_search).</summary>
    Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, int limit, CancellationToken cancellationToken = default);

    /// <summary>Batch 4: read-only authenticated customer cart (qty/checkout writes remain PHP).</summary>
    Task<StorefrontCartListResult> ListStorefrontCartAsync(int userId, int limit, CancellationToken cancellationToken = default);
}
