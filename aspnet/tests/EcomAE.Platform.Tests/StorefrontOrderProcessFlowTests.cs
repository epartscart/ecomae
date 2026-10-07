using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>epc_erp_order_fulfillment_bootstrap()</c> → <c>epc_pf_sync_order_case</c> / <c>epc_pf_sync_po_case</c>
/// and <c>epc_erp_po_set_status()</c> → <c>epc_erp_po_pf_sync</c>: checkout and later PO moves keep the
/// Process flow "Customer Order → Delivery" and "Procurement → Goods receipt" cases in step.
/// </summary>
public sealed class StorefrontOrderProcessFlowTests
{
    private static readonly string[] ExtraSchema =
    [
        "ALTER TABLE shop_orders_statuses_ref ADD for_finish TINYINT NOT NULL DEFAULT 0, ADD `order` INT NOT NULL DEFAULT 0",
        "ALTER TABLE shop_orders_items_statuses_ref ADD for_finish TINYINT NOT NULL DEFAULT 0, ADD `order` INT NOT NULL DEFAULT 0",
        "CREATE TABLE epc_erp_purchases (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, invoice_number VARCHAR(64) NOT NULL DEFAULT '', supplier_id INT NOT NULL DEFAULT 0, total_amount DECIMAL(14,2) NOT NULL DEFAULT 0, status VARCHAR(16) NOT NULL DEFAULT 'posted', gl_journal_id INT NOT NULL DEFAULT 0, po_id INT NOT NULL DEFAULT 0, order_id INT NOT NULL DEFAULT 0, active TINYINT NOT NULL DEFAULT 1)",
        "CREATE TABLE epc_einvoice_documents (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, invoice_number VARCHAR(64) NOT NULL DEFAULT '', validation_ok TINYINT NOT NULL DEFAULT 0, order_id INT NOT NULL DEFAULT 0)",
        "CREATE TABLE epc_pf_dept_heads (department_code VARCHAR(32) NOT NULL PRIMARY KEY, head_user_id INT NOT NULL DEFAULT 0, time_updated INT NOT NULL DEFAULT 0)",
    ];

