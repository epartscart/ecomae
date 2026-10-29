using System;
using System.IO;
using System.Linq;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRowWiseLayoutTests
{
    [Fact]
    public void Design_system_packs_opened_facts_and_forms_in_rows()
    {
        var css = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/wwwroot/css/epc-enterprise-design-system.css"));

        Assert.Contains(".epc-erp-facts", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap", css, StringComparison.Ordinal);
        Assert.Contains("--epc-field-min: 10.5rem", css, StringComparison.Ordinal);
        Assert.Contains("minmax(var(--epc-field-min), 14rem)", css, StringComparison.Ordinal);
        Assert.Contains("form[data-epc-inline=\"1\"] > label", css, StringComparison.Ordinal);
        Assert.Contains("width: 7.25rem", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-fact-row", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-doc-header .epc-erp-doc-header", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-content-body form > div", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-content-body form .row", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-content-body td form:not([data-epc-inline=\"1\"])", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-content-body form input[style]", css, StringComparison.Ordinal);
        Assert.Contains("#erp-module-new.panel .panel-body > .row", css, StringComparison.Ordinal);
        Assert.Contains(".epc-item-part__books", css, StringComparison.Ordinal);
        Assert.Contains("display: contents", css, StringComparison.Ordinal);
        Assert.Contains(".epc-jw-panel[hidden]", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--epc-field-min: 13.5rem", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Document_workspace_shows_field_groups_side_by_side()
    {
        var razor = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpDocumentWorkspace.razor"));
        var css = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/wwwroot/css/erp-document-workspace.css"));

        Assert.Contains("epc-document-workspace__field-row", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"address\" open", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"financial\" open", razor, StringComparison.Ordinal);
        Assert.Contains(".epc-document-workspace__field-row{display:grid;grid-template-columns:repeat(3,minmax(0,1fr))", css, StringComparison.Ordinal);
        Assert.Contains("flex-direction:row", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchasesApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpUsersApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpProductInfoApp.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor")]
    public void Opened_records_use_row_wise_facts(string relative)
    {
        var text = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains("class=\"epc-erp-facts\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-template-columns:8rem 1fr", text, StringComparison.Ordinal);
        Assert.DoesNotContain("grid-template-columns:9rem 1fr", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Purchase_opened_card_sits_facts_beside_actions()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchasesApp.razor"));
        Assert.Contains("class=\"epc-erp-fact-row\"", text, StringComparison.Ordinal);
        Assert.Contains("class=\"epc-erp-facts\"", text, StringComparison.Ordinal);
        Assert.Contains("class=\"epc-erp-action-pane\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Metal_purchase_keeps_one_panel_visible()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor"));
        // Panels switch in-page (no navigation) so typed entries are never lost.
        Assert.Contains("data-jw-panel=\"lines\">", text, StringComparison.Ordinal);
        Assert.Contains("data-jw-panel=\"addins\" hidden", text, StringComparison.Ordinal);
        Assert.Contains("data-jw-panel=\"attach\" hidden", text, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"@PanelHref(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@if (_panel != \"attach\")", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_no_longer_stack_opened_definition_lists()
    {
        var pages = Path.Combine(FindRepoDir("aspnet/src/EcomAE.Platform/Components/Pages"), "");
        var leftovers = Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("grid-template-columns:8rem 1fr", StringComparison.Ordinal)
                    || text.Contains("<dl style=\"display:grid;grid-template-columns:", StringComparison.Ordinal);
            })
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Empty(leftovers);
    }

    [Fact]
    public void Stylesheet_cache_bust_is_row_layout_revision()
    {
        var assets = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs"));
        Assert.Contains("epc-enterprise-design-system.css?v=20261016fit", assets, StringComparison.Ordinal);
        Assert.Contains("erp-document-workspace.css?v=20261013row1", assets, StringComparison.Ordinal);
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

    private static string FindRepoDir(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(relative);
    }
}
