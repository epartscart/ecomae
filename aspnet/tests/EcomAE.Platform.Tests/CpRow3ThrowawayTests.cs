using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Price upload, catalogue writes, and the groups-tree delete on a database this test creates and drops.
/// The connection string names that database only. It does not open docpart.
/// </summary>
public sealed class CpRow3ThrowawayTests
{
    [Fact]
    public async Task PriceCatalogueAndGroupsSaveOnAThrowawayDatabase()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        Assert.DoesNotContain("Database=docpart", connectionString, StringComparison.OrdinalIgnoreCase);
        var root = Path.Combine(Path.GetTempPath(), "ecomae-cpw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connections = new FixedConnections(connectionString);
        var remote = new CountingRemote();
        var imports = new CpPriceImportService(connections, remote, () => new Dictionary<string, string>(), Path.Combine(root, "files"), Path.Combine(root, "work"));

        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await SeedPriceTablesAsync(connectionString);
            await ProvePriceChannelsAsync(connectionString, connections, imports, remote);
            await ProveCatalogueAsync(connectionString, connections);
            await ProveGroupsDeleteAsync(connectionString, connections);
            Assert.Equal(0, remote.Calls);
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static async Task ProvePriceChannelsAsync(
        string connectionString,
        FixedConnections connections,
        CpPriceImportService imports,
        CountingRemote remote)
    {
        var pc = await imports.ImportUploadAsync(new CpPriceImportRequest(9, "pc", 1, Csv("probe.csv")));
        Assert.True(pc.Succeeded, pc.Message);
        Assert.Equal("pc", pc.Channel);
        Assert.Equal("0986", await ScalarAsync(connectionString, "SELECT article FROM shop_docpart_prices_data WHERE price_id = 9"));
        Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices_data WHERE price_id = 9 AND article = 'OLD'"));
        Assert.Equal(0, remote.Calls);

        await using (var layout = new MySqlConnection(connectionString))
        {
            await layout.OpenAsync();
            var written = await CpPriceListConfig.SaveLayoutAsync(layout, 13, new Dictionary<string, string>
            {
                ["strings_to_left"] = "1",
                ["manufacturer_col"] = "1",
                ["article_col"] = "2",
                ["name_col"] = "3",
                ["exist_col"] = "4",
                ["price_col"] = "5",
            }, CancellationToken.None);
            Assert.Equal(1, written);
        }

        var wizard = await imports.ImportUploadAsync(new CpPriceImportRequest(13, "wizard", 1, Csv("wizard.csv")));
        Assert.True(wizard.Succeeded, wizard.Message);
        Assert.Equal("wizard", wizard.Channel);
        Assert.Equal("BOSCH", await ScalarAsync(connectionString, "SELECT manufacturer FROM shop_docpart_prices_data WHERE price_id = 13"));

        var remoteResults = await imports.ImportRemoteAsync([10, 11, 12], 1);
        Assert.Equal(3, remoteResults.Count);
        var ftp = string.Join("\n", remoteResults[0].ValidationMessages);
        Assert.Contains("ftp_host", ftp, StringComparison.Ordinal);
        Assert.Contains("ftp_username", ftp, StringComparison.Ordinal);
        Assert.Contains("email_price_sender", string.Join("\n", remoteResults[1].ValidationMessages), StringComparison.Ordinal);
        Assert.Contains("absolute http(s)", string.Join("\n", remoteResults[2].ValidationMessages), StringComparison.Ordinal);
        Assert.Equal(0, remote.Calls);
        Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_docpart_prices_data WHERE price_id IN (10, 11, 12)"));

        var now = DateTimeOffset.Now;
        var day = now.DayOfWeek == DayOfWeek.Sunday ? "7" : ((int)now.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        var time = now.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + now.Minute.ToString("00", CultureInfo.InvariantCulture);
        var cron = new CpPriceCronService(connections, imports);
        var saved = await cron.SaveAsync(null, [10], "1", [day], time);
        Assert.True(saved.Succeeded, saved.Message);
        var launches = await cron.StartDueAsync(now);
        Assert.Single(launches);
        var executed = await cron.ExecuteAsync(launches[0]);
        Assert.Contains("ftp_host", string.Join("\n", executed[0].ValidationMessages), StringComparison.Ordinal);
        Assert.Equal(0, remote.Calls);
        Assert.NotEqual("0", await ScalarAsync(connectionString, "SELECT IFNULL(time_end, 0) FROM shop_docpart_prices_cron_executor_launches WHERE id = " + launches[0].LaunchId.ToString(CultureInfo.InvariantCulture)));

        var deploy = new CpPriceDeployApiService(connections, imports, Path.Combine(Path.GetTempPath(), "unused-files"));
        var listed = await deploy.ListPricesAsync();
        Assert.Contains(listed, row => Convert.ToInt64(row["id"], CultureInfo.InvariantCulture) == 9);
        var uploaded = await deploy.UploadAsync(0, "Deploy probe", Csv("deploy.csv"));
        Assert.Equal(true, uploaded["status"]);
        var deployId = Convert.ToInt64(uploaded["price_id"], CultureInfo.InvariantCulture);
        Assert.Equal("0986", await ScalarAsync(connectionString, "SELECT article FROM shop_docpart_prices_data WHERE price_id = " + deployId.ToString(CultureInfo.InvariantCulture)));

        await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_prices");
        var missingUpload = await imports.ImportUploadAsync(new CpPriceImportRequest(9, "pc", 1, Csv("probe.csv")));
        Assert.False(missingUpload.Succeeded);
        Assert.Equal("Price lists are not in this database.", missingUpload.Message);
        Assert.DoesNotContain("doesn't exist", missingUpload.Message, StringComparison.OrdinalIgnoreCase);
        var missingRemote = await imports.ImportRemoteAsync([9], 1);
        Assert.Equal("Price lists are not in this database.", missingRemote[0].Message);
        Assert.Empty(await deploy.ListPricesAsync());
        var missingDeploy = await deploy.UploadAsync(0, "Deploy probe", Csv("deploy.csv"));
        Assert.Equal(false, missingDeploy["status"]);
        Assert.Equal("Price lists are not in this database.", missingDeploy["message"]);
        var missingSchedule = await cron.SaveAsync(null, [9], "1", [day], time);
        Assert.Equal("Price schedules are not in this database.", missingSchedule.Message);
        await ExecuteAsync(connectionString, "DROP TABLE shop_docpart_pyprices_crontab");
        Assert.Empty(await cron.ListAsync(0));
        Assert.Empty(await cron.StartDueAsync(now));
    }

    private static async Task ProveCatalogueAsync(string connectionString, FixedConnections connections)
    {
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_catalogue_products (
              id INT NOT NULL PRIMARY KEY,
              min_limit_enable TINYINT NOT NULL DEFAULT 0,
              min_limit DECIMAL(12,2) NOT NULL DEFAULT 0
            )
            """);
        await ExecuteAsync(connectionString, "INSERT INTO shop_catalogue_products (id, min_limit_enable, min_limit) VALUES (1, 0, 0)");
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_catalogue_categories_templates (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              caption VARCHAR(255) NOT NULL,
              category_object TEXT NULL,
              image LONGBLOB NULL,
              image_name VARCHAR(255) NULL
            )
            """);
        var catalogue = new CpCatalogueWriteService(connections);
        var enabled = await catalogue.SetMinLimitEnableAsync(1, 1);
        var value = await catalogue.SetMinLimitValueAsync(1, 2.5m);
        Assert.True(enabled.Succeeded, enabled.Message);
        Assert.True(value.Succeeded, value.Message);
        Assert.Equal("1", await ScalarAsync(connectionString, "SELECT min_limit_enable FROM shop_catalogue_products WHERE id = 1"));
        Assert.Equal(2.5m, decimal.Parse(await ScalarAsync(connectionString, "SELECT min_limit FROM shop_catalogue_products WHERE id = 1"), CultureInfo.InvariantCulture));

        var created = await catalogue.CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest("Pads", "{\"title\":\"Pads\"}"));
        Assert.True(created.Succeeded, created.Message);
        Assert.Equal("Pads", await ScalarAsync(connectionString, "SELECT caption FROM shop_catalogue_categories_templates WHERE id = " + created.Id.ToString(CultureInfo.InvariantCulture)));
        var deleted = await catalogue.DeleteCategoryTemplateAsync(created.Id);
        Assert.True(deleted.Succeeded, deleted.Message);
        Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM shop_catalogue_categories_templates"));

        await ExecuteAsync(connectionString, "DROP TABLE shop_catalogue_products");
        await ExecuteAsync(connectionString, "DROP TABLE shop_catalogue_categories_templates");
        var missingProduct = await catalogue.SetMinLimitEnableAsync(1, 1);
        var missingTemplate = await catalogue.CreateCategoryTemplateAsync(new CpCategoryTemplateCreateRequest("Pads", "{\"title\":\"Pads\"}"));
        var missingDelete = await catalogue.DeleteCategoryTemplateAsync(created.Id);
        Assert.Equal("Catalogue products are not in this database.", missingProduct.Message);
        Assert.Equal("Catalogue templates are not in this database.", missingTemplate.Message);
        Assert.Equal("Catalogue templates are not in this database.", missingDelete.Message);
        Assert.DoesNotContain("doesn't exist", missingProduct.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ProveGroupsDeleteAsync(string connectionString, FixedConnections connections)
    {
        await ExecuteAsync(connectionString, """
            CREATE TABLE `groups` (
              id INT NOT NULL PRIMARY KEY,
              value VARCHAR(255) NOT NULL,
              count INT NOT NULL DEFAULT 0,
              level INT NOT NULL DEFAULT 1,
              parent INT NOT NULL DEFAULT 0,
              unblocked TINYINT NOT NULL DEFAULT 1,
              for_guests TINYINT NOT NULL DEFAULT 0,
              for_registrated TINYINT NOT NULL DEFAULT 0,
              for_backend TINYINT NOT NULL DEFAULT 0,
              for_percentage TINYINT NOT NULL DEFAULT 0,
              description VARCHAR(255) NOT NULL DEFAULT '',
              `order` INT NOT NULL DEFAULT 0
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE lang_text_strings (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              description VARCHAR(255) NULL,
              same VARCHAR(255) NULL,
              is_error TINYINT NOT NULL DEFAULT 0,
              is_custom TINYINT NOT NULL DEFAULT 0,
              str_key VARCHAR(255) NOT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE lang_text_strings_translation (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              value TEXT NULL,
              str_key VARCHAR(255) NOT NULL,
              lang_code VARCHAR(16) NOT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            INSERT INTO `groups` (id, value, unblocked, for_guests, for_registrated, for_backend) VALUES
            (1, 'Guests', 1, 1, 0, 0),
            (2, 'Retail customers', 1, 0, 1, 0),
            (3, 'Administrators', 1, 0, 0, 1),
            (4, 'Wholesale', 1, 0, 0, 0)
            """);
        var groups = new CpGroupTreeWriteService(connections);
        var saved = await groups.SaveAsync(
            [
                Group(1, "Guests", 1, 0, 0),
                Group(2, "Retail customers", 0, 1, 0),
                Group(3, "Administrators", 0, 0, 1),
            ],
            "en",
            null);
        Assert.True(saved.Succeeded, saved.Message);
        Assert.Equal("0", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `groups` WHERE id = 4"));
        Assert.Equal("3", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `groups`"));
        Assert.Equal("1", await ScalarAsync(connectionString, "SELECT for_backend FROM `groups` WHERE id = 3"));
        Assert.Equal("1", await ScalarAsync(connectionString, "SELECT COUNT(*) FROM lang_text_strings_translation WHERE value = 'Guests' AND lang_code = 'en'"));

        await ExecuteAsync(connectionString, "DROP TABLE `groups`");
        var missing = await groups.SaveAsync(
            [
                Group(1, "Guests", 1, 0, 0),
                Group(2, "Retail customers", 0, 1, 0),
                Group(3, "Administrators", 0, 0, 1),
            ],
            "en",
            null);
        Assert.False(missing.Succeeded);
        Assert.Equal("Groups are not in this database.", missing.Message);
        Assert.DoesNotContain("doesn't exist", missing.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CpGroupDraft Group(long id, string value, int guests, int registered, int backend)
        => new(id, value, "", "", "", 0, 1, guests, registered, backend, 0);

    private static CpPriceUpload Csv(string fileName)
        => new(fileName, new MemoryStream(Encoding.UTF8.GetBytes("brand,article,name,qty,price\nBOSCH,0986,Pad,2,12.50\n")));

    private static async Task SeedPriceTablesAsync(string connectionString)
    {
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_prices (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              name VARCHAR(255) NOT NULL DEFAULT '',
              load_mode INT NOT NULL DEFAULT 1,
              strings_to_left INT NOT NULL DEFAULT 0,
              manufacturer_col INT NOT NULL DEFAULT 0,
              article_col INT NOT NULL DEFAULT 0,
              name_col INT NOT NULL DEFAULT 0,
              exist_col INT NOT NULL DEFAULT 0,
              price_col INT NOT NULL DEFAULT 0,
              time_to_exe_col INT NOT NULL DEFAULT 0,
              storage_col INT NOT NULL DEFAULT 0,
              min_order_col INT NOT NULL DEFAULT 0,
              clean_before VARCHAR(8) NOT NULL DEFAULT '1',
              file_name_substring VARCHAR(255) NOT NULL DEFAULT '',
              file_name_substring_arch VARCHAR(255) NOT NULL DEFAULT '',
              encoding VARCHAR(16) NOT NULL DEFAULT '',
              `separator` VARCHAR(8) NOT NULL DEFAULT '',
              ftp_host VARCHAR(255) NOT NULL DEFAULT '',
              ftp_user VARCHAR(255) NOT NULL DEFAULT '',
              ftp_password VARCHAR(255) NOT NULL DEFAULT '',
              ftp_folder VARCHAR(255) NOT NULL DEFAULT '',
              sender_email VARCHAR(255) NOT NULL DEFAULT '',
              not_mark_seen_email_messages INT NOT NULL DEFAULT 0,
              message_header_substring VARCHAR(255) NOT NULL DEFAULT '',
              link VARCHAR(512) NOT NULL DEFAULT '',
              last_updated BIGINT NOT NULL DEFAULT 0,
              records_count INT NOT NULL DEFAULT 0,
              h_time VARCHAR(16) NOT NULL DEFAULT '0'
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_prices_data (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              price_id INT NOT NULL,
              manufacturer VARCHAR(255) NOT NULL DEFAULT '',
              article VARCHAR(255) NOT NULL DEFAULT '',
              article_show VARCHAR(255) NOT NULL DEFAULT '',
              name VARCHAR(255) NOT NULL DEFAULT '',
              exist INT NOT NULL DEFAULT 0,
              price DECIMAL(15,2) NOT NULL DEFAULT 0,
              time_to_exe INT NOT NULL DEFAULT 0,
              storage VARCHAR(255) NOT NULL DEFAULT '',
              min_order INT NOT NULL DEFAULT 0
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_pyprices_tasks (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              time_created BIGINT NOT NULL,
              price_id INT NOT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_pyprices_crontab (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              active TINYINT NOT NULL,
              day_week VARCHAR(32) NOT NULL,
              month VARCHAR(8) NOT NULL,
              day_month VARCHAR(8) NOT NULL,
              hour INT NOT NULL,
              minute INT NOT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_pyprices_crontab_prices (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              price_id INT NOT NULL,
              crontab_task_id INT NOT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            CREATE TABLE shop_docpart_prices_cron_executor_launches (
              id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
              crontab_task_id INT NOT NULL,
              time_start BIGINT NOT NULL,
              time_end BIGINT NULL,
              have_pyprices_query TINYINT NOT NULL DEFAULT 0,
              list_to_handle TEXT NULL,
              error_messages TEXT NULL,
              pyprices_answer TEXT NULL
            )
            """);
        await ExecuteAsync(connectionString, """
            INSERT INTO shop_docpart_prices
              (id, name, load_mode, strings_to_left, manufacturer_col, article_col, name_col, exist_col, price_col, clean_before)
            VALUES
              (9, 'Probe', 1, 1, 1, 2, 3, 4, 5, '1'),
              (10, 'Ftp probe', 2, 1, 1, 2, 3, 4, 5, '1'),
              (11, 'Email probe', 3, 1, 1, 2, 3, 4, 5, '1'),
              (12, 'Url probe', 4, 1, 1, 2, 3, 4, 5, '1'),
              (13, 'Wizard probe', 1, 0, 0, 0, 0, 0, 0, '1')
            """);
        await ExecuteAsync(connectionString, "UPDATE shop_docpart_prices SET link = 'not-a-url' WHERE id = 12");
        await ExecuteAsync(connectionString, "INSERT INTO shop_docpart_prices_data (price_id, manufacturer, article, article_show, name, exist, price) VALUES (9, 'OLD', 'OLD', 'OLD', 'Old', 1, 1.00)");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class FixedConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }

    private sealed class CountingRemote : ICpPriceRemoteSources
    {
        public int Calls;

        public Task DownloadUrlAsync(Uri url, string destinationPath, ICollection<string> messages, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("URL download was called.");
        }

        public Task<IReadOnlyList<string>> FetchFtpAsync(CpPriceListConfig list, Func<string, bool> wanted, string targetDirectory, ICollection<string> messages, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("FTP download was called.");
        }

        public Task<IReadOnlyList<CpPriceMailMessage>> FetchMailAsync(CpPriceMailSettings settings, string sender, bool markSeen, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Email download was called.");
        }
    }
}
