using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Defects found by the fresh ERP-only tenant browser acceptance run: PHP lazily
/// provisions inventory and period-close tables, resolves the e-invoice seller
/// country from the CP portal settings row, and serves the purchase-order script.
/// </summary>
public sealed class ErpFreshTenantAcceptanceFixTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Fact]
    public void Purchase_orders_script_is_allowlisted_and_exists()
    {
        Assert.Contains("\"erp-purchase-orders.js\"", Read("aspnet/src/EcomAE.Platform/Erp/ErpAppAssets.cs"));
        Assert.True(File.Exists(Path.Combine(Root, "aspnet/src/EcomAE.Platform/wwwroot/erp/erp-purchase-orders.js")));
    }

    [Fact]
    public void Inventory_writer_ensures_schema_after_every_open()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpInventoryMovementWriteService.cs");
        var opens = source.Split("_connections.OpenAsync(cancellationToken)").Length - 1;
        var ensures = source.Split("ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken)").Length - 1;
        Assert.True(opens > 0);
        Assert.Equal(opens, ensures);
    }

    [Theory]
    [InlineData("epc_erp_inv_items")]
    [InlineData("epc_erp_inv_stock")]
    [InlineData("epc_erp_inv_movements")]
    [InlineData("epc_erp_inv_warehouses")]
    [InlineData("epc_erp_inv_serials")]
    [InlineData("epc_erp_periods")]
    [InlineData("epc_erp_period_close_log")]
    [InlineData("epc_erp_fiscal_locks")]
    public void Lazy_schema_creates_php_owned_tables(string table)
    {
        Assert.Contains("CREATE TABLE IF NOT EXISTS `" + table + "`", Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
    }

    [Fact]
    public void Period_schema_ready_provisions_before_checking()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpPeriodLockReopenWriteService.cs");
        var ensure = source.IndexOf("ErpLazySchema.EnsurePeriodCloseAsync(connection, ct)", StringComparison.Ordinal);
        var check = source.IndexOf("ColumnExistsAsync(connection, \"epc_erp_periods\", \"checklist_json\", ct)", StringComparison.Ordinal);
        Assert.True(ensure > 0 && ensure < check);
    }

    [Theory]
    [InlineData("AE", "AE")]
    [InlineData("United Arab Emirates", "AE")]
    [InlineData("uae", "AE")]
    [InlineData("United Kingdom", "GB")]
    [InlineData("usa", "US")]
    [InlineData("sa", "SA")]
    [InlineData("", "")]
    [InlineData("Somewhere", "")]
    public void Country_name_to_iso_matches_php(string input, string expected)
    {
        Assert.Equal(expected, EcomAE.Platform.Erp.ErpEinvoiceProfileWriteService.CountryNameToIso(input));
    }

    [Fact]
    public void Seller_country_falls_back_to_portal_settings_after_profile_and_company()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpEinvoiceProfileWriteService.cs");
        var profile = source.IndexOf("epc_tax_toolkit_tenant_profile` WHERE", StringComparison.Ordinal);
        var company = source.IndexOf("'company_country_code'", StringComparison.Ordinal);
        var portal = source.IndexOf("ResolvePortalCountryAsync(connection, cancellationToken)", StringComparison.Ordinal);
        Assert.True(profile > 0 && profile < company && company < portal);
    }

    [Fact]
    public void Cp_settings_reader_tolerates_missing_menu_tables()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Cp/CpIndustrySettingsService.cs");
        Assert.Contains("catch (DbException)", source);
    }
}
