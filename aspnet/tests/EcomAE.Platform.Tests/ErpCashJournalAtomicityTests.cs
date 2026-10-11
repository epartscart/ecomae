using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Audit findings 1+4: a cash/bank write must commit entry and GL journal atomically
/// (no committed row with <c>gl_journal_id = 0</c>), and voucher numbers must not be
/// burned by a failed write — the sequence rolls back with the document transaction.
/// </summary>
public sealed class ErpCashJournalAtomicityTests
{
    [Fact]
    public async Task FailedGlPostingRollsBackEntryAndVoucher()
    {
        var (admin, cs) = NewDatabase();
        if (admin is null) return;
        await ExecuteAsync(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            await SeedAsync(cs);
            // Break GL: the revenue leg's COA is absent, so PostCashEntryAsync must throw.
            await ExecuteAsync(cs, "DELETE FROM `epc_erp_coa_accounts` WHERE `code` IN ('4000','1100')");
            var service = NewService(cs);

            await Assert.ThrowsAsync<ErpWriteException>(() => service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 120m, Direction = true, CounterpartyType = "customer" },
                adminId: 1));

            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_cash_bank_entries`"));
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_gl_journals`"));
            // The receipt sequence was consumed inside the rolled-back transaction.
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT IFNULL(MAX(`last_seq`),0) FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = 'RV'"));
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    [Fact]
    public async Task RetryAfterFailureReusesTheVoucherSequence()
    {
        var (admin, cs) = NewDatabase();
        if (admin is null) return;
        await ExecuteAsync(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            await SeedAsync(cs);
            var service = NewService(cs);

            var first = await service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 50m, Direction = true, CounterpartyType = "customer" },
                adminId: 1);

            await ExecuteAsync(cs, "DELETE FROM `epc_erp_coa_accounts` WHERE `code` IN ('4000','1100')");
            await Assert.ThrowsAsync<ErpWriteException>(() => service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 75m, Direction = true, CounterpartyType = "customer" },
                adminId: 1));

            var lastSeq = await ScalarAsync(cs, "SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = 'RV'");
            var rebuilt = first.VoucherNo.Split('-');
            var nextExpected = rebuilt[0] + "-" + rebuilt[1] + "-" + (int.Parse(rebuilt[2]) + 1).ToString().PadLeft(rebuilt[2].Length, '0');
            await Assert.ThrowsAsync<ErpWriteException>(() => service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 75m, Direction = true, CounterpartyType = "customer" },
                adminId: 1));
            Assert.Equal(lastSeq, await ScalarAsync(cs, "SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = 'RV'"));

            await ExecuteAsync(
                cs,
                "INSERT INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`system_flag`,`active`,`time_created`)"
                + " VALUES ('4000','Revenue','revenue','credit',1,1,UNIX_TIMESTAMP()),('1100','Accounts receivable','asset','debit',1,1,UNIX_TIMESTAMP())");
            var second = await service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 75m, Direction = true, CounterpartyType = "customer" },
                adminId: 1);
            Assert.Equal(nextExpected, second.VoucherNo);
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_cash_bank_entries` WHERE `gl_journal_id` = 0 AND `active` = 1"));
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    [Fact]
    public async Task AccountingScanFlagsOrphanEntriesAndUnbalancedJournals()
    {
        var (admin, cs) = NewDatabase();
        if (admin is null) return;
        await ExecuteAsync(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            await SeedAsync(cs);
            var service = NewService(cs);
            await service.CashEntryAsync(
                new ErpCashEntryInput { AccountId = 1, Amount = 10m, Direction = true, CounterpartyType = "customer" },
                adminId: 1);

            var integrity = new ErpIntegrityService(new Connections(cs));
            var clean = await integrity.ScanAccountingAsync();
            Assert.All(clean, r => Assert.Equal("clean", r.Status));
            Assert.Contains(clean, r => r.Check == "gl_unbalanced");
            Assert.Contains(clean, r => r.Check == "cash_entries_missing_journal");
            Assert.Contains(clean, r => r.Check == "cash_control_1000_1010");

            await ExecuteAsync(cs, "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`,`time`,`entry_type`,`direction`,`amount`,`voucher_no`,`admin_id`) VALUES (1,UNIX_TIMESTAMP(),'receipt',1,5,'RV-ORPHAN',1)");
            await ExecuteAsync(
                cs,
                "INSERT INTO `epc_erp_gl_journals` (`journal_no`,`journal_date`,`source_type`,`source_id`,`company_id`,`active`,`admin_id`)"
                + " VALUES ('GV-BAD',UNIX_TIMESTAMP(),'manual',0,1,1,1)");
            var coa = await ScalarAsync(cs, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = '1000'");
            var jid = await ScalarAsync(cs, "SELECT `id` FROM `epc_erp_gl_journals` WHERE `journal_no` = 'GV-BAD'");
            await ExecuteAsync(cs, "INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (" + jid + "," + coa + ",9,0,'unbalanced')");

            var dirty = await integrity.ScanAccountingAsync();
            Assert.Equal("dirty", dirty.Single(r => r.Check == "gl_unbalanced").Status);
            Assert.Equal("dirty", dirty.Single(r => r.Check == "cash_entries_missing_journal").Status);
            Assert.Equal("dirty", dirty.Single(r => r.Check == "cash_control_1000_1010").Status);
            Assert.NotEqual(0m, dirty.Single(r => r.Check == "cash_control_1000_1010").Variance);
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    private static async Task SeedAsync(string cs)
    {
        await ExecuteAsync(
            cs,
            "CREATE TABLE `epc_erp_cash_bank_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(255) NOT NULL,"
            + " `account_type` enum('cash','bank') NOT NULL DEFAULT 'cash', `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
        await ExecuteAsync(cs, "INSERT INTO `epc_erp_cash_bank_accounts` (`id`, `name`, `account_type`) VALUES (1, 'Till', 'cash')");
        await using var seed = new MySqlConnection(cs);
        await seed.OpenAsync();
        await ErpGlChartOfAccountsSeeder.EnsureAsync(seed, CancellationToken.None);
    }

    private static ErpCashWriteService NewService(string cs)
    {
        var gl = new ErpGlPostingService(new ErpVoucherNumberService());
        return new ErpCashWriteService(
            new Connections(cs),
            new ErpVoucherNumberService(),
            gl,
            new ErpAuditLogWriter(),
            new ErpSettlementAllocationService(),
            new ErpAdvanceVatService(gl));
    }

    private static (string? Admin, string Cs) NewDatabase()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password)) return (null, string.Empty);
        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        return (
            "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;",
            "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;");
    }

    private static string DbName(string cs) => cs.Split("Database=")[1].Split(';')[0];

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
        => Convert.ToInt64(await RawScalarAsync(connectionString, sql));

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
