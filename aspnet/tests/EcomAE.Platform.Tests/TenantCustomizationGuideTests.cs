using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantCustomizationGuideTests
{
    private static readonly string PagesDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "EcomAE.Platform", "Components", "Pages");

    [Fact]
    public void Every_embedded_guide_slug_exists_in_the_catalog()
    {
        var used = Directory.EnumerateFiles(PagesDir, "*.razor")
            .Select(File.ReadAllText)
            .SelectMany(c => Regex.Matches(c, @"TenantCustomizationGuideBoard\s+Slug=""([^""]+)"""))
            .Select(m => m.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(used);
        foreach (var slug in used)
        {
            Assert.True(
                TenantCustomizationGuide.Areas.ContainsKey(slug),
                $"Guide slug '{slug}' is embedded in a page but missing from TenantCustomizationGuide.Areas");
        }
    }

    [Fact]
    public void Every_area_has_steps_a_preview_and_valid_related_links()
    {
        foreach (var (slug, area) in TenantCustomizationGuide.Areas)
        {
            Assert.False(string.IsNullOrWhiteSpace(area.Title), slug);
            Assert.False(string.IsNullOrWhiteSpace(area.Intro), slug);
            Assert.False(string.IsNullOrWhiteSpace(area.PreviewKind), slug);
            Assert.False(string.IsNullOrWhiteSpace(area.PreviewCaption), slug);
            Assert.True(area.Steps.Count >= 3, $"{slug} should have at least 3 steps");
            Assert.Equal(
                Enumerable.Range(1, area.Steps.Count),
                area.Steps.Select(s => s.Number));
        }

        foreach (var link in TenantCustomizationGuide.RelatedLinks)
        {
            Assert.StartsWith("/", link.Href);
        }
    }
}
