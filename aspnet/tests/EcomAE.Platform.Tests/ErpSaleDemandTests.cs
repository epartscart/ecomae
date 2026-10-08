using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_erp_inventory_record_sale_demand</c> against goldens recorded by <c>Fixtures/SaleDemand/harness.sh</c> (the real
/// function over the PHP inventory schema and <c>data.sql</c>): the same calls in the same order give the same movements and stock.
/// </summary>
public sealed class ErpSaleDemandTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SaleDemand");

    [Fact]
    public Task RecordSaleDemand_MatchesPhpGolden_OnThrowawayDatabase_ThenDropped()
        => WithDatabaseAsync(async (connection, _) =>
        {
            var results = new List<string>
            {
                "doc7=" + await RecordAsync(connection, 7, 40, [("Wiper", 9m)], 1767225600),
                "doc7_again=" + await RecordAsync(connection, 7, 40, [], 1767225600),
                "doc8=" + await RecordAsync(connection, 8, 0, [("Brake pad", 2m), (" Wiper ", 1.5m), ("Unknown", 1m), ("Spark plug", 1m), ("Wiper", 0m), ("Brake pad", 1m)], 1767312000),
                "doc9=" + await RecordAsync(connection, 9, 41, [("Wiper", 2m)], 1767398400),
                "doc10=" + await RecordAsync(connection, 10, 0, [("Unknown", 2m)], 1767398400),
                "doc0=" + await RecordAsync(connection, 0, 40, [], 1767398400),
            };

            Assert.Equal(File.ReadAllText(Path.Combine(FixtureDir, "results.txt")), string.Join("\n", results) + "\n");
            Assert.Equal(
                File.ReadAllText(Path.Combine(FixtureDir, "movements.tsv")),
                await DumpAsync(connection, "SELECT `movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`order_id`,`reference`,`note`,`movement_date`,`admin_id`,`active` FROM `epc_erp_inv_movements` ORDER BY `id`"));
            Assert.Equal(
                File.ReadAllText(Path.Combine(FixtureDir, "stock.tsv")),
                await DumpAsync(connection, "SELECT `warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,IFNULL(`batch_no`,'') FROM `epc_erp_inv_stock` ORDER BY `id`"));
        });

    [Fact]
    public Task SavedInvoice_RecordsTaxInvoicesOnly_FromTheirStoredLines_AndNeverThrows()
        => WithDatabaseAsync(async (connection, _) =>
        {
            await ExecAsync(connection, "CREATE TABLE epc_einvoice_documents (id INT NOT NULL PRIMARY KEY, order_id INT NOT NULL DEFAULT 0, doc_category VARCHAR(32) NOT NULL DEFAULT 'tax_invoice', invoice_type_code VARCHAR(8) NOT NULL DEFAULT '380', issue_date INT NOT NULL DEFAULT 0)");
            await ExecAsync(connection, "CREATE TABLE epc_einvoice_lines (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, document_id INT NOT NULL, line_no INT NOT NULL DEFAULT 1, item_name VARCHAR(255) NOT NULL, quantity DECIMAL(14,4) NOT NULL DEFAULT 0)");
            await ExecAsync(connection, "INSERT INTO epc_einvoice_documents (id, order_id, doc_category, invoice_type_code, issue_date) VALUES (20, 0, 'tax_invoice', '380', 1767225600), (21, 0, 'tax_credit_note', '381', 1767225600), (22, 0, 'tax_invoice', '381', 1767225600)");
            await ExecAsync(connection, "INSERT INTO epc_einvoice_lines (document_id, line_no, item_name, quantity) VALUES (20, 2, 'Brake pad', 1), (20, 1, 'Wiper', 3), (21, 1, 'Wiper', 1), (22, 1, 'Wiper', 1)");

            Assert.Equal(2, await ErpSaleDemand.RecordForSavedInvoiceAsync(connection, 20, 4, CancellationToken.None));
            Assert.Equal(0, await ErpSaleDemand.RecordForSavedInvoiceAsync(connection, 21, 4, CancellationToken.None));
            Assert.Equal(0, await ErpSaleDemand.RecordForSavedInvoiceAsync(connection, 22, 4, CancellationToken.None));
            Assert.Equal(0, await ErpSaleDemand.RecordForSavedInvoiceAsync(connection, 99, 4, CancellationToken.None));
            Assert.Equal(
                "sale_out\t1\t4\t3.000\tSALEINV-20\t1767225600\t4\nsale_out\t1\t2\t1.000\tSALEINV-20\t1767225600\t4\n",
                await DumpAsync(connection, "SELECT `movement_type`,`warehouse_id`,`item_id`,`qty`,`reference`,`movement_date`,`admin_id` FROM `epc_erp_inv_movements` ORDER BY `id`"));

            await ExecAsync(connection, "DROP TABLE epc_erp_inv_movements");
            await ExecAsync(connection, "INSERT INTO epc_einvoice_documents (id) VALUES (23)");
            await ExecAsync(connection, "INSERT INTO epc_einvoice_lines (document_id, item_name, quantity) VALUES (23, 'Wiper', 1)");
            Assert.Equal(0, await ErpSaleDemand.RecordForSavedInvoiceAsync(connection, 23, 4, CancellationToken.None));
        });

    [Fact]
    public Task ManualInvoiceSave_RecordsTheSaleDemandAfterCommit()
        => WithDatabaseAsync(async (_, cs) =>
        {
            var service = new ErpManualInvoiceWriteService(new Connections(cs), StorefrontOrderPrintTests.Stub<IErpVoucherNumberService>.Create());
            var saved = await service.SaveAsync(new ErpManualInvoiceWriteRequest(
                0,
                0,
                "SI-DEMAND-1",
                "AED",
                null,
                null,
                "[{\"ItemName\":\"Oil filter\",\"Quantity\":2,\"UnitPrice\":10}]",
                null,
                null,
                null,
                0,
                0m,
                null,
                null,
                null,
                6));

            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            Assert.Equal(
                "sale_out\t2\t1\t2.000\t4.5000\t9.00\tSALEINV-" + saved.InvoiceId.ToString(CultureInfo.InvariantCulture) + "\t6\n",
                await DumpAsync(connection, "SELECT `movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`reference`,`admin_id` FROM `epc_erp_inv_movements` ORDER BY `id`"));
            Assert.Equal("8.000", await DumpScalarAsync(connection, "SELECT `qty_on_hand` FROM `epc_erp_inv_stock` WHERE `warehouse_id` = 2 AND `item_id` = 1"));
        });

    [Fact]
    public async Task OrderTaxInvoicePrint_RecordsTheOrdersSaleDemand_ThroughTheInvoiceService()
    {
        var orderPrintFixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OrderPrint", "fixture.sql"));
        await WithDatabaseAsync(
            async (connection, cs) =>
            {
                await ExecAsync(connection, "ALTER TABLE shop_orders_items ADD `product_id` INT NOT NULL DEFAULT 0");
                await ExecAsync(connection, "UPDATE shop_orders_items SET product_id = 101 WHERE id IN (90, 92)");
                await ExecAsync(connection, "INSERT INTO epc_erp_inv_warehouses (id, code, name, active) VALUES (2, 'DXB', 'Dubai', 1)");
                await ExecAsync(connection, "INSERT INTO epc_erp_inv_items (id, sku, name, product_id, active) VALUES (1, 'OF-1', 'Oil filter', 101, 1)");
                await ExecAsync(connection, "INSERT INTO epc_erp_inv_stock (warehouse_id, item_id, qty_on_hand, avg_unit_cost, time_updated) VALUES (2, 1, 10.000, 4.5000, 1)");

                var docRoot = Path.Combine(Path.GetTempPath(), "ecomae-sd-print-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(docRoot);
                File.WriteAllText(Path.Combine(docRoot, "config.php"), "<?php\nclass DP_Config {\npublic $shop_currency = 'AED';\n}\n");
                try
                {
                    await using var host = await StorefrontOrderPrintTests.StartAsync(cs, docRoot);
                    using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = host.BaseAddress };
                    using var request = new HttpRequestMessage(HttpMethod.Get, "content/shop/print_docs/service/print.php?doc_name=uae_tax_invoice&order_id=40&csrf_guard_key=adm-key");
                    request.Headers.Add("Cookie", "admin_session=adm-sess; admin_u_id=1");
                    using var response = await client.SendAsync(request);
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                }
                finally
                {
                    Directory.Delete(docRoot, true);
                }

                var invoiceId = await DumpScalarAsync(connection, "SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = 40");
                Assert.Equal(
                    "sale_out\t2\t1\t3.000\t4.5000\t13.50\t40\tSALEINV-" + invoiceId + "\n",
                    await DumpAsync(connection, "SELECT `movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`order_id`,`reference` FROM `epc_erp_inv_movements` ORDER BY `id`"));
                Assert.Equal("7.000", await DumpScalarAsync(connection, "SELECT `qty_on_hand` FROM `epc_erp_inv_stock` WHERE `item_id` = 1"));
            },
            orderPrintFixture + InventorySchema());
    }

    /// <summary>The PHP inventory tables of <c>Fixtures/SaleDemand/fixture.sql</c>, without its data.</summary>
    private static string InventorySchema()
        => string.Join(
            ";\n",
            File.ReadAllText(Path.Combine(FixtureDir, "fixture.sql"))
                .Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(statement => statement.StartsWith("CREATE TABLE `epc_erp_", StringComparison.Ordinal))) + ";\n";

    private static Task<int> RecordAsync(DbConnection connection, long docId, long orderId, (string, decimal)[] lines, long when)
        => ErpSaleDemand.RecordAsync(connection, docId, orderId, lines, when, 9, CancellationToken.None);

    private static async Task<string> DumpAsync(DbConnection connection, string sql)
    {
        var lines = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var cells = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                cells[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i) switch
                {
                    bool b => b ? "1" : "0",
                    var v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
                };
            }

            lines.Add(string.Join('\t', cells));
        }

        return string.Join('\n', lines) + "\n";
    }

    private static async Task<string> DumpScalarAsync(DbConnection connection, string sql)
        => (await DumpAsync(connection, sql)).TrimEnd('\n');

    private static async Task ExecAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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

    private static async Task WithDatabaseAsync(Func<DbConnection, string, Task> run, string? fixture = null)
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";";
        await using (var adminConnection = new MySqlConnection(admin))
        {
            await adminConnection.OpenAsync();
            await using var create = adminConnection.CreateCommand();
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            foreach (var statement in (fixture ?? File.ReadAllText(Path.Combine(FixtureDir, "fixture.sql"))).Split(";\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await ExecAsync(connection, statement);
            }

            await run(connection, cs);
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await using var adminConnection = new MySqlConnection(admin);
            await adminConnection.OpenAsync();
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
