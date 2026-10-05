using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMissingSchemaTests
{
    [Fact]
    public void IsMissing_RecognizesAbsentTablesAndColumns()
    {
        Assert.True(CpMissingSchema.IsMissing(new InvalidOperationException("Table 'docpart.shop_catalogue_products' doesn't exist")));
        Assert.True(CpMissingSchema.IsMissing(new InvalidOperationException("Unknown column 'is_frontend' in 'SELECT'")));
        Assert.True(CpMissingSchema.IsMissing(new InvalidOperationException("outer", new InvalidOperationException("Table 'docpart.content' does not exist"))));
        Assert.False(CpMissingSchema.IsMissing(new InvalidOperationException("deadlock")));
    }

    [Fact]
    public void SelectCpModules_SkipsColumnsThisTenantDoesNotHave()
    {
        var slim = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "caption", "activated", "is_prototype",
        };
        var sql = LegacySurfaceDashboardSql.SelectCpModulesForColumns(slim);
        Assert.Contains("IFNULL(`caption`, '') AS caption", sql, StringComparison.Ordinal);
        Assert.Contains("`activated`", sql, StringComparison.Ordinal);
        Assert.Contains("0 AS `is_frontend`", sql, StringComparison.Ordinal);
        Assert.Contains("0 AS `control_available`", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `is_prototype` = 0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(", `is_frontend`,", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);

        var full = LegacySurfaceDashboardSql.SelectCpModulesForColumns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "caption", "activated", "is_frontend", "is_prototype", "control_available",
        });
        Assert.Contains("`is_frontend`", full, StringComparison.Ordinal);
        Assert.Contains("`control_available`", full, StringComparison.Ordinal);
        Assert.DoesNotContain("0 AS `is_frontend`", full, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupTreeSelectSql_UsesGroupValueWhenTranslationsAreAbsent()
    {
        var slim = CpGroupTreeWriteService.GroupTreeSelectSql(false);
        Assert.Contains("FROM `groups` g", slim, StringComparison.Ordinal);
        Assert.Contains("IFNULL(g.`value`,'') AS caption", slim, StringComparison.Ordinal);
        Assert.Contains("IFNULL(g.`description`,'') AS description", slim, StringComparison.Ordinal);
        Assert.DoesNotContain("lang_text_strings_translation", slim, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", slim, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", slim, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", slim, StringComparison.OrdinalIgnoreCase);

        var translated = CpGroupTreeWriteService.GroupTreeSelectSql(true);
        Assert.Contains("LEFT JOIN `lang_text_strings_translation` tv", translated, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN `lang_text_strings_translation` td", translated, StringComparison.Ordinal);
        Assert.Contains("IFNULL(tv.`value`, IFNULL(g.`value`,'')) AS caption", translated, StringComparison.Ordinal);
    }
}
