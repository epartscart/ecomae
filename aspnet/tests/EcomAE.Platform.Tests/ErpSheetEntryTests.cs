using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSheetEntryTests
{
    [Fact]
    public void Customers_RequireName()
    {
        var missing = ErpDataMigrationCsv.Inspect("master_data", "customers", "customers.csv", "email\nacme@example.com");
        Assert.False(missing.Ok);
        Assert.Contains("name", missing.Message, StringComparison.OrdinalIgnoreCase);

        var blank = ErpDataMigrationCsv.Inspect("master_data", "customers", "customers.csv", "name,email\n,acme@example.com");
        Assert.False(blank.Ok);
        Assert.Contains("Customer name is required", blank.Message, StringComparison.Ordinal);

        var ready = ErpDataMigrationCsv.Inspect("master_data", "customers", "customers.csv", "name,email\nAcme Trading,acme@example.com");
        Assert.True(ready.Ok);
        Assert.Equal(1, ready.ValidRows);
        Assert.Equal("valid", ready.Rows[0].Status);
    }

    [Fact]
    public void Invoices_RequireAmount()
    {
        var missing = ErpDataMigrationCsv.Inspect("full_transaction", "invoices", "invoices.csv", "invoice_no,customer\n100,Acme");
        Assert.False(missing.Ok);
        Assert.Contains("amount", missing.Message, StringComparison.OrdinalIgnoreCase);

        var blank = ErpDataMigrationCsv.Inspect("full_transaction", "invoices", "invoices.csv", "invoice_no,amount\n100,");
        Assert.False(blank.Ok);
        Assert.Contains("Amount is required", blank.Message, StringComparison.Ordinal);

        var ready = ErpDataMigrationCsv.Inspect("full_transaction", "invoices", "invoices.csv", "invoice_no,amount\n100,\"1,250.00\"");
        Assert.True(ready.Ok);
        Assert.Contains("1,250.00", ready.Rows[0].RawJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("suppliers", "email\nx@y.com", "name")]
    [InlineData("products", "name\nBolt", "sku")]
    [InlineData("coa", "account_name\nCash", "account_code")]
    [InlineData("open_balances", "debit,credit\n10,0", "account_code")]
    [InlineData("inventory", "warehouse,qty_on_hand\nMain,4", "sku")]
    public void Templates_RequireThePhpColumn(string entity, string csv, string column)
    {
        var inspect = ErpDataMigrationCsv.Inspect("open_balance", entity, entity + ".csv", csv);
        Assert.False(inspect.Ok);
        Assert.Contains(column, inspect.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SheetChecks_BlockAnEmptyPost()
    {
        Assert.False(ErpSheetEntryCatalog.Check("stock_transfer", new Dictionary<string, string>()).Ok);
        Assert.False(ErpSheetEntryCatalog.Check("stock_movement", new Dictionary<string, string>()).Ok);
        Assert.False(ErpSheetEntryCatalog.Check("staff_profile", new Dictionary<string, string>()).Ok);
        Assert.False(ErpSheetEntryCatalog.Check("inventory_report", new Dictionary<string, string>()).Ok);
        Assert.False(ErpSheetEntryCatalog.Check("finance_close", new Dictionary<string, string>()).Ok);

        var transfer = ErpSheetEntryCatalog.Check("stock_transfer", new Dictionary<string, string>
        {
            ["from_warehouse_id"] = "1",
            ["to_warehouse_id"] = "2",
            ["sku"] = "BOLT",
            ["qty"] = "3",
        });
        Assert.True(transfer.Ok);

        var staff = ErpSheetEntryCatalog.Check("staff_profile", new Dictionary<string, string>
        {
            ["user_id"] = "9",
            ["display_name"] = "Amina Noor",
            ["department_code"] = "finance",
        });
        Assert.True(staff.Ok);
    }

    [Theory]
    [InlineData("Components/Pages/CpDataMigrationsApp.razor", "data_migration", "Import is not available on this page.")]
    [InlineData("Components/Pages/ErpStockTransfersApp.razor", "stock_transfer", "Ship and receive are not available on this page.")]
    [InlineData("Components/Pages/ErpStockMovementsApp.razor", "stock_movement", "Writes are not available on this page.")]
    [InlineData("Components/Pages/ErpStaffApp.razor", "staff_profile", "Writes are not available on this page.")]
    [InlineData("Components/Pages/ErpInventoryReportApp.razor", "inventory_report", "Snapshot generate is not available on this page.")]
    [InlineData("Components/Pages/CpFinanceCloseApp.razor", "finance_close", "")]
    public void EmptyPages_HaveValidateAndPostAwayFromTheList(string relativePath, string sheet, string removed)
    {
        var path = Path.Combine(FindPlatformRoot(), relativePath);
        var text = File.ReadAllText(path);
        var form = File.ReadAllText(Path.Combine(FindPlatformRoot(), "Components/Shared/Desktop/ErpSheetEntryForm.razor"));
        Assert.Contains("pm_view", text, StringComparison.Ordinal);
        Assert.Contains("Sheet=\"" + sheet + "\"", text, StringComparison.Ordinal);
        Assert.Contains("_view == \"entry\"", text, StringComparison.Ordinal);
        Assert.Contains(">Validate<", form, StringComparison.Ordinal);
        Assert.Contains(">Post<", form, StringComparison.Ordinal);
        if (removed.Length > 0)
        {
            Assert.DoesNotContain(removed, text, StringComparison.Ordinal);
        }
    }

    private static string FindPlatformRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            if (Directory.Exists(Path.Combine(dir.FullName, "Components", "Pages")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("EcomAE.Platform root was not found from the test output.");
    }
}
