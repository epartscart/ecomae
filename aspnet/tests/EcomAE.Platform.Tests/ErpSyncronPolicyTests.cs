using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Syncron inventory policy. The z table, safety stock, reorder point and exponential smoothing match goldens that
/// <c>Fixtures/SyncronPolicy/harness.py</c> records from the PR #8 PHP; the policy store, demand load, recommendation,
/// forecast run and service-level report run on a throwaway <c>ecomae_cpw_*</c> schema.
/// </summary>
public sealed class ErpSyncronPolicyTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SyncronPolicy");

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.EnumerateObject())
        {
            data.Add(testCase.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_php(string name)
    {
        var c = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.GetProperty(name);
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name);
        var daily = c.GetProperty("daily").GetDecimal();
        var lead = c.GetProperty("lead").GetInt32();
        var series = c.GetProperty("series").EnumerateArray().Select(e => e.GetDecimal()).ToList();

        var z = ErpSyncronPolicy.ServiceLevelZ(c.GetProperty("service").GetDecimal());
        var safety = ErpSyncronPolicy.SafetyStock(daily, lead, z);
        Assert.Equal(golden.GetProperty("z").GetString(), F(z));
        Assert.Equal(golden.GetProperty("safety").GetString(), F(safety));
        Assert.Equal(golden.GetProperty("rop").GetString(), F(ErpSyncronPolicy.ReorderPoint(daily, lead, safety)));
        Assert.Equal(golden.GetProperty("exponential").GetString(), F(ErpSyncronPolicy.Exponential(series, 90, c.GetProperty("alpha").GetDecimal())));
    }

    [Fact]
    public void Moving_average_counts_quiet_days_in_the_window()
    {
        decimal[] series = [9, 0, 0, 3, 0, 6];
        Assert.Equal(3m, ErpSyncronPolicy.MovingAverage(series, 6));
        Assert.Equal(6m, ErpSyncronPolicy.MovingAverage(series, 1));
        Assert.Equal(2.25m, ErpSyncronPolicy.MovingAverage(series, 4));
        Assert.Equal(0.9m, ErpSyncronPolicy.MovingAverage(series, 20));
        Assert.Equal(0m, ErpSyncronPolicy.MovingAverage([], 30));
    }

    [Fact]
    public void Item_beats_category_beats_global()
    {
        var global = Policy(1, "global", "", "All");
        var perishable = Policy(2, "category", "perishable", "Fresh");
        var item = Policy(3, "item", "OIL-5W30", "Oil");
        IReadOnlyList<ErpSyncronPolicy.Policy> all = [global, perishable, item];
        Assert.Same(item, ErpSyncronPolicy.PolicyFor(all, "oil-5w30", "perishable"));
        Assert.Same(perishable, ErpSyncronPolicy.PolicyFor(all, "MILK", "Perishable"));
        Assert.Same(global, ErpSyncronPolicy.PolicyFor(all, "BOLT", "standard"));
        Assert.Null(ErpSyncronPolicy.PolicyFor([item], "BOLT", "standard"));
    }

    [Fact]
    public void Recommends_stockout_reorder_overstock_and_order_quantities()
    {
        var demand = Enumerable.Repeat(2m, 90).ToList();
        ErpSyncronPolicy.ItemWarehouse Row(decimal onHand, IReadOnlyList<decimal> daily) => new(1, "A", "Item A", "standard", 1, "Main", onHand, daily);

        var none = ErpSyncronPolicy.Recommend(Row(0, demand), null);
        Assert.Equal(("stockout", 2m, 7), (none.Action, none.DailyDemand, none.LeadTimeDays));
        Assert.Equal(ErpSyncronPolicy.SafetyStock(2m, 7, 1.65m), none.SafetyStock);
        Assert.Equal(Math.Ceiling(none.ReorderPoint + 2m * 30m), none.SuggestedOrderQty);

        var reorder = ErpSyncronPolicy.Recommend(Row(10, demand), null);
        Assert.Equal("reorder", reorder.Action);
        Assert.Equal(Math.Ceiling(reorder.ReorderPoint + 60m - 10m), reorder.SuggestedOrderQty);

        Assert.Equal("ok", ErpSyncronPolicy.Recommend(Row(500, demand), null).Action);
        Assert.Equal("ok", ErpSyncronPolicy.Recommend(Row(0, new decimal[90]), null).Action);

        var capped = Policy(1, "global", "", "Capped") with { MaxStockQty = 40m, ReorderQty = 0m };
        Assert.Equal("overstock", ErpSyncronPolicy.Recommend(Row(41, demand), capped).Action);
        Assert.Equal(30m, ErpSyncronPolicy.Recommend(Row(10, demand), capped).SuggestedOrderQty);

        var manual = Policy(1, "global", "", "Manual") with { DemandMethod = "manual", SafetyStockQty = 5m, ReorderPoint = 12m, ReorderQty = 24m };
        var fixedRec = ErpSyncronPolicy.Recommend(Row(12, demand), manual);
        Assert.Equal(("reorder", 0m, 5m, 12m, 24m), (fixedRec.Action, fixedRec.DailyDemand, fixedRec.SafetyStock, fixedRec.ReorderPoint, fixedRec.SuggestedOrderQty));
        Assert.Equal("ok", ErpSyncronPolicy.Recommend(Row(13, demand), manual).Action);
    }

    [Theory]
    [InlineData("global", "", "Default", 95, 7, 90, 0.3, "")]
    [InlineData("region", "", "x", 95, 7, 90, 0.3, "Scope must be global, category or item.")]
    [InlineData("item", " ", "x", 95, 7, 90, 0.3, "Item policies need the item SKU.")]
    [InlineData("category", "spares", "x", 95, 7, 90, 0.3, "Category policies need an item type: standard, perishable or serialized.")]
    [InlineData("global", "", " ", 95, 7, 90, 0.3, "Policy name is required.")]
    [InlineData("global", "", "x", 100, 7, 90, 0.3, "Service level must be between 50 and 99.99 %.")]
    [InlineData("global", "", "x", 95, 400, 90, 0.3, "Lead time must be 0–365 days and the review period 1–365 days.")]
    [InlineData("global", "", "x", 95, 7, 3, 0.3, "Demand window must be 7–730 days.")]
    [InlineData("global", "", "x", 95, 7, 90, 0, "Smoothing alpha must be above 0 and at most 1.")]
    public void Validates_policies(string scope, string scopeRef, string name, double service, int lead, int window, double alpha, string expected)
    {
        var policy = Policy(0, scope, scopeRef, name) with
        {
            ServiceLevelPct = (decimal)service,
            LeadTimeDays = lead,
            DemandWindowDays = window,
            DemandAlpha = (decimal)alpha,
        };
        Assert.Equal(expected, ErpSyncronPolicy.Validate(policy));
    }

    [Fact]
    public async Task Policies_demand_forecast_and_service_levels_on_a_throwaway_schema()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            var today = new DateOnly(2026, 10, 8);
            long Day(int daysAgo) => new DateTimeOffset(today.AddDays(-daysAgo).ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero).ToUnixTimeSeconds();
            await ExecAsync(cs, """
                CREATE TABLE `epc_erp_inv_items` (`id` INT AUTO_INCREMENT PRIMARY KEY, `sku` VARCHAR(64), `name` VARCHAR(255), `item_type` VARCHAR(16), `active` TINYINT DEFAULT 1);
                CREATE TABLE `epc_erp_inv_warehouses` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255), `active` TINYINT DEFAULT 1);
                CREATE TABLE `epc_erp_inv_stock` (`id` INT AUTO_INCREMENT PRIMARY KEY, `warehouse_id` INT, `item_id` INT, `qty_on_hand` DECIMAL(14,3));
                CREATE TABLE `epc_erp_inv_movements` (`id` INT AUTO_INCREMENT PRIMARY KEY, `movement_type` VARCHAR(16), `warehouse_id` INT, `item_id` INT, `qty` DECIMAL(14,3), `movement_date` INT, `active` TINYINT DEFAULT 1);
                INSERT INTO `epc_erp_inv_items` (`id`, `sku`, `name`, `item_type`, `active`) VALUES
                  (1, 'OIL', 'Engine oil', 'standard', 1), (2, 'MILK', 'Coolant fresh', 'perishable', 1), (3, 'IDLE', 'No movement', 'standard', 1), (4, 'OLD', 'Retired', 'standard', 0);
                INSERT INTO `epc_erp_inv_warehouses` (`id`, `name`, `active`) VALUES (1, 'Main', 1), (2, 'Branch', 1), (3, 'Closed', 0);
                INSERT INTO `epc_erp_inv_stock` (`warehouse_id`, `item_id`, `qty_on_hand`) VALUES (1, 1, 5), (1, 2, 400), (2, 3, 0), (3, 1, 99), (1, 4, 1);
                """);
            var movements = new List<string>();
            for (var d = 1; d <= 90; d++)
            {
                movements.Add($"('sale_out', 1, 1, -3, {Day(d)}, 1)");
            }

            movements.Add($"('adjustment', 1, 1, -90, {Day(5)}, 1)");
            movements.Add($"('adjustment', 1, 1, 500, {Day(5)}, 1)");
            movements.Add($"('purchase_in', 1, 1, 1000, {Day(5)}, 1)");
            movements.Add($"('sale_out', 1, 1, -1000, {Day(5)}, 0)");
            movements.Add($"('sale_out', 1, 1, -1000, {Day(0)}, 1)");
            movements.Add($"('sale_out', 1, 1, -1000, {Day(200)}, 1)");
            movements.Add($"('sale_out', 2, 2, -6, {Day(10)}, 1)");
            await ExecAsync(cs, "INSERT INTO `epc_erp_inv_movements` (`movement_type`, `warehouse_id`, `item_id`, `qty`, `movement_date`, `active`) VALUES " + string.Join(",", movements));

            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            var ct = CancellationToken.None;
            await ErpSyncronPolicy.EnsureSchemaAsync(db, ct);
            await ErpSyncronPolicy.EnsureSchemaAsync(db, ct);

            var rows = await ErpSyncronPolicy.LoadItemWarehousesAsync(db, 90, 0, today, ct);
            Assert.Equal(["OIL@Main", "MILK@Branch", "MILK@Main", "IDLE@Branch"], rows.OrderBy(r => r.Sku == "OIL" ? 0 : r.Sku == "MILK" ? 1 : 2).ThenBy(r => r.WarehouseName).Select(r => r.Sku + "@" + r.WarehouseName));
            var oil = rows.Single(r => r.Sku == "OIL");
            Assert.Equal(90, oil.DailyDemand.Count);
            Assert.Equal(3m * 90 + 90m, oil.DailyDemand.Sum());
            Assert.Equal(93m, oil.DailyDemand[^5]);
            Assert.Equal(1, (await ErpSyncronPolicy.LoadItemWarehousesAsync(db, 90, 2, today, ct)).Count(r => r.Sku == "MILK"));

            var globalId = await ErpSyncronPolicy.SavePolicyAsync(db, Policy(0, "global", "ignored", "Default"), ct);
            var freshId = await ErpSyncronPolicy.SavePolicyAsync(db, Policy(0, "category", " Perishable ", "Fresh") with { MaxStockQty = 100m }, ct);
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpSyncronPolicy.SavePolicyAsync(db, Policy(0, "item", "", "Bad"), ct));
            var policies = await ErpSyncronPolicy.ListPoliciesAsync(db, ct);
            Assert.Equal([("category", "perishable", "Fresh"), ("global", "", "Default")], policies.Select(p => (p.Scope, p.ScopeRef, p.PolicyName)));

            Assert.Equal(globalId, await ErpSyncronPolicy.SavePolicyAsync(db, Policy(globalId, "global", "", "Default") with { LeadTimeDays = 10, DemandMethod = "exponential", DemandAlpha = 0.5m }, ct));
            Assert.Equal(0, await ErpSyncronPolicy.SavePolicyAsync(db, Policy(9999, "global", "", "Ghost"), ct));
            Assert.Equal((10, "exponential", 0.5m), (await ErpSyncronPolicy.ListPoliciesAsync(db, ct)).Where(p => p.Id == globalId).Select(p => (p.LeadTimeDays, p.DemandMethod, p.DemandAlpha)).Single());

            var recs = await ErpSyncronPolicy.RecommendAsync(db, 0, today, ct);
            var oilRec = recs.Single(r => r.Sku == "OIL");
            Assert.Equal(("Default", 10, "reorder"), (oilRec.PolicyName, oilRec.LeadTimeDays, oilRec.Action));
            Assert.Equal(ErpSyncronPolicy.Exponential(oil.DailyDemand, 90, 0.5m), oilRec.DailyDemand);
            var milkMain = recs.Single(r => r.Sku == "MILK" && r.WarehouseName == "Main");
            Assert.Equal(("Fresh", "overstock"), (milkMain.PolicyName, milkMain.Action));
            Assert.Equal("stockout", recs.Single(r => r.Sku == "MILK" && r.WarehouseName == "Branch").Action);
            Assert.Equal("ok", recs.Single(r => r.Sku == "IDLE").Action);

            Assert.Equal(4, await ErpSyncronPolicy.RunForecastAsync(db, 0, today, ct));
            Assert.Equal(4, await ErpSyncronPolicy.RunForecastAsync(db, 0, today, ct));
            Assert.Equal(4L, await ScalarAsync(db, "SELECT COUNT(*) FROM `epc_erp_inv_demand_forecast`"));
            var forecast = await ErpSyncronPolicy.LatestForecastAsync(db, 10, ct);
            var oilForecast = forecast.Single(f => f.Sku == "OIL");
            Assert.Equal(("2026-10-08", "2026-11-07", "exponential", decimal.Round(oilRec.DailyDemand * 30, 4)), (oilForecast.PeriodStart, oilForecast.PeriodEnd, oilForecast.Method, oilForecast.ForecastQty));

            Assert.True(await ErpSyncronPolicy.DeactivatePolicyAsync(db, globalId, ct));
            Assert.False(await ErpSyncronPolicy.DeactivatePolicyAsync(db, globalId, ct));
            Assert.Equal("", (await ErpSyncronPolicy.RecommendAsync(db, 0, today, ct)).Single(r => r.Sku == "OIL").PolicyName);

            await ErpSyncronPolicy.RecordServiceLevelAsync(db, 1, 1, "2026-09", 100m, 90m, 1, ct);
            await ErpSyncronPolicy.RecordServiceLevelAsync(db, 1, 1, "2026-09", 100m, 100m, 0, ct);
            await ErpSyncronPolicy.RecordServiceLevelAsync(db, 2, 1, "2026-10", 0m, 0m, 0, ct);
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpSyncronPolicy.RecordServiceLevelAsync(db, 1, 1, "2026-13", 1m, 1m, 0, ct));
            await Assert.ThrowsAsync<ErpWriteException>(() => ErpSyncronPolicy.RecordServiceLevelAsync(db, 1, 1, "2026-09", 1m, 2m, 0, ct));
            var report = await ErpSyncronPolicy.ServiceLevelReportAsync(db, "2026-01", "2026-12", ct);
            Assert.Equal([("2026-10", "MILK", 100m), ("2026-09", "OIL", 95m)], report.Select(r => (r.PeriodMonth, r.Sku, r.ServiceLevel)));
            Assert.Equal((200m, 190m, 1), report.Where(r => r.Sku == "OIL").Select(r => (r.DemandQty, r.FulfilledQty, r.StockoutEvents)).Single());
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    [Fact]
    public void Page_endpoint_and_nav_are_wired()
    {
        var root = RepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpSyncronApp.razor"));
        Assert.StartsWith("@page \"/erp/syncron-app\"", page, StringComparison.Ordinal);
        foreach (var action in ErpSyncronWriteService.Actions)
        {
            Assert.Contains("name=\"syncron_action\" value=\"" + action + "\"", page, StringComparison.Ordinal);
        }

        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpSyncronAction", module, StringComparison.Ordinal);
        Assert.Contains("IErpSyncronWriteService, EcomAE.Platform.Erp.ErpSyncronWriteService", File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);

        Assert.True(ErpPhpTabRouteMap.TryMapTab("syncron", out var mapped));
        Assert.Equal("/erp/syncron-app", mapped);
        var tab = Assert.Single(LegacyDesktopChromeCatalog.ErpTopnav().SelectMany(g => g.Links), t => t.Id == "inventory_mgmt/syncron");
        Assert.Equal("inventory_mgmt", tab.Group);
        Assert.DoesNotContain(PhpModuleCatalog.ErpTabs, t => t.Id == "inventory_mgmt/syncron");
    }

    [Fact]
    public async Task Write_service_applies_actions_with_audit_rows()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var name = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            await ExecAsync(cs, """
                CREATE TABLE `epc_erp_audit_log` (`id` INT AUTO_INCREMENT PRIMARY KEY, `time` INT, `admin_id` INT, `action` VARCHAR(64), `entity_type` VARCHAR(32), `entity_id` INT, `summary` VARCHAR(512), `detail_json` TEXT, `old_json` TEXT, `new_json` TEXT, `ip_address` VARCHAR(64), `user_agent` VARCHAR(255));
                CREATE TABLE `epc_erp_inv_items` (`id` INT AUTO_INCREMENT PRIMARY KEY, `sku` VARCHAR(64), `name` VARCHAR(255), `item_type` VARCHAR(16), `active` TINYINT DEFAULT 1);
                CREATE TABLE `epc_erp_inv_warehouses` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255), `active` TINYINT DEFAULT 1);
                CREATE TABLE `epc_erp_inv_stock` (`id` INT AUTO_INCREMENT PRIMARY KEY, `warehouse_id` INT, `item_id` INT, `qty_on_hand` DECIMAL(14,3));
                CREATE TABLE `epc_erp_inv_movements` (`id` INT AUTO_INCREMENT PRIMARY KEY, `movement_type` VARCHAR(16), `warehouse_id` INT, `item_id` INT, `qty` DECIMAL(14,3), `movement_date` INT, `active` TINYINT DEFAULT 1);
                INSERT INTO `epc_erp_inv_items` VALUES (1, 'OIL', 'Engine oil', 'standard', 1);
                INSERT INTO `epc_erp_inv_warehouses` VALUES (1, 'Main', 1);
                INSERT INTO `epc_erp_inv_stock` (`warehouse_id`, `item_id`, `qty_on_hand`) VALUES (1, 1, 4);
                """);
            var clock = new FixedClock(new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero));
            var service = new ErpSyncronWriteService(new Connections(cs), new ErpAuditLogWriter(), clock);
            var ct = CancellationToken.None;

            Assert.Equal("invalid", (await service.ApplyAsync(2, new ErpSyncronActionRequest("drop"), ct)).Code);
            Assert.Equal("Policy is required.", (await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_save"), ct)).Message);
            Assert.Equal("Item policies need the item SKU.", (await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_save", Policy(0, "item", "", "x")), ct)).Message);
            var created = await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_save", Policy(0, "global", "", "Default")), ct);
            Assert.Equal("Policy \"Default\" created.", created.Message);
            Assert.Equal("Policy \"Default\" updated.", (await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_save", Policy(created.Id, "global", "", "Default")), ct)).Message);
            Assert.Equal("not_found", (await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_save", Policy(999, "global", "", "Ghost")), ct)).Code);

            var run = await service.ApplyAsync(2, new ErpSyncronActionRequest("run_forecast"), ct);
            Assert.Equal(("Forecast written for 1 item × warehouse rows.", 1), (run.Message, run.Writes));
            Assert.Equal("Month must be YYYY-MM.", (await service.ApplyAsync(2, new ErpSyncronActionRequest("record_service_level", ItemId: 1, WarehouseId: 1, PeriodMonth: "Sept"), ct)).Message);
            Assert.True((await service.ApplyAsync(2, new ErpSyncronActionRequest("record_service_level", ItemId: 1, WarehouseId: 1, PeriodMonth: "2026-09", DemandQty: 10m, FulfilledQty: 8m), ct)).Succeeded);
            Assert.True((await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_deactivate", PolicyId: created.Id), ct)).Succeeded);
            Assert.Equal("not_found", (await service.ApplyAsync(2, new ErpSyncronActionRequest("policy_deactivate", PolicyId: created.Id), ct)).Code);

            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            Assert.Equal("2026-10-08", Convert.ToString(await new MySqlCommand("SELECT DATE_FORMAT(`period_start`, '%Y-%m-%d') FROM `epc_erp_inv_demand_forecast`", db).ExecuteScalarAsync(), CultureInfo.InvariantCulture));
            Assert.Equal(5L, await ScalarAsync(db, "SELECT COUNT(*) FROM `epc_erp_audit_log` WHERE `entity_type` = 'inv_policy' AND `admin_id` = 2"));
            Assert.Equal(1L, await ScalarAsync(db, "SELECT COUNT(*) FROM `epc_erp_audit_log` WHERE `action` = 'syncron_run_forecast'"));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Connections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not find repo root.");
    }

    private static ErpSyncronPolicy.Policy Policy(long id, string scope, string scopeRef, string name)
        => new(id, scope, scopeRef, name, 0m, 0m, 0m, 0m, 95m, 7, 30, "moving_avg", 90, 0.3m);

    private static string F(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static async Task<long> ScalarAsync(MySqlConnection db, string sql)
    {
        await using var command = new MySqlCommand(sql, db);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
