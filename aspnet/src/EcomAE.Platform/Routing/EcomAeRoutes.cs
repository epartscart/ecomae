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
    public const string LiveSurfaceLinks = "/migration/live-surface-links";
    public const string TenantContext = "/tenant/context";
    public const string TenantWorkspaceParity = "/tenant/workspace/parity";
    public const string LegacySessionProbe = "/auth/session/probe";
    public const string LegacySessionParity = "/auth/session/parity";
    public const string LegacyApiClientParity = "/auth/api-client/parity";
    public const string ControlPanel = "/cp";
    public const string ControlPanelParity = "/cp/parity";
    public const string ControlPanelDashboardSummary = "/cp/dashboard-summary";
    public const string ControlPanelTenants = "/cp/tenants";
    public const string ControlPanelUsers = "/cp/users";
    public const string ControlPanelGroups = "/cp/groups";
    public const string ControlPanelModules = "/cp/modules";
    public const string ControlPanelConfigItems = "/cp/config-items";
    public const string ControlPanelMenus = "/cp/menus";
    public const string ControlPanelPages = "/cp/pages";
    public const string ControlPanelAdminSessions = "/cp/admin-sessions";
    public const string ControlPanelStorages = "/cp/storages";
    public const string ControlPanelCurrencies = "/cp/currencies";
    public const string ControlPanelApiClients = "/cp/api-clients";
    public const string Erp = "/erp";
    public const string ErpParity = "/erp/parity";
    public const string ErpDashboardSummary = "/erp/dashboard-summary";
    public const string ErpAccountsSummary = "/erp/accounts-summary";
    public const string ErpSuppliers = "/erp/suppliers";
    public const string ErpPurchases = "/erp/purchases";
    public const string ErpCashAccounts = "/erp/cash-accounts";
    public const string ErpCashEntries = "/erp/cash-entries";
    public const string ErpInvoices = "/erp/invoices";
    public const string ErpGlJournals = "/erp/gl-journals";
    public const string ErpCoaAccounts = "/erp/coa-accounts";
    public const string ErpWarehouses = "/erp/warehouses";
    public const string ErpSalesOrders = "/erp/sales-orders";
    public const string ErpPurchaseOrders = "/erp/purchase-orders";
    public const string ErpInventoryStock = "/erp/inventory-stock";
    public const string Bos = "/bos";
    public const string BosParity = "/bos/parity";
    public const string BosFleetSummary = "/bos/fleet-summary";
    public const string BosTenants = "/bos/tenants";
    public const string BosFleetHealth = "/bos/fleet-health";
    public const string BosFleetReadiness = "/bos/fleet-readiness";
    public const string BosAuditLog = "/bos/audit-log";
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
    public const string StorefrontAccount = "/storefront/account";
    public const string StorefrontAccountSummary = "/storefront/account-summary";
    public const string StorefrontOrders = "/storefront/orders";
    public const string StorefrontGarage = "/storefront/garage";
    public const string StorefrontProfile = "/storefront/profile";

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
