using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpApiClientsParityTests
{
    [Fact]
    public void Catalog_actions_match_the_php_allowlist()
    {
        Assert.Equal(
            new[]
            {
                "manufacturers", "models", "modifications", "categories", "products", "articles",
                "article", "analogs", "brands", "vin", "engines", "engine_search", "status",
            },
            CpApiClientsDeskService.CatalogActions);

        Assert.Equal(new[] { "catalog", "price_pro", "both" }, CpApiClientsDeskService.Products);
    }

    [Fact]
    public void Product_normalisation_falls_back_to_catalog()
    {
        Assert.Equal("catalog", CpApiClientsDeskService.NormalizeProduct("catalog"));
        Assert.Equal("price_pro", CpApiClientsDeskService.NormalizeProduct(" PRICE_PRO "));
        Assert.Equal("both", CpApiClientsDeskService.NormalizeProduct("both"));
        Assert.Equal("catalog", CpApiClientsDeskService.NormalizeProduct("premium"));
        Assert.Equal("catalog", CpApiClientsDeskService.NormalizeProduct(null));
    }

    [Fact]
    public void Daily_limit_is_clamped_like_php()
    {
        Assert.Equal(1, CpApiClientsDeskService.NormalizeDailyLimit(0));
        Assert.Equal(1, CpApiClientsDeskService.NormalizeDailyLimit(-25));
        Assert.Equal(1000, CpApiClientsDeskService.NormalizeDailyLimit(1000));
        Assert.Equal(1_000_000, CpApiClientsDeskService.NormalizeDailyLimit(9_000_000));
    }

    [Fact]
    public void Allowed_actions_parse_json_lists_and_wildcards()
    {
        Assert.Empty(CpApiClientsDeskService.ParseAllowedActions(""));
        Assert.Empty(CpApiClientsDeskService.ParseAllowedActions("*"));
        Assert.Empty(CpApiClientsDeskService.ParseAllowedActions("[]"));
        Assert.Empty(CpApiClientsDeskService.ParseAllowedActions("[not json"));
        Assert.Equal(new[] { "not", "json" }, CpApiClientsDeskService.ParseAllowedActions("{not json"));
        Assert.Equal(
            new[] { "brands", "vin" },
            CpApiClientsDeskService.ParseAllowedActions("[\"BRANDS\",\"vin\"]"));
        Assert.Equal(
            new[] { "brands", "vin" },
            CpApiClientsDeskService.ParseAllowedActions("brands, vin"));
    }

    [Fact]
    public void Scope_persistence_intersects_with_the_catalog_allowlist()
    {
        Assert.Equal("[]", CpApiClientsDeskService.AllowedActionsJson("price_pro", ["brands"]));
        Assert.Equal("[]", CpApiClientsDeskService.AllowedActionsJson("catalog", []));
        Assert.Equal("[]", CpApiClientsDeskService.AllowedActionsJson("catalog", ["drop_table"]));
        Assert.Equal(
            "[\"categories\",\"brands\"]",
            CpApiClientsDeskService.AllowedActionsJson("both", ["brands", "categories", "brands", "nope"]));
    }

    [Fact]
    public void Keys_use_php_prefixes_and_are_stored_hashed()
    {
        var catalogKey = CpApiClientWriteService.MakeKey("catalog");
        var priceKey = CpApiClientWriteService.MakeKey("price_pro");
        var bothKey = CpApiClientWriteService.MakeKey("both");

        Assert.StartsWith("epc_catalog_", catalogKey, StringComparison.Ordinal);
        Assert.StartsWith("epc_pricepro_", priceKey, StringComparison.Ordinal);
        Assert.StartsWith("epc_catalog_", bothKey, StringComparison.Ordinal);
        Assert.Equal("epc_catalog_".Length + 24, catalogKey.Length);
        Assert.NotEqual(catalogKey, CpApiClientWriteService.MakeKey("catalog"));

        var hash = CpApiClientWriteService.HashKey("epc_catalog_abc");
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, CpApiClientWriteService.HashKey("epc_catalog_abc"));
        Assert.DoesNotContain("epc_catalog_abc", hash, StringComparison.Ordinal);
        Assert.Equal(24, CpApiClientWriteService.KeyPrefix(catalogKey).Length);
        Assert.StartsWith(CpApiClientWriteService.KeyPrefix(catalogKey), catalogKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_renders_php_quota_and_scope_labels()
    {
        var all = new CpApiClientRow(7, "Acme", "dev@acme.test", "catalog", "epc_catalog_0011", 1000, 12, [], true, 0, 0);
        Assert.Equal("all", all.ScopeLabel);
        Assert.Equal("12 / 1000", all.Quota);

        var scoped = all with { AllowedActions = ["brands", "vin"], DailyLimit = 0 };
        Assert.Equal("brands, vin", scoped.ScopeLabel);
        Assert.Equal("12 / 1", scoped.Quota);
    }

    [Fact]
    public void One_time_key_vault_returns_each_key_once()
    {
        var vault = new CpApiClientKeyVault(new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var token = vault.Stash("epc_catalog_secret");
        Assert.NotEqual("epc_catalog_secret", token);
        Assert.Equal("epc_catalog_secret", vault.Take(token));
        Assert.Equal(string.Empty, vault.Take(token));
        Assert.Equal(string.Empty, vault.Take("unknown-token"));
    }

    [Fact]
    public async Task Desk_reports_unconfigured_platform_database_without_internals()
    {
        var view = await new CpApiClientsDeskService(new UnconfiguredConnections()).LoadAsync();
        Assert.Empty(view.Clients);
        Assert.Equal(0, view.ActiveCount);
        Assert.Equal("Platform database unavailable.", view.Error);
    }

    [Fact]
    public void Service_never_selects_the_stored_key_hash()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpApiClientsDeskService.cs"));
        Assert.DoesNotContain("SELECT `client_key_hash`", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY `active` DESC, `id` DESC", service, StringComparison.Ordinal);

        var schema = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpApiClientsSchema.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_api_clients`", schema, StringComparison.Ordinal);
        Assert.Contains("`client_key_hash` CHAR(64) NOT NULL", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_renders_the_php_manage_screen_and_write_endpoint_is_gated()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpApiClientsApp.razor"));
        Assert.Contains("ICpApiClientsDeskService", razor, StringComparison.Ordinal);
        Assert.Contains("Catalog &amp; Price PRO API clients", razor, StringComparison.Ordinal);
        Assert.Contains("Keys are SHA-256 hashed", razor, StringComparison.Ordinal);
        foreach (var control in new[] { "value=\"create\"", "value=\"rotate\"", "value=\"update\"", "value=\"reset_quota\"", "value=\"revoke\"", "value=\"activate\"" })
        {
            Assert.Contains(control, razor, StringComparison.Ordinal);
        }

        foreach (var column in new[] { ">Key prefix<", ">Quota<", ">Scopes<", ">Status<" })
        {
            Assert.Contains(column, razor, StringComparison.Ordinal);
        }

        Assert.Contains("/api/v1/catalog.php", razor, StringComparison.Ordinal);
        Assert.Contains("/api/v1/price/lookup.php", razor, StringComparison.Ordinal);
        Assert.Contains("allowed_actions", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("client_key_hash", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ISurfaceDashboardSummaryReporter", razor, StringComparison.Ordinal);

        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var write = module[module.IndexOf("EcomAeRoutes.CpApiClientsWrite", StringComparison.Ordinal)..];
        write = write[..write.IndexOf("MapPost", 40, StringComparison.Ordinal)];
        Assert.Contains("SuperCpHostGate.IsAllowed", write, StringComparison.Ordinal);
        Assert.Contains("LegacySessionKind.Admin", write, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", write, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpApiClientsDeskService, EcomAE.Platform.Cp.CpApiClientsDeskService", program, StringComparison.Ordinal);
        Assert.Contains("ICpApiClientKeyVault, EcomAE.Platform.Cp.CpApiClientKeyVault", program, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found");
    }
}
