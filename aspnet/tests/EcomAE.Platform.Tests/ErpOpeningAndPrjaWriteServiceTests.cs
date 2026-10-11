using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Audit finding 5 (figure-changing services): live DB proof for
/// <see cref="ErpOpeningPostBatchWriteService"/> and <see cref="ErpPrjaRecognitionWriteService"/>.
/// </summary>
public sealed class ErpOpeningAndPrjaWriteServiceTests
{
    [Fact]
    public async Task OpeningBatchPostsBalancesAndFlipsStatus()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.ExecuteAsync(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_erp_coa_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(20) NOT NULL,"
                + " `name` varchar(255) NOT NULL, `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_erp_coa_accounts` (`id`,`code`,`name`) VALUES (1,'1000','Cash'),(2,'3000','Equity')");
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_erp_opening_batches` (`id` int(11) NOT NULL AUTO_INCREMENT, `as_of_date` varchar(20) NOT NULL DEFAULT '',"
                + " `reference` varchar(64) NOT NULL DEFAULT '', `status` varchar(16) NOT NULL DEFAULT 'draft',"
                + " `time_posted` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_erp_opening_lines` (`id` int(11) NOT NULL AUTO_INCREMENT, `batch_id` int(11) NOT NULL,"
                + " `line_type` varchar(24) NOT NULL DEFAULT '', `entity_id` int(11) NOT NULL DEFAULT 0,"
                + " `debit` decimal(14,2) NOT NULL DEFAULT 0, `credit` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `qty` decimal(14,4) NOT NULL DEFAULT 0, `unit_cost` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `meta_json` text, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_erp_opening_batches` (`id`,`as_of_date`,`reference`) VALUES (1,'2026-01-01','OB-1')");
            await Db.ExecuteAsync(
                cs,
                "INSERT INTO `epc_erp_opening_lines` (`batch_id`,`line_type`,`entity_id`,`debit`,`credit`) VALUES"
                + " (1,'coa',1,5000,0),(1,'coa',2,0,5000)");

            var service = new ErpOpeningPostBatchWriteService(new Db.Connections(cs));
            var result = await service.PostAsync(new ErpOpeningPostBatchWriteRequest(BatchId: 1, AdminId: 1));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(5000m, await Db.DecimalAsync(cs, "SELECT `opening_balance` FROM `epc_erp_coa_accounts` WHERE `id` = 1"));
            Assert.Equal(-5000m, await Db.DecimalAsync(cs, "SELECT `opening_balance` FROM `epc_erp_coa_accounts` WHERE `id` = 2"));
            Assert.Equal("posted", await Db.StringAsync(cs, "SELECT `status` FROM `epc_erp_opening_batches` WHERE `id` = 1"));

            var again = await service.PostAsync(new ErpOpeningPostBatchWriteRequest(BatchId: 1, AdminId: 1));
            Assert.False(again.Succeeded);
            Assert.Equal("not_found", again.Code);

