using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOnlyShopBootstrapTests
{
    [Fact]
    public void Source_seeds_shop_with_registry_cs_not_invented_user()
    {
        var bootstrap = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Cp/ErpOnlyShopBootstrap.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `users`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `sessions`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("`type` INT NOT NULL DEFAULT 0", bootstrap, StringComparison.Ordinal);
        Assert.Contains("`contact_type`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `groups`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `users_groups_bind`", bootstrap, StringComparison.Ordinal);
        Assert.Contains("OpenAsync(dbName, userName: null, password: null", bootstrap, StringComparison.Ordinal);
        Assert.Contains("do not remap TenantRegistry to PHP", bootstrap, StringComparison.Ordinal);
        Assert.Contains("LegacyLoginSecurity.HashPassword", bootstrap, StringComparison.Ordinal);
        Assert.Contains("replacePassword", bootstrap, StringComparison.Ordinal);
        Assert.Contains("EnsureOperatorLoginAsync", bootstrap, StringComparison.Ordinal);
        Assert.Contains("for_backend", bootstrap, StringComparison.Ordinal);
        Assert.Contains("EnsureIndustryProfileAsync", bootstrap, StringComparison.Ordinal);
        Assert.Contains("erp_industry_profile", bootstrap, StringComparison.Ordinal);
        Assert.Contains("industry_pack", bootstrap, StringComparison.Ordinal);
        Assert.Contains("ErpJewelSchema.EnsureAsync", bootstrap, StringComparison.Ordinal);

        var factory = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Data/MySqlTenantDbConnectionFactory.cs"));
        Assert.Contains("HasUsableDedicatedCredentials", factory, StringComparison.Ordinal);
        Assert.Contains("Empty dedicated password", factory, StringComparison.Ordinal);

        var login = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Auth/DbLegacyAdminLoginService.cs"));
        Assert.Contains("EnsureForSiteKeyAsync", login, StringComparison.Ordinal);
        Assert.Contains("clientErpBound", login, StringComparison.Ordinal);

        var onboard = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Cp/CpTenantsWriteService.cs"));
        Assert.Contains("EnsureShopAsync", onboard, StringComparison.Ordinal);
        Assert.Contains("\"\",\n                \"\",\n                \"ERP-only. No public storefront.\"", onboard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureSchemaAndOperator_on_empty_throwaway_database()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_erpshop_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        try
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await ErpOnlyShopBootstrap.EnsureSchemaAsync(connection, CancellationToken.None);
            Assert.True(await ErpOnlyShopBootstrap.EnsureOperatorAsync(
                connection, "indj@ecomae.com", "TempShopPass9", CancellationToken.None));
            Assert.True(await ErpOnlyShopBootstrap.EnsureOperatorAsync(
                connection, "indj@ecomae.com", "ShouldNotOverwrite", CancellationToken.None, replacePassword: false));

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `password`, `email_confirmed`, `unlocked` FROM `users` WHERE `email`='indj@ecomae.com'";
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                var hash = reader.GetString(0);
                Assert.True(LegacyPasswordVerifier.Verify("TempShopPass9", hash, secretSuccession: "unused"));
                Assert.False(LegacyPasswordVerifier.Verify("ShouldNotOverwrite", hash, secretSuccession: "unused"));
                Assert.Equal(1, reader.GetInt32(1));
                Assert.Equal(1, reader.GetInt32(2));
            }

            command.CommandText = "SELECT COUNT(*) FROM `users_groups_bind` b INNER JOIN `groups` g ON g.`id`=b.`group_id` WHERE g.`for_backend`=1";
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));

            command.CommandText = "SHOW COLUMNS FROM `sessions` LIKE 'type'";
            Assert.NotNull(await command.ExecuteScalarAsync());

            await ErpOnlyShopBootstrap.EnsureIndustryProfileAsync(connection, "jewellery", CancellationToken.None);
            command.CommandText = "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key`='erp_industry_profile'";
            Assert.Equal("jewellery", Convert.ToString(await command.ExecuteScalarAsync()));
            command.CommandText = "SELECT `setting_value` FROM `epc_org_company_settings` WHERE `setting_key`='industry_pack' LIMIT 1";
            Assert.Equal("jewellery_diamond", Convert.ToString(await command.ExecuteScalarAsync()));
            command.CommandText = "SHOW TABLES LIKE 'epc_jewel_karat_master'";
            Assert.NotNull(await command.ExecuteScalarAsync());

            await ErpOnlyShopBootstrap.EnsureIndustryProfileAsync(connection, "auto_parts", CancellationToken.None);
            command.CommandText = "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key`='erp_industry_profile'";
            Assert.Equal("jewellery", Convert.ToString(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string Find(string relative)
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
}