    [Fact]
    public async Task Checkout_StartsOrderAndPoCases_PoMovesAdvanceThem_OnThrowawayDatabase_ThenDropped()
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

        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";";
        Assert.DoesNotContain("Database=docpart", cs, StringComparison.OrdinalIgnoreCase);
        try
        {
            foreach (var ddl in StorefrontOrderSupplierPoTests.Schema.Concat(ExtraSchema))
            {
                await ExecuteAsync(cs, ddl);
            }

            await ExecuteAsync(cs, """
                INSERT INTO shop_storages (id, name, interface_type, connection_options, currency) VALUES
                (10, 'Own Warehouse Dubai', 1, '{}', 'AED'),
                (20, 'Supplier A Prices', 2, '{}', 'AED')
                """);
            await ExecuteAsync(cs, "INSERT INTO epc_erp_suppliers (storage_id, name) VALUES (20, 'Supplier A LLC')");
            await ExecuteAsync(cs, "INSERT INTO shop_obtaining_modes (id) VALUES (2)");
            await ExecuteAsync(cs, "INSERT INTO shop_orders_statuses_ref (id, for_created) VALUES (1, 1)");
            await ExecuteAsync(cs, "INSERT INTO shop_orders_items_statuses_ref (id, for_created) VALUES (1, 1)");
            await ExecuteAsync(cs, "INSERT INTO shop_currencies (iso_code, rate) VALUES ('AED', 1)");
            // No purchase head: PHP routes those steps to the initiator, else epc_pf_user_id().
            await ExecuteAsync(cs, "INSERT INTO epc_pf_dept_heads (department_code, head_user_id) VALUES ('sales', 101), ('finance', 102), ('logistics', 104), ('accounts', 105)");
            await ExecuteAsync(cs, """
                INSERT INTO shop_carts (user_id, session_id, checked_for_order, product_type, product_id, price, count_need,
                  t2_manufacturer, t2_article, t2_article_show, t2_name, t2_price_purchase, t2_storage_id, t2_json_params) VALUES
                (5, 0, 1, 2, 0, 120, 2, 'BOSCH', '0986', '0 986', 'Brake pad', 80, 10, ''),
                (5, 0, 1, 2, 0, 50, 1, 'MANN', 'W712', 'W 712', 'Oil filter', 30, 20, '')
                """);

            await using var provider = Services(cs);
            await using var scope = provider.CreateAsyncScope();
            var checkout = scope.ServiceProvider.GetRequiredService<IStorefrontCheckoutWriteService>();
            var result = await checkout.CreateAsync(5, new StorefrontCheckoutWriteRequest(HowGetMode: 2, UsersAgreement: true));
            Assert.True(result.Ok, result.Message);
            var no = result.OrderId.ToString(CultureInfo.InvariantCulture);
            Assert.Contains("with 2 supplier PO(s).", result.Message, StringComparison.Ordinal);

            Assert.Equal(
                "order_lifecycle:Customer Order → Delivery:7|po_lifecycle:Procurement → Goods receipt:4",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT(p.category, ':', p.name, ':', (SELECT COUNT(*) FROM epc_pf_steps s WHERE s.process_id = p.id)) ORDER BY p.category SEPARATOR '|') FROM epc_pf_processes p"));

            Assert.Equal(
                "Customer order #" + no + "|Order #" + no + "|open|2|101|0|sales",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', title, reference, status, current_step_no, current_assignee_id, initiator_id, current_department) FROM epc_pf_cases WHERE subject_type = 'shop_order' AND subject_id = " + no));
            Assert.Equal(
                "1:approved:101:Auto-advanced from order status|2:active:101:|3:pending:0:",
                await ScalarAsync(cs, "SELECT GROUP_CONCAT(CONCAT(step_no, ':', cs.status, ':', IF(cs.status = 'approved', acted_by, assignee_id), ':', IFNULL(comment, '')) ORDER BY step_no SEPARATOR '|') FROM epc_pf_case_steps cs JOIN epc_pf_cases c ON c.id = cs.case_id WHERE c.subject_type = 'shop_order' AND cs.step_no <= 3"));

            var poOwn = await ScalarAsync(cs, "SELECT p.id FROM epc_erp_purchase_orders p JOIN epc_erp_suppliers s ON s.id = p.supplier_id WHERE s.storage_id = 10");
            var poA = await ScalarAsync(cs, "SELECT p.id FROM epc_erp_purchase_orders p JOIN epc_erp_suppliers s ON s.id = p.supplier_id WHERE s.storage_id = 20");
            var poOwnNo = await ScalarAsync(cs, "SELECT po_no FROM epc_erp_purchase_orders WHERE id = " + poOwn);
            Assert.Equal(
                "Purchase order " + poOwnNo + " — Own Warehouse Dubai|" + poOwnNo + "|open|1|5|0",
                await ScalarAsync(cs, "SELECT CONCAT_WS('|', title, reference, status, current_step_no, current_assignee_id, initiator_id) FROM epc_pf_cases WHERE subject_type = 'erp_po' AND subject_id = " + poOwn));
            Assert.Equal("1|5", await ScalarAsync(cs, "SELECT CONCAT_WS('|', current_step_no, current_assignee_id) FROM epc_pf_cases WHERE subject_type = 'erp_po' AND subject_id = " + poA));

            var pipeline = scope.ServiceProvider.GetRequiredService<IStorefrontOrderCreatedPipeline>();
            await pipeline.RunAsync(result.OrderId, 5);
            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_pf_cases"));
            Assert.Equal("2", await ScalarAsync(cs, "SELECT current_step_no FROM epc_pf_cases WHERE subject_type = 'shop_order'"));

            await scope.ServiceProvider.GetRequiredService<IErpPurchaseOrderWriteService>().SetStatusAsync(long.Parse(poOwn, CultureInfo.InvariantCulture), "approved", 7);
            Assert.Equal("2|7", await ScalarAsync(cs, "SELECT CONCAT_WS('|', current_step_no, current_assignee_id) FROM epc_pf_cases WHERE subject_type = 'erp_po' AND subject_id = " + poOwn));
            Assert.Equal(
                "approved:5:Auto-advanced from PO status",
                await ScalarAsync(cs, "SELECT CONCAT_WS(':', cs.status, cs.acted_by, cs.comment) FROM epc_pf_case_steps cs JOIN epc_pf_cases c ON c.id = cs.case_id WHERE c.subject_type = 'erp_po' AND c.subject_id = " + poOwn + " AND cs.step_no = 1"));

            var status = await scope.ServiceProvider.GetRequiredService<IErpOrderFulfillmentWriteService>().StatusAsync(result.OrderId, 7);
            Assert.True(status.Ok, status.Message);
            Assert.Equal("approved", await ScalarAsync(cs, "SELECT status FROM epc_erp_purchase_orders WHERE id = " + poA));
            Assert.Equal("2|7", await ScalarAsync(cs, "SELECT CONCAT_WS('|', current_step_no, current_assignee_id) FROM epc_pf_cases WHERE subject_type = 'erp_po' AND subject_id = " + poA));
            Assert.Equal("2", await ScalarAsync(cs, "SELECT current_step_no FROM epc_pf_cases WHERE subject_type = 'shop_order'"));
            Assert.Equal("3", await ScalarAsync(cs, "SELECT COUNT(*) FROM epc_pf_cases"));
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

    private static ServiceProvider Services(string cs)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IErpWriteConnectionFactory>(new WriteConnections(cs));
        services.AddSingleton<ICpPlatformMailer>(new NullMailer());
        services.AddScoped<IErpVoucherNumberService, ErpVoucherNumberService>();
        services.AddScoped<IErpTaxAmountCalculator, ErpTaxAmountCalculator>();
        services.AddScoped<IErpAuditLogWriter, ErpAuditLogWriter>();
        services.AddScoped<IErpGlPostingService, ErpGlPostingService>();
        services.AddScoped<IErpSettlementAllocationService, ErpSettlementAllocationService>();
        services.AddScoped<IErpAdvanceVatService, ErpAdvanceVatService>();
        services.AddScoped<IErpCashWriteService, ErpCashWriteService>();
        services.AddScoped<IErpSalesInvoiceWriteService, ErpSalesInvoiceWriteService>();
        services.AddScoped<IErpInvoiceFromOrderWriteService, ErpInvoiceFromOrderWriteService>();
        services.AddScoped<IErpPurchaseInvoiceWriteService, ErpPurchaseInvoiceWriteService>();
        services.AddScoped<IErpSupplierWriteService, ErpSupplierWriteService>();
        services.AddScoped<IErpPurchaseOrderWriteService, ErpPurchaseOrderWriteService>();
        services.AddScoped<IErpPfProcessSaveWriteService, ErpPfProcessSaveWriteService>();
        services.AddScoped<IErpPfStepSaveWriteService, ErpPfStepSaveWriteService>();
        services.AddScoped<IErpPfSetDeptHeadWriteService, ErpPfSetDeptHeadWriteService>();
        services.AddScoped<IErpPfCaseStartWriteService, ErpPfCaseStartWriteService>();
        services.AddScoped<IErpPfCaseActWriteService, ErpPfCaseActWriteService>();
        services.AddScoped<IErpPfCaseCancelWriteService, ErpPfCaseCancelWriteService>();
        services.AddScoped<IErpProcessFlowSyncService, ErpPfDemoSyncWriteService>();
        services.AddScoped<IErpOrderFulfillmentWriteService, ErpOrderFulfillmentWriteService>();
        services.AddScoped<IStorefrontNotifyDispatcher, StorefrontNotifyDispatcher>();
        services.AddScoped<IStorefrontSupplierLpoNotifier, StorefrontSupplierLpoNotifier>();
        services.AddScoped<IStorefrontOrderCreatedPipeline, StorefrontOrderCreatedPipeline>();
        services.AddScoped<IStorefrontCheckoutWriteService, StorefrontCheckoutWriteService>();
        return services.BuildServiceProvider();
    }

    private static async Task ExecuteAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class NullMailer : ICpPlatformMailer
    {
        public IReadOnlyDictionary<string, string> ReadConfig() => new Dictionary<string, string>();

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => Task.FromResult(new CpSmsSendOutcome(true, string.Empty));
    }

    private sealed class WriteConnections(string connectionString) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }
}
