using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceDigestContractValidatorTests
{
    [Fact]
    public async Task MigrationDigestsSatisfyFieldContractsForSummaryRoutes()
    {
        var reporter = new SurfaceDashboardSummaryReporter(new UnconfiguredFactory());
        var session = new
        {
            kind = "Admin",
            user_id = 1,
            email = "parity@example.com",
            group_ids = new[] { 3 },
            has_backend_access = true,
            capabilities = new[] { "cp", "erp", "bos" },
            module_acl = Array.Empty<object>(),
            permissions = Array.Empty<string>()
        };

        var payloads = new Dictionary<string, object>
        {
            ["/cp/dashboard-summary"] = new
            {
                ok = true,
                surface = "cp",
                summary = await reporter.BuildControlPanelAsync(),
                session,
                note = "contract validation"
            },
            ["/erp/dashboard-summary"] = new
            {
                ok = true,
                surface = "erp",
                summary = await reporter.BuildErpAsync(),
                session,
                note = "contract validation"
            },
            ["/bos/fleet-summary"] = new
            {
                ok = true,
                surface = "bos",
                summary = await reporter.BuildBosAsync(),
                session,
                note = "contract validation"
            },
            ["/storefront/account-summary"] = new
            {
                ok = true,
                surface = "storefront",
                summary = await reporter.BuildStorefrontAccountAsync(9),
                session = new { kind = "Customer", user_id = 9, email = "c@example.com", group_ids = Array.Empty<int>(), capabilities = new[] { "storefront_account" }, permissions = Array.Empty<string>() },
                note = "contract validation"
            },
            ["/erp/inventory-stock"] = new
            {
                ok = true,
                surface = "erp",
                summary = await reporter.BuildErpInventoryStockSummaryAsync(),
                session,
                note = "contract validation"
            },
            ["/bos/fleet-readiness"] = new
            {
                ok = true,
                surface = "bos",
                readiness = await reporter.BuildBosFleetReadinessAsync(),
                session,
                note = "contract validation"
            },
            ["/erp/accounts-summary"] = new
            {
                ok = true,
                surface = "erp",
                summary = (await reporter.BuildErpAccountsAsync()).Summary,
                source = "migration",
                message = "TenantRegistry DB is not configured.",
                session,
                note = "contract validation"
            }
        };

        foreach (var (route, envelope) in payloads)
        {
            var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == route);
            var json = SurfaceDigestContractValidator.SerializeEnvelope(envelope);
            var failures = SurfaceDigestContractValidator.Validate(contract, json);
            Assert.True(failures.Count == 0, $"{route}: {string.Join("; ", failures)}");
        }
    }

    [Fact]
    public async Task MigrationListDigestsSatisfyItemFieldContractsWhenEmpty()
    {
        var reporter = new SurfaceDashboardSummaryReporter(new UnconfiguredFactory());
        var session = new { kind = "Admin", user_id = 1, email = "parity@example.com", group_ids = new[] { 3 }, has_backend_access = true, capabilities = new[] { "cp", "erp", "bos" }, module_acl = Array.Empty<object>(), permissions = Array.Empty<string>() };

        var listPayloads = new Dictionary<string, object>
        {
            ["/cp/tenants"] = Envelope("cp", "tenants", await reporter.ListPortalTenantsAsync(10), session),
            ["/cp/orders-digest"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.ListCpOrdersAsync(10)).Summary,
                orders = (await reporter.ListCpOrdersAsync(10)).Orders,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/users"] = Envelope("cp", "users", await reporter.ListCpUsersAsync(10), session),
            ["/cp/groups"] = Envelope("cp", "groups", await reporter.ListCpGroupsAsync(10), session),
            ["/cp/modules"] = Envelope("cp", "modules", await reporter.ListCpModulesAsync(10), session),
            ["/cp/menus"] = Envelope("cp", "menus", await reporter.ListCpMenusAsync(10), session),
            ["/cp/pages"] = Envelope("cp", "pages", await reporter.ListCpPagesAsync(10), session),
            ["/cp/currencies"] = Envelope("cp", "currencies", await reporter.ListCpCurrenciesAsync(10), session),
            ["/cp/api-clients"] = Envelope("cp", "clients", await reporter.ListCpApiClientsMetaAsync(10), session),
            ["/cp/power-bi"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpPowerBiDigestAsync(10)).Summary,
                reports = (await reporter.BuildCpPowerBiDigestAsync(10)).Reports,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/mobile-apps"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpMobileAppsDigestAsync()).Summary,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/metabase"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpMetabaseDigestAsync(10)).Summary,
                dashboards = (await reporter.BuildCpMetabaseDigestAsync(10)).Dashboards,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/nl-reporting"] = Envelope("cp", "definitions", await reporter.ListCpNlReportDefinitionsAsync(10), session),
            ["/cp/marketing-broadcast"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpMarketingBroadcastDigestAsync(10)).Summary,
                campaigns = (await reporter.BuildCpMarketingBroadcastDigestAsync(10)).Campaigns,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/demo-tenants"] = Envelope("cp", "tenants", await reporter.ListCpDemoTenantsAsync(10), session),
            ["/cp/parts-agent-chats"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpPartsAgentDigestAsync(10)).Summary,
                sessions = (await reporter.BuildCpPartsAgentDigestAsync(10)).Sessions,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/pos-overview"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpPosOverviewDigestAsync(10)).Summary,
                sales = (await reporter.BuildCpPosOverviewDigestAsync(10)).Sales,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/tax-toolkits"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpTaxToolkitsDigestAsync(10)).Summary,
                toolkits = (await reporter.BuildCpTaxToolkitsDigestAsync(10)).Toolkits,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/sms-whatsapp"] = new
            {
                ok = true,
                surface = "cp",
                summary = (await reporter.BuildCpSmsWhatsappDigestAsync(10)).Summary,
                operators = (await reporter.BuildCpSmsWhatsappDigestAsync(10)).Operators,
                whatsappLog = (await reporter.BuildCpSmsWhatsappDigestAsync(10)).WhatsappLog,
                count = 0,
                source = "migration",
                message = "x",
                session,
                note = "contract validation"
            },
            ["/cp/crm-board"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCrmBoardDigestAsync(10)).Summary, leads = (await reporter.BuildCpCrmBoardDigestAsync(10)).Leads, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/document-control"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpDocumentControlDigestAsync(10)).Summary, templates = (await reporter.BuildCpDocumentControlDigestAsync(10)).Templates, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/delivery-methods"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpDeliveryMethodsDigestAsync(10)).Summary, modes = (await reporter.BuildCpDeliveryMethodsDigestAsync(10)).Modes, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/crosses"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCrossesDigestAsync(10)).Summary, pairs = (await reporter.BuildCpCrossesDigestAsync(10)).Pairs, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/hr-overview"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpHrOverviewDigestAsync(10)).Summary, employees = (await reporter.BuildCpHrOverviewDigestAsync(10)).Employees, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/production-overview"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpProductionOverviewDigestAsync(10)).Summary, workOrders = (await reporter.BuildCpProductionOverviewDigestAsync(10)).WorkOrders, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/projects-overview"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpProjectsOverviewDigestAsync(10)).Summary, projects = (await reporter.BuildCpProjectsOverviewDigestAsync(10)).Projects, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/industry-packs"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpIndustryPacksDigestAsync(10)).Summary, packs = (await reporter.BuildCpIndustryPacksDigestAsync(10)).Packs, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/jewellery-retail"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpJewelleryRetailDigestAsync(10)).Summary, vouchers = (await reporter.BuildCpJewelleryRetailDigestAsync(10)).Vouchers, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/price-lists"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPriceListsDigestAsync(10)).Summary, lists = (await reporter.BuildCpPriceListsDigestAsync(10)).Lists, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/auto-price"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpAutoPriceDigestAsync(10)).Summary, rules = (await reporter.BuildCpAutoPriceDigestAsync(10)).Rules, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/uae-tax-compliance"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpUaeTaxComplianceDigestAsync(10)).Summary, items = (await reporter.BuildCpUaeTaxComplianceDigestAsync(10)).Items, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/budgets"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpBudgetsDigestAsync(10)).Summary, budgets = (await reporter.BuildCpBudgetsDigestAsync(10)).Budgets, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/carriers"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCarriersDigestAsync(10)).Summary, carriers = (await reporter.BuildCpCarriersDigestAsync(10)).Carriers, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/payment-gateways"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPaymentGatewaysDigestAsync(10)).Summary, gateways = (await reporter.BuildCpPaymentGatewaysDigestAsync(10)).Gateways, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/workflows"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpWorkflowsDigestAsync(10)).Summary, workflows = (await reporter.BuildCpWorkflowsDigestAsync(10)).Workflows, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/purchase-requests"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPurchaseRequestsDigestAsync(10)).Summary, requests = (await reporter.BuildCpPurchaseRequestsDigestAsync(10)).Requests, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/promotions"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPromotionsDigestAsync(10)).Summary, promotions = (await reporter.BuildCpPromotionsDigestAsync(10)).Promotions, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/crm-opportunities"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCrmOpportunitiesDigestAsync(10)).Summary, opportunities = (await reporter.BuildCpCrmOpportunitiesDigestAsync(10)).Opportunities, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/integrations"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpIntegrationsDigestAsync(10)).Summary, integrations = (await reporter.BuildCpIntegrationsDigestAsync(10)).Integrations, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/page-builder"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPageBuilderDigestAsync(10)).Summary, layouts = (await reporter.BuildCpPageBuilderDigestAsync(10)).Layouts, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/product-catalogue"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpProductCatalogueDigestAsync(10)).Summary, products = (await reporter.BuildCpProductCatalogueDigestAsync(10)).Products, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/platform-governance"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpPlatformGovernanceDigestAsync(10)).Summary, rules = (await reporter.BuildCpPlatformGovernanceDigestAsync(10)).Rules, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/einvoice-documents"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpEinvoiceDocumentsDigestAsync(10)).Summary, documents = (await reporter.BuildCpEinvoiceDocumentsDigestAsync(10)).Documents, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/jewellery-repairs"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpJewelleryRepairsDigestAsync(10)).Summary, repairs = (await reporter.BuildCpJewelleryRepairsDigestAsync(10)).Repairs, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/crm-tickets"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCrmTicketsDigestAsync(10)).Summary, tickets = (await reporter.BuildCpCrmTicketsDigestAsync(10)).Tickets, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/marketing-growth"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpMarketingGrowthDigestAsync(10)).Summary, reviews = (await reporter.BuildCpMarketingGrowthDigestAsync(10)).Reviews, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/soc2-compliance"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpSoc2ComplianceDigestAsync(10)).Summary, controls = (await reporter.BuildCpSoc2ComplianceDigestAsync(10)).Controls, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/cost-models"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpCostModelsDigestAsync(10)).Summary, items = (await reporter.BuildCpCostModelsDigestAsync(10)).Items, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/fin-advanced"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpFinAdvancedDigestAsync(10)).Summary, periods = (await reporter.BuildCpFinAdvancedDigestAsync(10)).Periods, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/blockchain-proofs"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpBlockchainProofsDigestAsync(10)).Summary, proofs = (await reporter.BuildCpBlockchainProofsDigestAsync(10)).Proofs, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/landed-cost"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpLandedCostDigestAsync(10)).Summary, sheets = (await reporter.BuildCpLandedCostDigestAsync(10)).Sheets, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/warehouse-wms"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpWarehouseWmsDigestAsync(10)).Summary, work = (await reporter.BuildCpWarehouseWmsDigestAsync(10)).Work, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/ai-service"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpAiServiceDigestAsync(10)).Summary, queries = (await reporter.BuildCpAiServiceDigestAsync(10)).Queries, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/returns-rma"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpReturnsRmaDigestAsync(10)).Summary, requests = (await reporter.BuildCpReturnsRmaDigestAsync(10)).Requests, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/isolation-audit"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpIsolationAuditDigestAsync(10)).Summary, runs = (await reporter.BuildCpIsolationAuditDigestAsync(10)).Runs, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/aml-compliance"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpAmlComplianceDigestAsync(10)).Summary, kyc = (await reporter.BuildCpAmlComplianceDigestAsync(10)).Kyc, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/jewellery-masters"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpJewelleryMastersDigestAsync(10)).Summary, karats = (await reporter.BuildCpJewelleryMastersDigestAsync(10)).Karats, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/consolidations"] = new { ok = true, surface = "cp", summary = (await reporter.BuildCpConsolidationsDigestAsync(10)).Summary, entities = (await reporter.BuildCpConsolidationsDigestAsync(10)).Entities, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/erp/bank-reconciliation"] = new { ok = true, surface = "erp", summary = (await reporter.BuildErpBankReconciliationDigestAsync(10)).Summary, lines = (await reporter.BuildErpBankReconciliationDigestAsync(10)).Lines, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/erp/stock-transfers"] = new { ok = true, surface = "erp", summary = (await reporter.BuildErpStockTransfersDigestAsync(10)).Summary, transfers = (await reporter.BuildErpStockTransfersDigestAsync(10)).Transfers, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/erp/sales-quotations"] = new { ok = true, surface = "erp", summary = (await reporter.BuildErpSalesQuotationsDigestAsync(10)).Summary, quotations = (await reporter.BuildErpSalesQuotationsDigestAsync(10)).Quotations, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/erp/workspace-favorites"] = new { ok = true, surface = "erp", summary = (await reporter.BuildErpWorkspaceFavoritesDigestAsync(10)).Summary, favorites = (await reporter.BuildErpWorkspaceFavoritesDigestAsync(10)).Favorites, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/erp/fixed-assets"] = new { ok = true, surface = "erp", summary = (await reporter.BuildErpFixedAssetsDigestAsync(10)).Summary, assets = (await reporter.BuildErpFixedAssetsDigestAsync(10)).Assets, count = 0, source = "migration", message = "x", session, note = "contract validation" },
            ["/cp/config-items"] = Envelope("cp", "items", await reporter.ListCpConfigItemsMetaAsync(10), session),
            ["/cp/admin-sessions"] = Envelope("cp", "sessions", await reporter.ListCpAdminSessionsAsync(10), session),
            ["/cp/storages"] = Envelope("cp", "storages", await reporter.ListCpStoragesAsync(10), session),
            ["/erp/suppliers"] = Envelope("erp", "suppliers", await reporter.ListErpSuppliersAsync(10), session),
            ["/erp/purchases"] = Envelope("erp", "purchases", await reporter.ListErpPurchasesAsync(10), session),
            ["/erp/cash-accounts"] = Envelope("erp", "accounts", await reporter.ListErpCashAccountsAsync(10), session),
            ["/erp/cash-entries"] = Envelope("erp", "entries", await reporter.ListErpCashEntriesAsync(null, 10), session),
            ["/erp/coa-accounts"] = Envelope("erp", "accounts", await reporter.ListErpCoaAccountsAsync(10), session),
            ["/erp/warehouses"] = Envelope("erp", "warehouses", await reporter.ListErpWarehousesAsync(10), session),
            ["/erp/sales-orders"] = Envelope("erp", "orders", await reporter.ListErpSalesOrdersAsync(10), session),
            ["/erp/purchase-orders"] = Envelope("erp", "orders", await reporter.ListErpPurchaseOrdersAsync(10), session),
            ["/erp/invoices"] = Envelope("erp", "invoices", await reporter.ListErpInvoicesAsync(10), session),
            ["/erp/gl-journals"] = Envelope("erp", "journals", await reporter.ListErpGlJournalsAsync(10), session),
            ["/bos/tenants"] = Envelope("bos", "tenants", await reporter.ListPortalTenantsAsync(10), session),
            ["/bos/audit-log"] = Envelope("bos", "entries", await reporter.ListBosAuditLogAsync(null, 10), session),
            ["/storefront/orders"] = new
            {
                ok = true,
                surface = "storefront",
                user_id = 9,
                orders = (await reporter.ListStorefrontOrdersAsync(9, 10)).Orders,
                count = 0,
                source = "migration",
                message = "x",
                session = new { kind = "Customer", user_id = 9, email = "c@example.com", group_ids = Array.Empty<int>(), capabilities = new[] { "storefront_account" }, permissions = Array.Empty<string>() },
                note = "contract validation"
            },
            ["/storefront/garage"] = new
            {
                ok = true,
                surface = "storefront",
                user_id = 9,
                vehicles = (await reporter.ListStorefrontGarageAsync(9, 10)).Vehicles,
                count = 0,
                source = "migration",
                message = "x",
                session = new { kind = "Customer", user_id = 9, email = "c@example.com", group_ids = Array.Empty<int>(), capabilities = new[] { "storefront_account" }, permissions = Array.Empty<string>() },
                note = "contract validation"
            },
            ["/storefront/search"] = new
            {
                ok = true,
                surface = "storefront",
                article = "0986424590",
                rows = (await reporter.SearchStorefrontPartsAsync("0986424590", 10)).Rows,
                count = 0,
                source = "migration",
                message = "x",
                session = new { kind = "Customer", user_id = 9, email = "c@example.com", group_ids = Array.Empty<int>(), capabilities = new[] { "storefront_account" }, permissions = Array.Empty<string>() },
                note = "contract validation"
            },
            ["/storefront/cart"] = new
            {
                ok = true,
                surface = "storefront",
                user_id = 9,
                summary = (await reporter.ListStorefrontCartAsync(9, 10)).Summary,
                lines = (await reporter.ListStorefrontCartAsync(9, 10)).Lines,
                count = 0,
                source = "migration",
                message = "x",
                session = new { kind = "Customer", user_id = 9, email = "c@example.com", group_ids = Array.Empty<int>(), capabilities = new[] { "storefront_account" }, permissions = Array.Empty<string>() },
                note = "contract validation"
            }
        };

        foreach (var (route, envelope) in listPayloads)
        {
            var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == route);
            var json = SurfaceDigestContractValidator.SerializeEnvelope(envelope);
            var failures = SurfaceDigestContractValidator.Validate(contract, json);
            Assert.True(failures.Count == 0, $"{route}: {string.Join("; ", failures)}");
        }
    }

    [Fact]
    public void ValidatorDetectsMissingSummaryField()
    {
        var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == "/cp/dashboard-summary");
        var json = """{"ok":true,"surface":"cp","summary":{"users":1},"session":{},"note":"x"}""";
        var failures = SurfaceDigestContractValidator.Validate(contract, json);
        Assert.Contains(failures, f => f.Contains("adminSessions", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogListEnvelopesSatisfyContractsWhenDataEmpty()
    {
        var payloads = new Dictionary<string, string>
        {
            ["/api/v1/catalog/manufacturers"] = """{"ok":true,"section":"passenger","rows":0,"source":"migration","data":[],"message":"x"}""",
            ["/api/v1/catalog/models"] = """{"ok":true,"action":"models","section":"passenger","rows":0,"source":"migration","data":[],"message":"x"}""",
            ["/api/v1/catalog/modifications"] = """{"ok":true,"action":"modifications","section":"passenger","rows":0,"source":"migration","data":[],"message":"x"}""",
            ["/api/v1/catalog/brands"] = """{"ok":true,"action":"brands","section":"all","rows":0,"source":"migration","data":[],"message":"x"}""",
            ["/api/v1/catalog/suppliers"] = """{"ok":true,"action":"suppliers","section":"all","rows":0,"source":"migration","data":[],"message":"x"}"""
        };

        foreach (var (route, json) in payloads)
        {
            var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == route);
            var failures = SurfaceDigestContractValidator.Validate(contract, json);
            Assert.True(failures.Count == 0, $"{route}: {string.Join("; ", failures)}");
        }
    }

    [Fact]
    public void PriceLookupEnvelopeSatisfiesContract()
    {
        var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == "/api/v1/price/lookup");
        var json = """
            {"status":true,"brand":"TOYOTA","article":"044650K020","offers":[
              {"supplier":"fast","brand":"TOYOTA","article":"04465-0K020","name":"Brake Pad Set","price":120,"currency":"AED","stockHint":8,"leadTime":"same day"}
            ]}
            """;
        var failures = SurfaceDigestContractValidator.Validate(contract, json);
        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public void StorefrontProfileAndBosFleetHealthSatisfyContracts()
    {
        var payloads = new Dictionary<string, string>
        {
            ["/storefront/profile"] = """{"ok":true,"surface":"storefront","user_id":9,"email":"a@b.c","email_confirmed":false,"phone":"","phone_confirmed":false,"reg_variant":"email","profile_fields":{},"source":"migration","message":"x","session":{},"note":"x"}""",
            ["/bos/fleet-health"] = """{"ok":true,"surface":"bos","summary":{"portalTenants":0,"activePortalTenants":0,"adminSessions":0,"withDatabase":0,"erpOnly":0,"source":"migration","message":"x"},"sampleTenants":[],"source":"migration","message":"x","session":{},"note":"x"}"""
        };
        foreach (var (route, json) in payloads)
        {
            var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == route);
            var failures = SurfaceDigestContractValidator.Validate(contract, json);
            Assert.True(failures.Count == 0, $"{route}: {string.Join("; ", failures)}");
        }
    }

    [Fact]
    public void CatalogOfflineCacheAndVinEnvelopesSatisfyContracts()
    {
        var payloads = new Dictionary<string, string>
        {
            ["/api/v1/catalog/engines"] = """{"ok":true,"action":"engines","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/analogs"] = """{"ok":true,"action":"analogs","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/article-brands"] = """{"ok":true,"action":"brands","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/categories"] = """{"ok":true,"action":"categories","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/products"] = """{"ok":true,"action":"products","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/engine-search"] = """{"ok":true,"action":"engine_search","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/article-links"] = """{"ok":true,"action":"article_links","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/article"] = """{"ok":true,"action":"article","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/articles"] = """{"ok":true,"action":"articles","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/engine"] = """{"ok":true,"action":"engine","section":"passenger","rows":0,"source":"migration","stale":true,"data":{}}""",
            ["/api/v1/catalog/vin"] = """{"ok":true,"source":"migration","stale":true,"cached_at":null,"vin":"WBAXG1103CDW29096","language":"en","region":"WWW","vehicle_count":0,"manufacturer":"","model_label":"","payload":{}}""",
            ["/api/v1/catalog/brand-parts"] = """{"ok":true,"brand":"BOSCH","rows":0,"source":"migration","data":[],"message":"x"}"""
        };

        foreach (var (route, json) in payloads)
        {
            var contract = SurfacePayloadContractCatalog.All.Single(c => c.AspNetRoute == route);
            var failures = SurfaceDigestContractValidator.Validate(contract, json);
            Assert.True(failures.Count == 0, $"{route}: {string.Join("; ", failures)}");
        }
    }

    private static object Envelope(string surface, string listKey, object listResult, object session)
    {
        var node = JsonSerializer.SerializeToNode(
            listResult,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();

        return new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["surface"] = surface,
            [listKey] = node[listKey],
            ["count"] = node["count"]?.GetValue<int>() ?? 0,
            ["source"] = node["source"]?.GetValue<string>() ?? "migration",
            ["message"] = node["message"]?.GetValue<string>() ?? string.Empty,
            ["session"] = session,
            ["note"] = "contract validation"
        };
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }
}
