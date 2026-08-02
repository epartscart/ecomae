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
}
