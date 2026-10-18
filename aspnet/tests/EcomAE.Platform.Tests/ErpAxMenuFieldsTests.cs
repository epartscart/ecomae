using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAxMenuFieldsTests
{
    [Fact]
    public void EveryWorkspaceMenu_HasStudyFields()
    {
        foreach (var page in ErpD365WorkspaceCatalog.All)
        {
            var fields = ErpAxMenuFields.For(page);
            Assert.True(fields.Count >= 4, page.Tab);
            Assert.NotEmpty(ErpAxMenuFields.Extras(page));
            Assert.All(fields, field => Assert.False(string.IsNullOrWhiteSpace(field.Label)));
        }
    }

    [Fact]
    public void QuotationInvoiceAndTracking_UseTheStudyNames()
    {
        var quotation = Page("quotation");
        Assert.Equal("Quotation", ErpAxMenuFields.Caption(quotation, "code", "Code"));
        Assert.Contains(ErpAxMenuFields.Extras(quotation), field => field.Label == "Customer reference");
        Assert.Contains(ErpAxMenuFields.Extras(quotation), field => field.Label == "Tax group");

        var invoice = Page("invoice");
        Assert.Equal("Invoice", ErpAxMenuFields.Caption(invoice, "code", "Code"));
        Assert.Contains(ErpAxMenuFields.Extras(invoice), field => field.Label == "Payment terms");

        var tracking = Page("serial");
        Assert.Contains(ErpAxMenuFields.Extras(tracking), field => field.Label == "Serial number control");
        Assert.Contains(ErpAxMenuFields.Extras(tracking), field => field.Label == "Blank receipt allowed");
    }

    [Fact]
    public void Pack_KeepsOnlyTheExtraFields()
    {
        var page = Page("quotation");
        var packed = ErpAxMenuFields.Pack(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["customer_ref"] = "PO-44",
                ["tax_group"] = "VAT",
                ["quotation"] = "ignored because it is a column",
            },
            page);
        Assert.Contains("customer_ref=PO-44", packed, StringComparison.Ordinal);
        Assert.Contains("tax_group=VAT", packed, StringComparison.Ordinal);
        Assert.DoesNotContain("quotation=", packed, StringComparison.Ordinal);
        var shown = ErpAxMenuFields.Display(packed, page);
        Assert.Contains("Customer reference: PO-44", shown, StringComparison.Ordinal);
        Assert.Contains("Tax group: VAT", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceForm_RendersAndStoresTheMenuFields()
    {
        var razor = Read("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor");
        Assert.Contains("epc-ax-fields", razor, StringComparison.Ordinal);
        Assert.Contains("ax_", razor, StringComparison.Ordinal);
        Assert.Contains("Fields on this menu", razor, StringComparison.Ordinal);
        var writer = Read("aspnet/src/EcomAE.Platform/Erp/ErpD365WorkspaceWriteService.cs");
        Assert.Contains("fields_json", writer, StringComparison.Ordinal);
        var schema = Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs");
        Assert.Contains("fields_json", schema, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteService_StoresMenuFieldsOnThrowawayDb()
    {
        if (Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E") != "1")
        {
            return;
        }

        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "";

        var database = "ecomae_axmenu_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using (var adminConn = new MySqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using var create = new MySqlCommand("CREATE DATABASE `" + database + "`", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var page = Page("quotation", workspaceOnly: true);
            var packed = ErpAxMenuFields.Pack(
                new Dictionary<string, string> { ["customer_ref"] = "PO-44", ["tax_group"] = "VAT" },
                page);
            var writes = new ErpD365WorkspaceWriteService(new Connections(cs));
            var saved = await writes.SaveAsync(new ErpD365WorkspaceWriteRequest(
                Area: page.Area,
                Tab: page.Tab,
                Code: "Q-44",
                Name: "Harbour quote",
                Party: "C-100",
                FieldsJson: packed));
            Assert.True(saved.Succeeded, saved.Message);

            var rows = await new ErpD365WorkspaceReadService(new Connections(cs)).ListAsync(page.Area, page.Tab);
            var row = Assert.Single(rows);
            Assert.Equal("Q-44", row.Code);
            Assert.Contains("customer_ref=PO-44", row.FieldsJson, StringComparison.Ordinal);
            Assert.Contains("Customer reference: PO-44", ErpAxMenuFields.Display(row.FieldsJson, page), StringComparison.Ordinal);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class Connections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private static ErpD365Part3Catalog.Page Page(string token, bool workspaceOnly = false)
        => ErpD365WorkspaceCatalog.All.First(page =>
            (!workspaceOnly || page.ExistingHref is null)
            && (page.Tab.Contains(token, StringComparison.OrdinalIgnoreCase)
                || page.Label.Contains(token, StringComparison.OrdinalIgnoreCase)));

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
