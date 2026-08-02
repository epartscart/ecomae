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
            ["/cp/users"] = Envelope("cp", "users", await reporter.ListCpUsersAsync(10), session),
            ["/cp/groups"] = Envelope("cp", "groups", await reporter.ListCpGroupsAsync(10), session),
            ["/cp/modules"] = Envelope("cp", "modules", await reporter.ListCpModulesAsync(10), session),
            ["/cp/menus"] = Envelope("cp", "menus", await reporter.ListCpMenusAsync(10), session),
            ["/cp/pages"] = Envelope("cp", "pages", await reporter.ListCpPagesAsync(10), session),
            ["/cp/currencies"] = Envelope("cp", "currencies", await reporter.ListCpCurrenciesAsync(10), session),
            ["/cp/api-clients"] = Envelope("cp", "clients", await reporter.ListCpApiClientsMetaAsync(10), session),
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
            ["/api/v1/catalog/brands"] = """{"ok":true,"action":"brands","section":"all","rows":0,"source":"migration","data":[],"message":"x"}"""
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
