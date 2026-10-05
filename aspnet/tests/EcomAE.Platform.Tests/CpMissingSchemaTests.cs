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

    [Fact]
    public void SearchTabSelectSql_SkipsTheTranslationTableWhenItIsAbsent()
    {
        var slim = CpSearchTabEditorService.SearchTabSelectSql(false);
        Assert.Contains("FROM `shop_docpart_search_tabs` t", slim, StringComparison.Ordinal);
        Assert.Contains(", '' FROM", slim, StringComparison.Ordinal);
        Assert.DoesNotContain("lang_text_strings_translation", slim, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", slim, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", slim, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", slim, StringComparison.OrdinalIgnoreCase);

        var translated = CpSearchTabEditorService.SearchTabSelectSql(true);
        Assert.Contains("lang_text_strings_translation", translated, StringComparison.Ordinal);
        Assert.Contains("x.`lang_code` = ?", translated, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficeSelectSql_SkipsColumnsThisTenantDoesNotHave()
    {
        var slim = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "caption", "country", "city", "address", "phone", "email", "users",
        };
        var sql = CpOfficeEditorService.OfficeSelectSql(slim);
        Assert.Contains("IFNULL(`caption`,'')", sql, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`city`,'')", sql, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`email`,'')", sql, StringComparison.Ordinal);
        Assert.Contains("''", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`region`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`description`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`timetable`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`coordinates`", sql, StringComparison.Ordinal);
        Assert.Contains("FROM `shop_offices` WHERE `id` = ?", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);

        var full = CpOfficeEditorService.OfficeSelectSql(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "caption", "country", "region", "city", "address", "description", "timetable", "phone", "email", "coordinates", "users",
        });
        Assert.Contains("IFNULL(`region`,'')", full, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`coordinates`,'')", full, StringComparison.Ordinal);
    }

    [Fact]
    public void UserOpenSelectSql_SkipsRegistrationColumnsThisTenantDoesNotHave()
    {
        var slim = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "user_id", "email", "phone", "password", "email_confirmed", "phone_confirmed", "unlocked", "name",
        };
        var sql = CpUserEditorService.UserOpenSelectSql(slim);
        Assert.Contains("IFNULL(`email`,'')", sql, StringComparison.Ordinal);
        Assert.Contains("`email_confirmed`", sql, StringComparison.Ordinal);
        Assert.Contains("`unlocked`", sql, StringComparison.Ordinal);
        Assert.Contains("FROM `users` WHERE `user_id` = ?", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`reg_variant`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`comment`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("users_profiles", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);

        var full = CpUserEditorService.UserOpenSelectSql(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reg_variant", "email", "email_confirmed", "phone", "phone_confirmed", "unlocked", "comment",
        });
        Assert.Contains("`reg_variant`", full, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`comment`,'')", full, StringComparison.Ordinal);
    }

    [Fact]
    public void StorageInt_TreatsAnEmptyStringAsZero()
    {
        Assert.Equal(0, CpStorageEditorService.StorageInt(""));
        Assert.Equal(0, CpStorageEditorService.StorageInt(DBNull.Value));
        Assert.Equal(784, CpStorageEditorService.StorageInt(784));
        Assert.Equal(2, CpStorageEditorService.StorageInt("2"));
    }
}
