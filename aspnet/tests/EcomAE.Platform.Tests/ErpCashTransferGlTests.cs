using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCashTransferGlTests
{
    [Fact]
    public async Task TransferPostsThroughCashInTransit_AndLeavesRevenueAndExpenseUntouched()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await ExecuteAsync(admin, "CREATE DATABASE `" + database + "`");
        try
        {
            await ExecuteAsync(
                connectionString,
                "CREATE TABLE `epc_erp_cash_bank_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(255) NOT NULL,"
                + " `account_type` enum('cash','bank') NOT NULL DEFAULT 'cash', `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,"
                + " `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await ExecuteAsync(connectionString, "INSERT INTO `epc_erp_cash_bank_accounts` (`id`, `name`, `account_type`) VALUES (1, 'Till', 'cash'), (2, 'Main bank', 'bank')");
            await using (var seed = new MySqlConnection(connectionString))
            {
                await seed.OpenAsync();
                await ErpGlChartOfAccountsSeeder.EnsureAsync(seed, CancellationToken.None);
            }

            await ExecuteAsync(connectionString, "DELETE FROM `epc_erp_coa_accounts` WHERE `code` = '1090'");

            var gl = new ErpGlPostingService(new ErpVoucherNumberService());
            var service = new ErpCashWriteService(
                new Connections(connectionString),
                new ErpVoucherNumberService(),
                gl,
                new ErpAuditLogWriter(),
                new ErpSettlementAllocationService(),
                new ErpAdvanceVatService(gl));

            var result = await service.TransferVoucherAsync(
                new ErpTransferVoucherInput { FromAccountId = 2, ToAccountId = 1, Amount = 250m, Note = "Float top-up" },
                adminId: 1);

            Assert.StartsWith("TV-", result.VoucherNo, StringComparison.Ordinal);
            var lines = await LinesAsync(connectionString);
            Assert.Equal(
                [
                    ("1000", 250.00m, 0.00m),
                    ("1010", 0.00m, 250.00m),
                    ("1090", 0.00m, 250.00m),
                    ("1090", 250.00m, 0.00m),
                ],
                lines.OrderBy(l => l.Code, StringComparer.Ordinal).ThenBy(l => l.Debit).ToArray());
            Assert.DoesNotContain(lines, l => l.Code is "4000" or "6100");
            Assert.Equal(0m, lines.Where(l => l.Code == "1090").Sum(l => l.Debit - l.Credit));
            Assert.Equal(
                2L,
                await ScalarAsync(connectionString, "SELECT COUNT(*) FROM `epc_erp_cash_bank_entries` WHERE `gl_journal_id` > 0 AND `voucher_no` = '" + result.VoucherNo + "'"));
            Assert.Equal(
                "asset",
                await StringAsync(connectionString, "SELECT `account_type` FROM `epc_erp_coa_accounts` WHERE `code` = '1090'"));
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private static async Task<List<(string Code, decimal Debit, decimal Credit)>> LinesAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "SELECT c.`code`, l.`debit`, l.`credit` FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_coa_accounts` c ON c.`id` = l.`coa_id`",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string, decimal, decimal)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        }

        return rows;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
        => Convert.ToInt64(await RawScalarAsync(connectionString, sql));

    private static async Task<string?> StringAsync(string connectionString, string sql)
        => Convert.ToString(await RawScalarAsync(connectionString, sql));

    private static async Task<object?> RawScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private sealed class Connections : IErpWriteConnectionFactory
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
