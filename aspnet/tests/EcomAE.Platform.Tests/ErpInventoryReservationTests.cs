using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpInventoryReservationTests
{
    [Fact]
    public void AtpIsOnHandMinusReservedNeverNegative()
    {
        Assert.Equal(7m, ErpInventoryReservationWriteService.Available(10m, 3m));
        Assert.Equal(0m, ErpInventoryReservationWriteService.Available(2m, 5m));
        Assert.Equal(0m, ErpInventoryReservationWriteService.Available(0m, 0m));
    }

    [Theory]
    [InlineData("held", 0, 1, true)]
    [InlineData("confirmed", 0, 1, true)]
    [InlineData("held", 50, 10, true)]
    [InlineData("held", 10, 50, false)]
    [InlineData("consumed", 0, 1, false)]
    [InlineData("released", 0, 1, false)]
    [InlineData("expired", 0, 1, false)]
    public void OpenHoldsCountAgainstAtpUntilTheyExpire(string status, long expiresAt, long now, bool expected)
        => Assert.Equal(expected, ErpInventoryReservationWriteService.CountsAgainstAtp(status, expiresAt, now));

    [Fact]
    public void SoftHoldLastsThirtyMinutes()
        => Assert.Equal(1800, ErpInventoryReservationWriteService.HoldSeconds);

    [Fact]
    public void LockNameIsTenantScopedAndFitsMysqlLimit()
    {
        var name = ErpInventoryReservationWriteService.LockName("ecomae_very_long_tenant_schema_name", 42);
        Assert.StartsWith("ecomae_very_long_tenant_sche", name, StringComparison.Ordinal);
        Assert.Contains("erp_inv_rsv_42", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 64);
    }

    [Fact]
    public void FirstClickLostMessageNamesTheSkuAndRemainingQty()
    {
        var message = ErpInventoryReservationWriteService.FirstClickLostMessage("BK1", 0);
        Assert.Contains("BK1", message, StringComparison.Ordinal);
        Assert.Contains("Another sales user", message, StringComparison.Ordinal);
        Assert.Contains("0", message, StringComparison.Ordinal);
    }

    [Fact]
    public void QuoteAndSalesOrderAreTheOnlySources()
    {
        Assert.Equal("quote", ErpInventoryReservationWriteService.NormalizeSource("quote"));
        Assert.Equal("sales_order", ErpInventoryReservationWriteService.NormalizeSource("sales_order"));
        Assert.Equal("sales_order", ErpInventoryReservationWriteService.NormalizeSource("cart"));
    }

    [Fact]
    public void RoutesAndCatalogAreLiveGated()
    {
        Assert.Equal("/erp/ajax/item-reserve", EcomAeRoutes.ErpAjaxItemReserve);
        Assert.Equal("/erp/ajax/item-release", EcomAeRoutes.ErpAjaxItemRelease);
        Assert.Equal("/erp/ajax/item-atp", EcomAeRoutes.ErpAjaxItemAtp);
        Assert.Contains(SurfacePayloadContractCatalog.Functions, row =>
            row.AspNetRouteOrCapability == "/erp/ajax/item-reserve" && row.Status == "write-live-gated");
        Assert.Contains(SurfacePayloadContractCatalog.Functions, row =>
            row.AspNetRouteOrCapability == "/erp/ajax/item-release");
        Assert.Contains(SurfacePayloadContractCatalog.Functions, row =>
            row.AspNetRouteOrCapability == "/erp/ajax/item-atp");
    }

    [Fact]
    public void DryRunDoesNotWrite()
    {
        var result = new ErpItemReserveDryRun().Evaluate(new ErpItemReserveRequest(12, "BK1", 1m, "sales_order", 0, false));
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.True(result.WouldWrite);
        Assert.Equal("ok", result.ValidationCode);
    }

    [Fact]
    public void SalesOrderPickerReservesOnFirstClick()
    {
        var js = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/wwwroot/erp/erp-sales-orders.js"));
        Assert.Contains("/erp/ajax/item-reserve", js, StringComparison.Ordinal);
        Assert.Contains("sourceType: 'sales_order'", js, StringComparison.Ordinal);
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("available", page, StringComparison.Ordinal);
        Assert.Contains("First click", page, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryStockShowsReservedAndAvailable()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryStockApp.razor"));
        Assert.Contains("Reserved", page, StringComparison.Ordinal);
        Assert.Contains("Available", page, StringComparison.Ordinal);
        Assert.Contains("Open sales holds", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconfiguredReserveFailsClosed()
    {
        var service = new ErpInventoryReservationWriteService(new UnconfiguredConnections());
        var result = await service.ReserveAsync(new ErpReserveInput { ItemId = 1, Qty = 1m, AdminId = 7 });
        Assert.False(result.Succeeded);
        Assert.Equal("db", result.Code);
        Assert.Equal(0, result.ReservationId);
    }

    [Fact]
    public async Task FirstClickWinsWhenTwoUsersRaceTheLastUnit()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_rsv_" + Guid.NewGuid().ToString("N")[..12];
        var adminCs = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await Exec(adminCs, "CREATE DATABASE `" + database + "` CHARACTER SET utf8 COLLATE utf8_general_ci");
        try
        {
            await Exec(cs, "CREATE TABLE `epc_erp_inv_items` (`id` int NOT NULL AUTO_INCREMENT, `sku` varchar(64) NOT NULL, `name` varchar(255) NOT NULL, `active` tinyint NOT NULL DEFAULT 1, PRIMARY KEY (`id`))");
            await Exec(cs, "CREATE TABLE `epc_erp_inv_stock` (`id` int NOT NULL AUTO_INCREMENT, `warehouse_id` int NOT NULL, `item_id` int NOT NULL, `qty_on_hand` decimal(14,3) NOT NULL, `avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Exec(cs, "INSERT INTO `epc_erp_inv_items` (`sku`,`name`,`active`) VALUES ('BK1','Brake kit',1)");
            await Exec(cs, "INSERT INTO `epc_erp_inv_stock` (`warehouse_id`,`item_id`,`qty_on_hand`) VALUES (1,1,1.000)");

            var service = new ErpInventoryReservationWriteService(new ThrowawayConnections(cs));
            var first = await service.ReserveAsync(new ErpReserveInput
            {
                ItemId = 1,
                Sku = "BK1",
                Qty = 1m,
                SourceType = "sales_order",
                SourceId = 0,
                SourceLineKey = "user-a",
                AdminId = 11,
            });
            var second = await service.ReserveAsync(new ErpReserveInput
            {
                ItemId = 1,
                Sku = "BK1",
                Qty = 1m,
                SourceType = "sales_order",
                SourceId = 0,
                SourceLineKey = "user-b",
                AdminId = 22,
            });

            Assert.True(first.Succeeded);
            Assert.Equal("held", first.Status);
            Assert.False(second.Succeeded);
            Assert.Equal("unavailable", second.Code);
            Assert.Contains("Another sales user", second.Message, StringComparison.Ordinal);
            Assert.Equal(0m, second.Available);

            var atp = await service.AtpAsync(1, "BK1");
            Assert.Equal(1m, atp.OnHand);
            Assert.Equal(1m, atp.Reserved);
            Assert.Equal(0m, atp.Available);
        }
        finally
        {
            await Exec(adminCs, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("must not open");
    }

    private sealed class ThrowawayConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public ThrowawayConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private static async Task Exec(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "aspnet", "src")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
