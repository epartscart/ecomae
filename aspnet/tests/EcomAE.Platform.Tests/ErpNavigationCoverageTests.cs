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
