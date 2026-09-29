using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpNavigationCoverageTests
{
    [Fact]
    public void CoveragePageIsAnAspNetPrimaryRoute()
    {
        var root = FindRepoRoot();
        var page = Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "ErpNavigationCoverageApp.razor");
        var text = File.ReadAllText(page);

        Assert.Contains("@page \"/erp/navigation-coverage-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpIndustryNav.Inspect", text, StringComparison.Ordinal);
        Assert.Contains("ErpPhpTabRouteMap.TryMapTab", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref", text, StringComparison.Ordinal);

        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "PhpErpDesktopChrome.razor"));
        Assert.Contains("navigation-coverage-app", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPhpErpTabMapsToAnExistingAspNetPageRoute()
    {
        var root = FindRepoRoot();
        var pages = Directory
            .GetFiles(
                Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages"),
                "*.razor")
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                "@page\\s+\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value.Split('?', 2)[0]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = PhpModuleCatalog.ErpTabs
            .Select(tab => tab.Id[(tab.Id.LastIndexOf('/') + 1)..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(tab => ErpPhpTabRouteMap.TryMapTab(tab, out var href)
                && !pages.Contains(href.Split('?', 2)[0]))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPhpErpAreaHubStaysOnAnExistingErpRoute()
    {
        var root = FindRepoRoot();
        var pages = Directory
            .GetFiles(
                Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages"),
                "*.razor")
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                "@page\\s+\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value.Split('?', 2)[0]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = PhpModuleCatalog.ErpAreas
            .Select(area => PhpSurfaceLinkMap.AspNetPrimaryHref(area.Href))
            .Where(href => !href.StartsWith("/erp", StringComparison.OrdinalIgnoreCase)
                || !pages.Contains(href.Split('?', 2)[0]))
            .ToArray();

        Assert.Empty(invalid);
    }

    [Fact]
    public void TaxComplianceUsesTheErpChromeOnItsErpRoute()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "CpUaeTaxComplianceApp.razor"));
        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "TaxComplianceSurfaceChrome.razor"));

        Assert.Contains("@page \"/erp/uae-tax-compliance-app\"", page, StringComparison.Ordinal);
        Assert.Contains("<TaxComplianceSurfaceChrome", page, StringComparison.Ordinal);
        Assert.Contains("PhpErpDesktopChrome", chrome, StringComparison.Ordinal);
        Assert.Contains("PhpCpDesktopChrome", chrome, StringComparison.Ordinal);
        Assert.Contains("\"/erp/uae-tax-compliance-app\"", chrome, StringComparison.Ordinal);

        var adjustments = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Erp",
            "ErpUaeTaxSaveCtAdjustmentsWriteService.cs"));
        foreach (var field in new[]
        {
            "non_deductible_entertainment",
            "fines_penalties",
            "book_depreciation_excess",
            "related_party_adjustments",
            "other_add_backs",
            "exempt_income",
            "foreign_branch_exemption",
            "loss_carryforward",
            "qualifying_donations",
            "other_deductions",
        })
        {
            Assert.Contains("(\"" + field + "\"", adjustments, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ErpUserControlDefinesCapabilityGroupsAndStandardActions()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "ErpUserControlApp.razor"));
        var catalog = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Presentation",
            "ErpCapabilityCatalog.cs"));
        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "PhpErpDesktopChrome.razor"));

        Assert.Contains("@page \"/erp/user-control-app\"", page, StringComparison.Ordinal);
        Assert.Contains("role → capability → area/action mappings", page, StringComparison.Ordinal);
        foreach (var group in new[] { "Finance", "Purchasing", "Sales", "Inventory", "Projects / fit-out", "Jewellery", "HR / payroll", "Administration" })
        {
            Assert.Contains("new(\"" + group, catalog, StringComparison.Ordinal);
        }

        foreach (var action in new[] { "View", "New", "Edit", "Delete", "Void", "Submit", "Approve", "Reject", "Post", "Reverse", "Print", "Export" })
        {
            Assert.Contains("\"" + action + "\"", catalog, StringComparison.Ordinal);
        }

        Assert.Contains("<a href=\"/erp/user-control-app\">User control</a>", chrome, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
