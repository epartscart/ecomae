using System;
using System.Linq;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpShortcutCatalogTests
{
    [Fact]
    public void Auto_parts_defaults_match_php_dashboard_list()
    {
        Assert.Equal(
            new[] { "orders", "prices", "multivendor", "crosses", "procurement", "pos", "erp", "stock" },
            CpShortcutCatalog.DefaultsFor("auto_parts").ToArray());
    }

    [Theory]
    [InlineData("jewellery")]
    [InlineData("fashion")]
    [InlineData(null)]
    public void Non_auto_parts_industries_use_the_generic_php_defaults(string? industry)
    {
        Assert.Equal(
            new[] { "orders", "catalogue", "prices", "clients", "accessories", "erp", "documents", "settings" },
            CpShortcutCatalog.DefaultsFor(industry).ToArray());
    }

    [Fact]
    public void Default_tiles_resolve_every_default_key_from_the_catalogue()
    {
        foreach (var industry in new string?[] { "auto_parts", "jewellery" })
        {
            var tiles = CpShortcutCatalog.DefaultTiles(industry);
            Assert.Equal(CpShortcutCatalog.DefaultsFor(industry).Count, tiles.Count);
            Assert.All(tiles, t => Assert.False(string.IsNullOrWhiteSpace(t.Label)));
            Assert.All(tiles, t => Assert.False(string.IsNullOrWhiteSpace(t.Url)));
            Assert.All(tiles, t => Assert.False(string.IsNullOrWhiteSpace(t.Tone)));
        }
    }

    [Fact]
    public void Catalogue_keys_are_unique()
    {
        var keys = CpShortcutCatalog.All.Select(i => i.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalogue_targets_are_safe_relative_cp_or_erp_urls()
    {
        foreach (var item in CpShortcutCatalog.All)
        {
            Assert.StartsWith("/", item.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("javascript:", item.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data:", item.Url, StringComparison.OrdinalIgnoreCase);
            // Tenant CP must never deep-link the Super CP fleet console.
            Assert.DoesNotContain("/super-cp", item.Url, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Icon_classes_are_font_awesome_prefixed_and_bare_icon_strips_the_prefix()
    {
        foreach (var item in CpShortcutCatalog.All)
        {
            Assert.StartsWith("fa ", item.IconClass, StringComparison.Ordinal);
            Assert.Equal(item.IconClass[3..], item.BareIcon);
        }
    }

    [Fact]
    public void Find_is_case_insensitive_and_returns_null_for_unknown_keys()
    {
        Assert.NotNull(CpShortcutCatalog.Find("ORDERS"));
        Assert.Null(CpShortcutCatalog.Find("not_a_shortcut"));
        Assert.Null(CpShortcutCatalog.Find(null));
    }

    [Fact]
    public void Tone_falls_back_to_the_cycling_palette_for_custom_shortcuts()
    {
        Assert.Equal("red", CpShortcutCatalog.ToneFor("orders", 3));
        Assert.Contains(CpShortcutCatalog.ToneFor("custom_link", 3), new[] { "red", "black", "crimson", "stone" });
        Assert.Equal(CpShortcutCatalog.ToneFor("custom_link", 3), CpShortcutCatalog.ToneFor("another_custom", 3));
    }
}
