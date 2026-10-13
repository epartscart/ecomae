using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Company 2 (Jewellery Division) writes PHP voucher codes into a throwaway schema.
/// </summary>
public sealed class ErpJwCompanyTwoWriteTests
{
    [Fact]
    public async Task CompanyTwoJewelleryWritesUsePhpVoucherCodesAndReadBack()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var name = "ecomae_jw2_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + name + ";User ID=ecomae;Password=" + password + ";";
        await ExecAsync(admin, "CREATE DATABASE `" + name + "`");
        try
        {
            var connections = new Connections(cs);
            var writes = new ErpJwVoucherWriteService(connections);
            var purchases = new ErpJwPurchaseHistoryReadService(connections);
            var sales = new ErpJwSaleHistoryReadService(connections);

            var metal = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_metal_purchase_save",
                PartyName: "Company 2 gold supplier",
                PartyCode: "SUP-JW2",
                Lines:
                [
                    new ErpJwVoucherLineSaveRequest(
                        StockCode: "22K-CO2",
                        Description: "22K bar",
                        Division: "G",
                        Pcs: 1,
                        GrossWeight: 10m,
                        Purity: 0.917m,
                        MetalRate: 250m)
                ]));
            Assert.True(metal.Succeeded, metal.Message);
            Assert.Contains("RMP voucher 1 saved", metal.Message, StringComparison.Ordinal);

            var diamond = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_diamond_purchase_save",
                PartyName: "Company 2 diamond supplier",
                Lines: [new ErpJwVoucherLineSaveRequest(StockCode: "DIA-CO2", Description: "1ct", Division: "D", Pcs: 1)]));
            Assert.True(diamond.Succeeded, diamond.Message);

            var sale = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_retail_sale_save",
                CustomerName: "Walk-in JW2",
                Lines: [new ErpJwVoucherLineSaveRequest(StockCode: "RING-CO2", Description: "Ring", Division: "G", Pcs: 1)]));
            Assert.True(sale.Succeeded, sale.Message);

            var manufacture = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_manufacture_save",
                PartyName: "Workshop HO",
                Lines:
                [
                    new ErpJwVoucherLineSaveRequest(
                        StockCode: "MFG-CO2",
                        Description: "Handmade bangle",
                        Division: "G",
                        Pcs: 1,
                        GrossWeight: 8.5m,
                        Purity: 0.917m,
                        WastagePercent: 2m)
                ]));
            Assert.True(manufacture.Succeeded, manufacture.Message);

            var transfer = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_branch_transfer_save",
                Branch: "HO",
                PartyCode: "BR2",
                Lines: [new ErpJwVoucherLineSaveRequest(StockCode: "22K-CO2", Description: "22K bar", Pcs: 1, GrossWeight: 10m)]));
            Assert.True(transfer.Succeeded, transfer.Message);

            var metalHistory = await purchases.ReadAsync(2, "METAL");
            Assert.Contains(metalHistory.Rows, row => row.VoucherType == "RMP" && row.VoucherNumber == 1 && row.PartyName == "Company 2 gold supplier");

            var diamondHistory = await purchases.ReadAsync(2, "DIAMOND");
            Assert.Contains(diamondHistory.Rows, row => row.VoucherType == "RDP" && row.VoucherNumber == 1);

            var manufactureHistory = await purchases.ReadAsync(2, "MANUFACTURE");
            Assert.Contains(manufactureHistory.Rows, row => row.VoucherType == "MFG" && row.VoucherNumber == 1);

            var transferHistory = await purchases.ReadAsync(2, "TRANSFER");
            Assert.Contains(transferHistory.Rows, row => row.VoucherType == "BTR" && row.VoucherNumber == 1 && row.PartyCode == "BR2");

            var saleHistory = await sales.ReadAsync(2, "RETAIL");
            Assert.Contains(saleHistory.Rows, row => row.VoucherType == "RIN" && row.VoucherNumber == 1);

            var metalAgain = await writes.SaveAsync(new ErpJwVoucherSaveRequest(
                CompanyId: 2,
                Action: "jw_metal_purchase_save",
                PartyCode: "SUP-JW2B",
                PartyName: "Company 2 gold supplier 2",
                Lines: [new ErpJwVoucherLineSaveRequest(StockCode: "22K-CO2B", Description: "22K bar 2", Division: "G", Pcs: 1)]));
            Assert.True(metalAgain.Succeeded, metalAgain.Message);
            Assert.Contains("RMP voucher 2 saved", metalAgain.Message, StringComparison.Ordinal);
            metalHistory = await purchases.ReadAsync(2, "METAL");
            Assert.Contains(metalHistory.Rows, row => row.VoucherType == "RMP" && row.VoucherNumber == 2 && row.PartyName == "Company 2 gold supplier 2");

            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            await using var command = new MySqlCommand(
                "SELECT `voc_type`, COUNT(*) FROM `epc_jewel_voucher` WHERE `company_id`=2 GROUP BY `voc_type` ORDER BY `voc_type`",
                db);
            await using var reader = await command.ExecuteReaderAsync();
            var types = new Dictionary<string, long>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
            {
                types[reader.GetString(0)] = reader.GetInt64(1);
            }

            Assert.Equal(2, types["RMP"]);
            Assert.Equal(1, types["RDP"]);
            Assert.Equal(1, types["RIN"]);
            Assert.Equal(1, types["MFG"]);
            Assert.Equal(1, types["BTR"]);
            Assert.False(types.ContainsKey("MMP"));
            Assert.False(types.ContainsKey("DMP"));
            Assert.False(types.ContainsKey("RSI"));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + name + "`");
        }
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

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
