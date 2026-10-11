using Xunit;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Fresh tenants have no epc_portal_site_settings table; PHP creates it lazily in
/// epc_portal_db_ensure() at the top of every portal/industry settings entry. The
/// ASP.NET industry-settings reader and writer must do the same, and must not
/// depend on an `id` column that the PHP table (PRIMARY KEY `host`) does not have.
/// </summary>
public sealed class CpPortalSchemaLazyEnsureTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Cp/CpIndustrySettingsWriteService.cs", "epc_portal_site_settings")]
    [InlineData("aspnet/src/EcomAE.Platform/Cp/CpIndustrySettingsService.cs", "epc_portal_site_settings")]
    public void Industry_settings_ensure_portal_schema_before_first_query(string relative, string table)
    {
        var source = Read(relative);
        var open = source.IndexOf("_connections.OpenAsync(cancellationToken)", StringComparison.Ordinal);
        var ensure = source.IndexOf("CpPortalSchema.EnsureSiteSettingsAsync(connection, cancellationToken)", StringComparison.Ordinal);
        var firstQuery = source.IndexOf("FROM `" + table + "`", StringComparison.Ordinal);
        if (firstQuery < 0)
        {
            firstQuery = source.IndexOf("INTO `" + table + "`", StringComparison.Ordinal);
        }

        Assert.True(open >= 0, relative + " opens a tenant connection");
        Assert.True(ensure > open, relative + " ensures portal schema after OpenAsync");
        Assert.True(firstQuery > 0, relative + " queries " + table);
        var executionOrder = source.IndexOf("ExecuteReaderAsync", ensure, StringComparison.Ordinal);
        var executionOrderWrite = source.IndexOf("ErpDb.ExecuteAsync(", StringComparison.Ordinal);
        Assert.True(
            executionOrder > ensure || executionOrderWrite > ensure,
            relative + " executes the first site-settings query only after the ensure");
    }

    [Fact]
    public void Industry_settings_reader_does_not_order_by_missing_id_column()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Cp/CpIndustrySettingsService.cs");
        Assert.DoesNotContain("ORDER BY `id`", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Portal_site_settings_ddl_matches_php()
    {
        var php = Read("content/general_pages/epc_portal_db.php");
        var cs = Read("aspnet/src/EcomAE.Platform/Cp/CpPortalSchema.cs");

        var phpDdl = Extract(php, "CREATE TABLE IF NOT EXISTS `epc_portal_site_settings`", "ENGINE=InnoDB");
        var csDdl = Extract(cs, "CREATE TABLE IF NOT EXISTS `epc_portal_site_settings`", "ENGINE=InnoDB");
        Assert.Equal(Normalize(phpDdl), Normalize(csDdl));

        foreach (var column in new[]
                 {
                     "domain_path", "contact_json", "cp_menu_json", "theme_template", "access_mode",
                     "erp_modules_json", "cp_default_lang", "country_code", "integrations_json",
                 })
        {
            Assert.True(
                php.Contains("'" + column + "' =>", StringComparison.Ordinal)
                || php.Contains("ADD COLUMN `" + column + "`", StringComparison.Ordinal),
                "PHP adds column " + column);
            Assert.Contains("(\"" + column + "\",", cs, StringComparison.Ordinal);
        }
    }

    private static string Extract(string source, string start, string end)
    {
        var s = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(s >= 0, "start marker not found: " + start);
        var e = source.IndexOf(end, s, StringComparison.Ordinal);
        Assert.True(e > s, "end marker not found: " + end);
        return source.Substring(s, e - s);
    }

    private static string Normalize(string ddl)
    {
        ddl = ddl.Replace("\\'", "'", StringComparison.Ordinal);
        ddl = Regex.Replace(ddl, "\"\\s*\\+\\s*\"", string.Empty);
        ddl = Regex.Replace(ddl, "[\"']\\s*\\.\\s*[\"']", string.Empty);
        ddl = ddl.Replace("\"", string.Empty, StringComparison.Ordinal);
        ddl = Regex.Replace(ddl, "\\s+", " ");
        ddl = Regex.Replace(ddl, "\\s*([(),])\\s*", "$1");
        return ddl.Trim().ToUpperInvariant();
    }
}
