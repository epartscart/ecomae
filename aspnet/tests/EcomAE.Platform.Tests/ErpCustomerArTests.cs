using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCustomerArTests
{
    [Fact]
    public void Statement_balance_nets_invoices_credits_and_receipts()
    {
        var when = new DateTimeOffset(new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var invoices = new[]
        {
            ErpCustomerAr.MakeInvoice(1, 7, "INV-1", when, 100m, 40m, false),
            ErpCustomerAr.MakeInvoice(2, 7, "CN-1", when + 10, 20m, 0m, true),
            ErpCustomerAr.MakeInvoice(3, 8, "INV-2", when, 50m, 50m, false),
        };
        var receipts = new[]
        {
            ErpCustomerAr.MakeReceipt(9, 7, "RV-1", when + 20, 40m, 40m),
            ErpCustomerAr.MakeReceipt(10, 7, "RV-2", when + 30, 30m, 0m),
        };
        var names = new Dictionary<long, string> { [7] = "alpha@example.com" };

        var report = ErpCustomerAr.Build(invoices, receipts, names, "alpha", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        Assert.Equal(40m, report.Open);
        Assert.Equal(30m, report.Unapplied);
        Assert.Equal(10m, report.Net);
        Assert.Equal(10m, report.StatementBalance);
        Assert.Equal(["Invoice", "Credit", "Payment", "Payment"], report.Statement.Select(line => line.Kind).ToArray());
        Assert.Equal(10m, report.Statement[^1].Balance);
        Assert.Single(report.OpenInvoices);
        Assert.Empty(report.SettledInvoices);
        var byId = ErpCustomerAr.Build(invoices, receipts, names, "7", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        Assert.DoesNotContain(byId.Statement, line => line.CustomerId == 8);
        Assert.Contains(byId.Statement, line => line.Number == "INV-1");
        Assert.Equal(0m, ErpCustomerAr.OutstandingOf(10m, 10m));
    }

    [Fact]
    public void Inquiries_open_on_the_receivables_pages()
    {
        Assert.Equal(ErpCustomerAr.ViewOpen, ErpCustomerAr.ViewFor("ar_payment_journal"));
        Assert.Equal(ErpCustomerAr.ViewStatement, ErpCustomerAr.ViewFor("ar_customer_statement"));
        Assert.Equal(ErpCustomerAr.ViewRecon, ErpCustomerAr.ViewFor("ar_customer_ledger_recon"));
        Assert.False(ErpCustomerAr.IsInquiryTab("ar_terms_payment"));

        Assert.Equal("/erp/receivables-app?view=open", ErpPhpTabRouteMap.TryMapTab("ar_open_invoices", out var open) ? open : "");
        Assert.Equal("/erp/receivables-app?view=open", ErpPhpTabRouteMap.TryMapTab("ar_payment_journal", out var pay) ? pay : "");
        Assert.Equal("/erp/receivables-workspace-app?tab=ar_customer_statement", ErpPhpTabRouteMap.TryMapTab("ar_customer_statement", out var statement) ? statement : "");

        var root = FindRepoRoot();
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpCustomerArPanel.razor"));
        var workspace = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpReceivablesApp.razor"));
        Assert.Contains("Post receipt and settle", panel, StringComparison.Ordinal);
        Assert.Contains("alloc_invoice_id", panel, StringComparison.Ordinal);
        Assert.Contains("ErpCustomerArPanel", workspace, StringComparison.Ordinal);
        Assert.Contains("ErpCustomerArPanel", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Load_keeps_open_invoices_and_unapplied_receipts()
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
            var inside = new DateTimeOffset(new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
            await ExecAsync(cs, $"""
                CREATE TABLE `users` (`user_id` INT PRIMARY KEY, `email` VARCHAR(255));
                CREATE TABLE `epc_einvoice_documents` (
                  `id` INT PRIMARY KEY, `user_id` INT, `invoice_number` VARCHAR(64), `issue_date` INT,
                  `total_incl_vat` DECIMAL(14,2), `paid_amount` DECIMAL(14,2), `doc_category` VARCHAR(32),
                  `status` VARCHAR(32), `active` TINYINT);
                CREATE TABLE `epc_erp_cash_bank_entries` (
                  `id` INT PRIMARY KEY, `counterparty_id` INT, `counterparty_type` VARCHAR(16), `time` INT,
                  `amount` DECIMAL(14,2), `voucher_no` VARCHAR(64), `reference` VARCHAR(64),
                  `entry_type` VARCHAR(16), `direction` TINYINT);
                CREATE TABLE `epc_erp_settlement_allocations` (
                  `id` INT PRIMARY KEY, `doc_type` VARCHAR(8), `cash_entry_id` INT, `counterparty_id` INT,
                  `invoice_id` INT, `amount` DECIMAL(14,2), `active` TINYINT);
                INSERT INTO `users` VALUES (7, 'alpha@example.com');
                INSERT INTO `epc_einvoice_documents` VALUES
                  (1, 7, 'INV-1', {inside}, 100, 40, 'tax_invoice', 'validated', 1),
                  (2, 7, 'INV-2', {inside}, 50, 50, 'tax_invoice', 'validated', 1),
                  (3, 7, 'INV-X', {inside}, 80, 0, 'tax_invoice', 'cancelled', 1);
                INSERT INTO `epc_erp_cash_bank_entries` VALUES
                  (9, 7, 'customer', {inside}, 40, 'RV-1', '', 'receipt', 1),
                  (10, 7, 'customer', {inside}, 30, 'RV-2', '', 'receipt', 1);
                INSERT INTO `epc_erp_settlement_allocations` VALUES (1, 'ar', 9, 7, 1, 40, 1);
                """);
            await using var db = new MySqlConnection(cs);
            await db.OpenAsync();
            var report = await ErpCustomerAr.LoadAsync(db, "7", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), CancellationToken.None);
            Assert.Equal(60m, report.Open);
            Assert.Equal(30m, report.Unapplied);
            Assert.Equal(30m, report.Net);
            Assert.Equal(80m, report.StatementBalance);
            Assert.Equal("alpha@example.com", report.Balances[0].Customer);
            Assert.DoesNotContain(report.OpenInvoices, row => row.Number == "INV-X");
            Assert.Contains(report.SettledInvoices, row => row.Number == "INV-2");
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
