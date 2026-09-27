using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpSuperCustomerBoardTests
{
    [Theory]
    [InlineData("Demo-Tenant!", "demotenant")]
    [InlineData("  PLATFORM  ", "platform")]
    [InlineData(null, "")]
    public void Tenant_filter_is_normalised_like_php(string? raw, string expected)
    {
        Assert.Equal(expected, CpSuperCustomerBoardService.NormalizeTenantFilter(raw));
    }

    [Theory]
    [InlineData("https://www.Shop.ae/cp/", "www.shop.ae")]
    [InlineData("shop.ae", "www.shop.ae")]
    [InlineData("localhost", "")]
    [InlineData("", "")]
    public void Commerce_host_matches_php(string hostname, string expected)
    {
        Assert.Equal(expected, CpSuperCustomerBoardService.CommerceHost(hostname));
    }

    [Fact]
    public void Customer_name_falls_back_company_then_email()
    {
        Assert.Equal("Ali Khan", CpSuperCustomerBoardService.CustomerName("Ali", "Khan", "Acme", "a@b.c"));
        Assert.Equal("Acme", CpSuperCustomerBoardService.CustomerName(" ", "", "Acme", "a@b.c"));
        Assert.Equal("a@b.c", CpSuperCustomerBoardService.CustomerName("", "", "", "a@b.c"));
    }

    [Fact]
    public void Tenant_option_label_matches_php_selector()
    {
        var option = new CpSuperCustomerTenantOption("acme", "Acme Trading", "www.acme.ae") { Database = "acme_shop" };
        Assert.Equal("Acme Trading (acme)", option.DropdownLabel);
        Assert.Equal("acme_shop", option.Database);
    }

    [Fact]
    public void Page_is_php_customer_board_twin()
    {
        var razor = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCustomerBoardApp.razor"));

        Assert.Contains("Customer board", razor, StringComparison.Ordinal);
        Assert.Contains("Search and filter customers across the platform and live tenant databases", razor, StringComparison.Ordinal);
        Assert.Contains("Tenant hub", razor, StringComparison.Ordinal);
        Assert.Contains("Communication", razor, StringComparison.Ordinal);
        Assert.Contains("Operator guide", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"tenant\"", razor, StringComparison.Ordinal);
        Assert.Contains("Platform only", razor, StringComparison.Ordinal);
        Assert.Contains("Tenants scanned", razor, StringComparison.Ordinal);
        Assert.Contains("Quick actions", razor, StringComparison.Ordinal);
        Assert.Contains("No customers found", razor, StringComparison.Ordinal);
        Assert.Contains("ICpSuperCustomerBoardService", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_searches_platform_and_tenant_databases_without_secrets()
    {
        var service = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpSuperCustomerBoardService.cs"));

        Assert.Contains("OpenRegistryAsync", service, StringComparison.Ordinal);
        Assert.Contains("epc_portal_tenants", service, StringComparison.Ordinal);
        Assert.Contains("`users_profiles`", service, StringComparison.Ordinal);
        Assert.Contains("ErpDb.AddParameters", service, StringComparison.Ordinal);
        Assert.DoesNotContain("password", service, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_marks_customer_board_twin_live()
    {
        var entry = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/customer-board-app");
        Assert.Equal("twin-live", entry.Status);
        Assert.Contains("epc_super_cp_customer_board.php", entry.Notes, StringComparison.Ordinal);
        Assert.Contains("cross-tenant", entry.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_is_registered()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpSuperCustomerBoardService", program, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
