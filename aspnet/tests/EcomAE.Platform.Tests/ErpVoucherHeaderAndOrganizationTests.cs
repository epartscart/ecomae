using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpVoucherHeaderAndOrganizationTests
{
    [Fact]
    public void TodayDate_IsIsoCalendarDate()
    {
        var today = ErpVoucherHeader.TodayDate();
        Assert.Equal(10, today.Length);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), today);
        Assert.Equal("Auto", ErpVoucherHeader.AutoNumber);
    }

    [Fact]
    public void Shared_header_shows_date_auto_number_and_org_dimensions()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpVoucherHeaderFields.razor"));

        Assert.Contains("name=\"@DateFieldName\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpVoucherHeader.TodayDate()", text, StringComparison.Ordinal);
        Assert.Contains("name=\"voc_no_preview\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpVoucherHeader.AutoNumber", text, StringComparison.Ordinal);
        Assert.Contains("name=\"legal_entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"business_unit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"cost_centre\"", text, StringComparison.Ordinal);
        Assert.Contains("/erp/organization-app?tab=legal_entities", text, StringComparison.Ordinal);
        Assert.Contains("/erp/organization-app?tab=business_units", text, StringComparison.Ordinal);
        Assert.Contains("/erp/organization-app?tab=cost_centres", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-doc-header", text, StringComparison.Ordinal);
        Assert.Contains("Document identity", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_create_well_is_an_enterprise_card()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpPhpCreateWell.razor"));

        Assert.Contains("epc-erp-create-card", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-create-card__hd", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-create-card__bd", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Enterprise_design_system_aligns_erp_and_cp_forms()
    {
        var css = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/wwwroot/css/epc-enterprise-design-system.css"));

        Assert.Contains(".epc-erp-content-body form:not([data-epc-inline=\"1\"])", css, StringComparison.Ordinal);
        Assert.Contains(".epc-cp-content-inner form:not([data-epc-inline=\"1\"])", css, StringComparison.Ordinal);
        Assert.Contains("display: grid !important", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-doc-header", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-create-card", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-content-body .form-actions", css, StringComparison.Ordinal);
        Assert.Contains("table form", css, StringComparison.Ordinal);
        Assert.Contains("display: inline-flex !important", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpGlJournalsApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRetailApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor")]
    public void Document_create_forms_use_shared_voucher_header(string relative)
    {
        var text = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains("<ErpVoucherHeaderFields", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Organization_app_is_the_tenant_findable_org_hub()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpOrganizationApp.razor"));

        Assert.Contains("@page \"/erp/organization-app\"", text, StringComparison.Ordinal);
        Assert.Contains("tab=legal_entities", text, StringComparison.Ordinal);
        Assert.Contains("tab=business_units", text, StringComparison.Ordinal);
        Assert.Contains("tab=class_units", text, StringComparison.Ordinal);
        Assert.Contains("tab=dimensions", text, StringComparison.Ordinal);
        Assert.Contains("tab=cost_centres", text, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_legal_entities", text, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_business_units", text, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_class_units", text, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_dimensions", text, StringComparison.Ordinal);
        Assert.Contains("/erp/fin/cost-centers/write", text, StringComparison.Ordinal);
        Assert.Contains("OrganizationMasters.LoadAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Company_bar_links_every_tenant_to_organization()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpCompanyIndustryBar.razor"));

        Assert.Contains("href=\"/erp/organization-app\"", text, StringComparison.Ordinal);
        Assert.Contains("Legal entity, business unit, cost centre, financial dimensions", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Php_org_tabs_no_longer_hide_on_consolidations_or_tenant_config()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("business_units", out var units));
        Assert.Equal("/erp/organization-app?tab=business_units", units);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("org_admin", out var admin));
        Assert.Equal("/erp/organization-app?tab=legal_entities", admin);
        Assert.DoesNotContain("consolidations-app", units, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-config-app", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void Company_context_seeds_tenant_organization_for_every_shop()
    {
        var schema = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        Assert.Contains("EnsureTenantOrganizationAsync", schema, StringComparison.Ordinal);
        Assert.Contains("SeedDefaultHqOrganizationAsync", schema, StringComparison.Ordinal);
        Assert.Contains("\"HQ\", \"Head office\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"CC-ADMIN\", \"Administration\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"DIV-MAIN\", \"Main division\"", schema, StringComparison.Ordinal);
        Assert.Contains("await EnsureTenantOrganizationAsync(c, ct)", schema, StringComparison.Ordinal);

        var bootstrap = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/ErpOnlyShopBootstrap.cs"));
        Assert.Contains("EnsureTenantOrganizationAsync", bootstrap, StringComparison.Ordinal);

        var reader = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOrganizationMasterReadService.cs"));
        Assert.Contains("EnsureTenantOrganizationAsync", reader, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_class_units", reader, StringComparison.Ordinal);
        Assert.Contains("epc_cc_centers", reader, StringComparison.Ordinal);
        Assert.Contains("ClassUnits", reader, StringComparison.Ordinal);
        Assert.Contains("CostCentres", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void Cash_and_invoice_writes_bind_document_date_from_the_header()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("UnixDate(form, \"voc_date\", \"voucher_date\", \"entry_date\")", module, StringComparison.Ordinal);
        Assert.Contains("LiveWriteFormBinder.Long(form, \"id\", \"sales_order_id\", \"salesOrderId\")", module, StringComparison.Ordinal);
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

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