            var missing = await service.PostAsync(new ErpOpeningPostBatchWriteRequest(BatchId: 99, AdminId: 1));
            Assert.False(missing.Succeeded);
            Assert.Equal("not_found", missing.Code);
        }
        finally
        {
            await Db.ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task OpeningBatchRefusesUnsupportedLineTypes()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.ExecuteAsync(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_erp_opening_batches` (`id` int(11) NOT NULL AUTO_INCREMENT, `as_of_date` varchar(20) NOT NULL DEFAULT '',"
                + " `reference` varchar(64) NOT NULL DEFAULT '', `status` varchar(16) NOT NULL DEFAULT 'draft',"
                + " `time_posted` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_erp_opening_lines` (`id` int(11) NOT NULL AUTO_INCREMENT, `batch_id` int(11) NOT NULL,"
                + " `line_type` varchar(24) NOT NULL DEFAULT '', `entity_id` int(11) NOT NULL DEFAULT 0,"
                + " `debit` decimal(14,2) NOT NULL DEFAULT 0, `credit` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `qty` decimal(14,4) NOT NULL DEFAULT 0, `unit_cost` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `meta_json` text, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_erp_opening_batches` (`id`) VALUES (7)");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_erp_opening_lines` (`batch_id`,`line_type`,`entity_id`,`debit`) VALUES (7,'item_stock',9,100)");

            var service = new ErpOpeningPostBatchWriteService(new Db.Connections(cs));
            var result = await service.PostAsync(new ErpOpeningPostBatchWriteRequest(BatchId: 7, AdminId: 1));

            Assert.False(result.Succeeded);
            Assert.Equal("unsupported_line", result.Code);
            Assert.Equal("draft", await Db.StringAsync(cs, "SELECT `status` FROM `epc_erp_opening_batches` WHERE `id` = 7"));
        }
        finally
        {
            await Db.ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task PrjaRecognitionComputesPocAndPersistsRun()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.ExecuteAsync(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_prja_budget` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `project_id` int(11) NOT NULL DEFAULT 0, `category` varchar(60) NOT NULL DEFAULT 'general',"
                + " `cost_budget` decimal(18,2) NOT NULL DEFAULT 0, `revenue_budget` decimal(18,2) NOT NULL DEFAULT 0,"
                + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_prja_recognition` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `project_id` int(11) NOT NULL DEFAULT 0, `method` varchar(16) NOT NULL DEFAULT 'poc',"
                + " `as_of` int(11) NOT NULL DEFAULT 0, `pct_complete` decimal(7,4) NOT NULL DEFAULT 0,"
                + " `recognized_revenue` decimal(18,2) NOT NULL DEFAULT 0, `recognized_cost` decimal(18,2) NOT NULL DEFAULT 0,"
                + " `wip` decimal(18,2) NOT NULL DEFAULT 0, `detail_json` text, `time_created` int(11) NOT NULL DEFAULT 0,"
                + " PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(
                cs,
                "CREATE TABLE `epc_prja_txn` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `project_id` int(11) NOT NULL DEFAULT 0, `txn_type` varchar(12) NOT NULL DEFAULT 'cost',"
                + " `category` varchar(60) NOT NULL DEFAULT 'general', `description` varchar(200) NOT NULL DEFAULT '',"
                + " `amount` decimal(18,2) NOT NULL DEFAULT 0, `txn_date` int(11) NOT NULL DEFAULT 0,"
                + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_prja_budget` (`project_id`,`cost_budget`,`revenue_budget`) VALUES (5,1000,2000)");
            await Db.ExecuteAsync(cs, "INSERT INTO `epc_prja_txn` (`project_id`,`txn_type`,`amount`) VALUES (5,'cost',400),(5,'billing',500)");

            var service = new ErpPrjaRecognitionWriteService(new Db.Connections(cs));
            var result = await service.RunAsync(new ErpPrjaRecognitionWriteRequest(ProjectId: 5, Method: "poc"));

            Assert.True(result.Succeeded, result.Message);
            // pct = 400/1000 = 0.4 → recognized revenue 800, WIP = 800 - 500 billed = 300
            Assert.Equal(0.4m, await Db.DecimalAsync(cs, "SELECT `pct_complete` FROM `epc_prja_recognition` WHERE `project_id` = 5"));
            Assert.Equal(800m, await Db.DecimalAsync(cs, "SELECT `recognized_revenue` FROM `epc_prja_recognition` WHERE `project_id` = 5"));
            Assert.Equal(400m, await Db.DecimalAsync(cs, "SELECT `recognized_cost` FROM `epc_prja_recognition` WHERE `project_id` = 5"));
            Assert.Equal(300m, await Db.DecimalAsync(cs, "SELECT `wip` FROM `epc_prja_recognition` WHERE `project_id` = 5"));

            var completedTooEarly = await service.RunAsync(new ErpPrjaRecognitionWriteRequest(ProjectId: 5, Method: "completed"));
            Assert.True(completedTooEarly.Succeeded);
            Assert.Equal(
                0m,
                await Db.DecimalAsync(cs, "SELECT `recognized_revenue` FROM `epc_prja_recognition` WHERE `project_id` = 5 AND `method` = 'completed'"));

            var invalid = await service.RunAsync(new ErpPrjaRecognitionWriteRequest(ProjectId: 0));
            Assert.False(invalid.Succeeded);
            Assert.Equal("invalid", invalid.Code);
        }
        finally
        {
            await Db.ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    private static class Db
    {
        public static (string? Admin, string Cs) New()
        {
            var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
            if (string.IsNullOrWhiteSpace(password)) return (null, string.Empty);
            var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
            return (
                "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;",
                "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;");
        }

        public static string Name(string cs) => cs.Split("Database=")[1].Split(';')[0];

        public static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task<decimal> DecimalAsync(string cs, string sql)
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            return Convert.ToDecimal(await command.ExecuteScalarAsync());
        }

        public static async Task<string?> StringAsync(string cs, string sql)
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            return Convert.ToString(await command.ExecuteScalarAsync());
        }

        public sealed class Connections : IErpWriteConnectionFactory
        {
            private readonly string _connectionString;

            public Connections(string connectionString) => _connectionString = connectionString;

            public bool IsConfigured => true;

            public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            {
                var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
        }
    }
}
