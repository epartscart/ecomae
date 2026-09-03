using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Slim leftover guards from CP tenant PHP-parity: detail consoles, floor columns,
/// and showcase Open/Configure leaving empty shells for classic PHP.
/// Does not require PhpCpModulePageHeader on every CSS-shim app.
/// </summary>
public sealed class CpTenantDetailFloorParityTests
{
    [Theory]
    [InlineData("CpPagesApp.razor", "content_id=", "_selected")]
    [InlineData("CpMenusApp.razor", "menu_id=", "_selected")]
    [InlineData("CpPaymentGatewaysApp.razor", "ConfigureHref", "_selected")]
    [InlineData("CpProductCatalogueApp.razor", "product_id=", "_selected")]
    public void DetailConsoles_HaveWorkspaceAndSelectedRow(string fileName, string rowKey, string selectedMarker)
    {
        var text = File.ReadAllText(FindRepoFile($"aspnet/src/EcomAE.Platform/Components/Pages/{fileName}"));
        Assert.Contains("epc-scp-users-workspace", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace__detail", text, StringComparison.Ordinal);
        Assert.Contains(rowKey, text, StringComparison.Ordinal);
        Assert.Contains(selectedMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP compare", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<a href=\"/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CarriersApp_ExposesBlurbOrCreatedFloorColumn()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCarriersApp.razor"));
        Assert.True(
            text.Contains("row.Blurb", StringComparison.Ordinal)
            || text.Contains("row.TimeCreated", StringComparison.Ordinal),
            "Carriers floor must expose Blurb or Created.");
    }

    [Fact]
    public void JewelleryRepairsApp_ExposesCompanyOrBranchFloorColumn()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRepairsApp.razor"));
        Assert.True(
            text.Contains("row.CompanyId", StringComparison.Ordinal)
            || text.Contains("row.Branch", StringComparison.Ordinal),
            "Jewellery repairs floor must expose Company or Branch.");
    }

    [Theory]
    [InlineData("CpModulesApp.razor")]
    [InlineData("CpPortalSettingsApp.razor")]
    [InlineData("CpTenantsApp.razor")]
    public void ShowcaseApps_OpenConfigureUsesPhpReferenceOnlyHref(string fileName)
    {
        var text = File.ReadAllText(FindRepoFile(
            $"aspnet/src/EcomAE.Platform/Components/Pages/{fileName}"));
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)", text, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
