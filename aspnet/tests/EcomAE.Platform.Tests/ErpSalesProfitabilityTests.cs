using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSalesProfitabilityTests
{
    [Fact]
    public void Margin_uses_quantity_times_average_unit_cost()
    {
        var line = ErpSalesProfitability.Make("2026-03-15", "  Gulf Parts ", " SKU-1 ", "Filter", 2m, 100m, 30m);
        Assert.Equal("Gulf Parts", line.Customer);
        Assert.Equal("SKU-1", line.ItemCode);
        Assert.Equal(60m, line.Cost);
        Assert.Equal(40m, line.Margin);
        Assert.Equal(40m, line.MarginPct);
        Assert.Equal(0m, ErpSalesProfitability.MarginPercent(0m, 10m));

        var loss = ErpSalesProfitability.Make("2026-03-15", "Gulf Parts", "SKU-2", "Hose", 4m, 10m, 10m);
        Assert.Equal(40m, loss.Cost);
        Assert.Equal(-30m, loss.Margin);
        Assert.Equal(-300m, loss.MarginPct);
    }

    [Fact]
    public void Summarize_groups_customer_item_and_month_and_filters()
    {
        var lines = new[]
        {
            ErpSalesProfitability.Make("2026-01-10", "Alpha", "A1", "Bolt", 1m, 80m, 20m),
            ErpSalesProfitability.Make("2026-01-20", "Alpha", "A1", "Bolt", 1m, 40m, 10m),
            ErpSalesProfitability.Make("2026-02-02", "Beta", "B2", "Nut", 3m, 30m, 5m),
            ErpSalesProfitability.Make("2026-02-02", "Gamma", "C3", "Washer", 1m, 10m, 1m),
        };

        var filtered = ErpSalesProfitability.Filter(lines, "alp", "bolt");
        Assert.Equal(2, filtered.Count);

        var report = ErpSalesProfitability.Summarize(lines);
        Assert.Equal(160m, report.Revenue);
        Assert.Equal(46m, report.Cost);
        Assert.Equal(114m, report.Margin);
        Assert.Equal(71.25m, report.MarginPct);
        Assert.Equal("Alpha", report.ByCustomer[0].Label);
        Assert.Equal(120m, report.ByCustomer[0].Revenue);
        Assert.Equal(90m, report.ByCustomer[0].Margin);
        Assert.Equal("A1 Bolt", report.ByItem[0].Label);
        Assert.Equal(new[] { "2026-01", "2026-02" }, report.ByMonth.Select(group => group.Key).ToArray());

        var many = Enumerable.Range(1, 20)
            .Select(n => ErpSalesProfitability.Make("2026-04-01", "C" + n.ToString("00"), "I" + n, "Part", 1m, n, 0m))
            .ToList();
        Assert.Equal(15, ErpSalesProfitability.Summarize(many).ByCustomer.Count);
        Assert.Equal("C20", ErpSalesProfitability.Summarize(many).ByCustomer[0].Label);
    }

    [Fact]
    public void Workspace_and_dataset_open_the_same_profitability_view()
    {
        Assert.True(ErpSalesProfitability.IsProfitabilityTab("sm_performance"));
        Assert.True(ErpSalesProfitability.IsProfitabilityTab("CM_SALES_PROFITABILITY"));
        Assert.False(ErpSalesProfitability.IsProfitabilityTab("sm_turnover"));

        Assert.True(ErpD365Part2Catalog.TryGet(ErpSalesProfitability.Tab, out var sales));
        Assert.Equal("Sales profitability performance", sales.Label);
        Assert.Equal("/erp/sales-workspace-app?tab=sm_performance", ErpD365Part2Catalog.TryMapTab(ErpSalesProfitability.Tab, out var salesHref) ? salesHref : "");

        Assert.True(ErpD365Part1Catalog.TryGet(ErpSalesProfitability.CostTab, out var cost));
        Assert.Equal("Sales profitability performance", cost.Label);
        Assert.Equal("/erp/sales-workspace-app?tab=sm_performance", cost.ExistingHref);
        Assert.Equal("/erp/sales-workspace-app?tab=sm_performance", ErpPhpTabRouteMap.TryMapTab(ErpSalesProfitability.CostTab, out var mapped) ? mapped : "");

        Assert.Contains(PowerBiCatalog.Datasets, dataset => dataset.Id == ErpSalesProfitability.DatasetId);
        Assert.True(EpcPublicApiV1.IsKeyedRoute("powerbi/sales-profitability"));
        Assert.True(EpcPublicApiV1.IsPowerBiRoute("powerbi/sales-profitability"));

        var root = FindRepoRoot();
        var workspace = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpSalesProfitabilityPanel.razor"));
        var catalogJson = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/EpcPublicApiEndpoint.cs"));
        Assert.Contains("ErpSalesProfitabilityPanel", workspace, StringComparison.Ordinal);
        Assert.Contains("name=\"country\"", panel, StringComparison.Ordinal);
        Assert.Contains("name=\"detail\"", panel, StringComparison.Ordinal);
        Assert.Contains("Sales profitability performance", panel, StringComparison.Ordinal);
        Assert.Contains("/sales-profitability?format=csv", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", panel, StringComparison.Ordinal);
        Assert.Contains("Dataset(\"sales-profitability\"", catalogJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_uses_posted_sales_and_average_stock_cost()
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
            var inside = new DateTimeOffset(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var outside = new DateTimeOffset(new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
            await ExecAsync(cs, $"""
                CREATE TABLE `users` (`user_id` INT PRIMARY KEY, `email` VARCHAR(255));
                CREATE TABLE `users_profiles` (`user_id` INT, `data_key` VARCHAR(64), `data_value` VARCHAR(64));
                CREATE TABLE `epc_erp_inv_items` (`id` INT PRIMARY KEY, `sku` VARCHAR(64));
                CREATE TABLE `epc_erp_inv_stock` (`id` INT PRIMARY KEY, `item_id` INT, `avg_unit_cost` DECIMAL(14,4));
                CREATE TABLE `epc_erp_sales_orders` (
                  `id` INT AUTO_INCREMENT PRIMARY KEY, `so_no` VARCHAR(32), `customer_user_id` INT, `status` VARCHAR(16), `time_created` INT);
                CREATE TABLE `epc_erp_sales_order_lines` (
                  `id` INT AUTO_INCREMENT PRIMARY KEY, `sales_order_id` INT, `line_no` INT, `item_code` VARCHAR(32),
                  `description` VARCHAR(255), `qty` DECIMAL(14,3), `line_ex_vat` DECIMAL(14,2));
                INSERT INTO `users` (`user_id`, `email`) VALUES (7, 'alpha@example.com');
                INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (7, 'epc_reg_country', 'UAE');
                INSERT INTO `epc_erp_inv_items` (`id`, `sku`) VALUES (1, 'SKU-1');
                INSERT INTO `epc_erp_inv_stock` (`id`, `item_id`, `avg_unit_cost`) VALUES (1, 1, 30), (2, 1, 10);
                INSERT INTO `epc_erp_sales_orders` (`id`, `so_no`, `customer_user_id`, `status`, `time_created`) VALUES
                  (1, 'SO-1', 7, 'confirmed', {inside}),
                  (2, 'SO-2', 7, 'draft', {inside}),
                  (3, 'SO-3', 8, 'invoiced', {outside}),
                  (4, 'SO-4', 0, 'cancelled', {inside});
                INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`, `line_no`, `item_code`, `description`, `qty`, `line_ex_vat`) VALUES
                  (1, 1, 'sku-1', 'Filter', 2, 100),
                  (2, 1, 'sku-1', 'Draft', 9, 900),
                  (3, 1, 'sku-1', 'Later', 1, 50),
                  (4, 1, 'sku-1', 'Cancelled', 1, 20);
                """);

            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            var report = await ErpSalesProfitability.LoadAsync(
                db, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), null, null, CancellationToken.None);
            Assert.Equal(1, report.LineCount);
            Assert.Equal(100m, report.Revenue);
            Assert.Equal(40m, report.Cost);
            Assert.Equal(60m, report.Margin);
            Assert.Equal(60m, report.MarginPct);
            Assert.Equal("alpha@example.com", report.Lines[0].Customer);
            Assert.Equal(20m, report.Lines[0].UnitCost);

            var filtered = await ErpSalesProfitability.LoadAsync(
                db, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), "nope", "sku-1", CancellationToken.None);
            Assert.Equal(0, filtered.LineCount);

            var uae = await ErpSalesProfitability.LoadAsync(
                db, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), null, null, CancellationToken.None, "AE");
            Assert.Equal(1, uae.LineCount);
            Assert.Contains("United Arab Emirates", uae.Note, StringComparison.Ordinal);

            var saudi = await ErpSalesProfitability.LoadAsync(
                db, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), null, null, CancellationToken.None, "SA");
            Assert.Equal(0, saudi.LineCount);
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
