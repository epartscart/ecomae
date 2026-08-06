using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontSearchSqlParityTests
{
    [Fact]
    public void DocpartNormalizeArticleExpr_MatchesPhpFifteenReplaceLayers()
    {
        var expr = LegacySurfaceDashboardSql.DocpartNormalizeArticleExpr("`article`");
        Assert.Equal(15, CountOccurrences(expr, "REPLACE("));
        Assert.StartsWith("UPPER(", expr, StringComparison.Ordinal);
        Assert.EndsWith("))", expr, StringComparison.Ordinal); // last REPLACE + UPPER
        Assert.Contains("CHAR(9)", expr, StringComparison.Ordinal);
        Assert.Contains("CHAR(92)", expr, StringComparison.Ordinal);

        var depth = 0;
        foreach (var ch in expr)
        {
            if (ch == '(')
            {
                depth++;
            }

            if (ch == ')')
            {
                depth--;
            }

            Assert.True(depth >= 0);
        }

        Assert.Equal(0, depth);
    }

    [Fact]
    public void PriceArticleMatch_PrimaryUsesArticleSearchWithoutOrReplace()
    {
        var primary = LegacySurfaceDashboardSql.StorefrontPriceArticleMatchSql(hasArticleSearchColumn: true);
        var fallback = LegacySurfaceDashboardSql.StorefrontPriceArticleMatchSql(
            hasArticleSearchColumn: true,
            useReplaceFallback: true);
        var without = LegacySurfaceDashboardSql.StorefrontPriceArticleMatchSql(hasArticleSearchColumn: false);

        Assert.Contains("article_search", primary, StringComparison.Ordinal);
        Assert.DoesNotContain("REPLACE(", primary, StringComparison.Ordinal);
        Assert.Equal("(d.`article_search` = @article)", primary);

        Assert.Contains("REPLACE(", fallback, StringComparison.Ordinal);
        Assert.DoesNotContain("article_search", fallback, StringComparison.Ordinal);
        Assert.Contains("REPLACE(", without, StringComparison.Ordinal);
        Assert.DoesNotContain("article_search", without, StringComparison.Ordinal);

        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("{CROSS_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        // Brand discovery / offers must not require price > 0 (PHP CHPU / prices_enclosure do not).
        Assert.DoesNotContain("price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.DoesNotContain("price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("storefront_temp_disabled", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
    }

    [Fact]
    public void PriceArticleSearchInSql_BuildsIndexedPlaceholders()
    {
        var sql = LegacySurfaceDashboardSql.StorefrontPriceArticleSearchInSql(3);
        Assert.Equal("(d.`article_search` IN (@a0,@a1,@a2))", sql);
        Assert.Equal("0", LegacySurfaceDashboardSql.StorefrontPriceArticleSearchInSql(0));
        var exact = LegacySurfaceDashboardSql.StorefrontPriceArticleExactInSql(2);
        Assert.Contains("@a0,@a1", exact, StringComparison.Ordinal);
        Assert.Contains("@b0,@b1", exact, StringComparison.Ordinal);
        Assert.Contains("@c0,@c1", exact, StringComparison.Ordinal);
        Assert.Contains("UPPER(TRIM(IFNULL(d.`article`", exact, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAR(9)", exact, StringComparison.Ordinal);
        Assert.Contains("@article", LegacySurfaceDashboardSql.StorefrontPriceArticleSimpleEqualitySql, StringComparison.Ordinal);
        Assert.Contains("article_show", LegacySurfaceDashboardSql.StorefrontPriceArticleSimpleEqualitySql, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossMatch_PrefersSearchColumnsWhenProbed()
    {
        var indexed = LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearchColumns: true);
        var replace = LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearchColumns: false);
        Assert.Contains("article_search", indexed, StringComparison.Ordinal);
        Assert.Contains("analog_search", indexed, StringComparison.Ordinal);
        Assert.DoesNotContain("REPLACE(", indexed, StringComparison.Ordinal);
        Assert.Contains("REPLACE(", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("article_search", replace, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }
}
