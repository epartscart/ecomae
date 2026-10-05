using System.Text;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    private static readonly (string Flag, string Href, string Css, string Caption)[] UcatsCatalogueTiles =
    [
        ("ucats_shiny", "/shop/katalogi-ucats/shiny", "new-cat-block-tires", "4585"),
        ("ucats_disks", "/shop/katalogi-ucats/kolesnye-diski", "new-cat-block-disks", "4586"),
        ("ucats_accessories", "/shop/katalogi-ucats/avtoaksessuary", "new-cat-block-accessories", "4587"),
        ("ucats_to", "/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya", "new-cat-block-to", "4588"),
        ("ucats_oil", "/shop/katalogi-ucats/avtoximiya", "new-cat-block-oil", "4589"),
        ("ucats_akb", "/shop/katalogi-ucats/akkumulyatory", "new-cat-block-akb", "4590"),
        ("ucats_caps", "/shop/katalogi-ucats/kolpaki", "new-cat-block-caps", "4591"),
        ("ucats_bolty", "/shop/katalogi-ucats/kolesnye-gajki-bolty-prostavki", "new-cat-block-bolts", "4592")
    ];

    public static string UcatsCatalogues(IReadOnlyDictionary<string, string> config)
    {
        if (config.Count == 0)
        {
            return UcatsConfigMissing;
        }

        var tiles = new StringBuilder();
        foreach (var tile in UcatsCatalogueTiles)
        {
            if (!config.TryGetValue(tile.Flag, out var value) || value.Length == 0)
            {
                continue;
            }

            tiles.Append("<div class=\"col-sm-6 col-md-4 col-lg-3 new-cat-block\"><a href=\"");
            tiles.Append(tile.Href);
            tiles.Append("\" class=\"ucats-h-1 ");
            tiles.Append(tile.Css);
            tiles.Append("\"><div class=\"new-cat-block-text navbar-inverse\">");
            tiles.Append(tile.Caption);
            tiles.Append("</div></a></div>");
        }

        if (tiles.Length == 0)
        {
            return string.Empty;
        }

        return "<div class=\"row\"><div class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\"><h2 class=\"section-title\">4584</h2><div class=\"row\" style=\"margin-right:-11px; margin-left:-11px; margin-top:-9px; margin-bottom:-10px;\">"
            + tiles
            + "</div></div></div>";
    }
}
