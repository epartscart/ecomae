using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Audit finding 5: live DB coverage for the remaining untested write services.</summary>
public sealed class ErpUntestedWriteServicesTests
{
    [Fact]
    public async Task PettyCashCreatesFloatRowWithLedgerAccount()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.Exec(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_erp_petty_cash` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(120) NOT NULL,"
                + " `account_id` int(11) NOT NULL DEFAULT 0, `float_amount` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `custodian_user_id` int(11) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");

            var service = new ErpPettyCashWriteService(new Db.Connections(cs), new StubLedger());
            var result = await service.SaveAsync(new ErpPettyCashWriteRequest(Name: "Office float", FloatAmount: 250m, CustodianUserId: 3), 1);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("Office float", await Db.Str(cs, "SELECT `name` FROM `epc_erp_petty_cash` WHERE `id` = 1"));
            Assert.Equal(9L, await Db.Dec(cs, "SELECT `account_id` FROM `epc_erp_petty_cash` WHERE `id` = 1"));
            Assert.Equal(250m, await Db.Dec(cs, "SELECT `float_amount` FROM `epc_erp_petty_cash` WHERE `id` = 1"));

            var invalid = await service.SaveAsync(new ErpPettyCashWriteRequest(Name: " "), 1);
            Assert.False(invalid.Succeeded);
        }
        finally
        {
            await Db.Exec(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task PmListingSaveInsertsWithGeneratedRefAndUpdates()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.Exec(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_erp_pm_listings` (`id` int(11) NOT NULL AUTO_INCREMENT, `ref_no` varchar(32) NOT NULL DEFAULT '',"
                + " `resource_type` varchar(24) NOT NULL DEFAULT '', `title` varchar(200) NOT NULL DEFAULT '',"
                + " `description` text, `qty` double NOT NULL DEFAULT 0, `rate` double NOT NULL DEFAULT 0,"
                + " `amount` double NOT NULL DEFAULT 0, `voucher_ref` varchar(64) NOT NULL DEFAULT '',"
                + " `status` varchar(16) NOT NULL DEFAULT 'open', `active` tinyint(1) NOT NULL DEFAULT 1,"
                + " `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");

            var service = new ErpPmListingSaveWriteService(new Db.Connections(cs));
            var created = await service.SaveAsync(new ErpPmListingSaveRequest(Title: "Compressor hire", Qty: 2, Rate: 150));

            Assert.True(created.Ok, created.Message);
            Assert.Matches("^LST-", created.RefNo);
            Assert.Equal(300, await Db.Dec(cs, "SELECT `amount` FROM `epc_erp_pm_listings` WHERE `id` = 1"));

            var updated = await service.SaveAsync(new ErpPmListingSaveRequest(Id: 1, Title: "Compressor hire x3", Qty: 3, Rate: 150, Status: "posted"));
            Assert.True(updated.Ok, updated.Message);
            Assert.Equal(450, await Db.Dec(cs, "SELECT `amount` FROM `epc_erp_pm_listings` WHERE `id` = 1"));
            Assert.Equal("posted", await Db.Str(cs, "SELECT `status` FROM `epc_erp_pm_listings` WHERE `id` = 1"));
        }
        finally
        {
            await Db.Exec(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task QmOrderCreatePersistsOpenOrderAndAudits()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.Exec(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_qm_order` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `plan_id` int(11) NOT NULL DEFAULT 0, `ref_type` varchar(32) NOT NULL DEFAULT '',"
                + " `ref_id` varchar(64) NOT NULL DEFAULT '', `item_id` int(11) NOT NULL DEFAULT 0,"
                + " `qty` decimal(14,2) NOT NULL DEFAULT 0, `status` varchar(16) NOT NULL DEFAULT '',"
                + " `verdict` varchar(32) NOT NULL DEFAULT '', `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");

            var service = new ErpQmOrderCreateWriteService(new Db.Connections(cs), new StubAudit());
            var result = await service.CreateAsync(
                new ErpQmOrderCreateWriteRequest(CompanyId: 2, PlanId: 4, ItemId: 77, Qty: 12.5m, ActorUserId: 9));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("open", await Db.Str(cs, "SELECT `status` FROM `epc_qm_order` WHERE `id` = 1"));
            Assert.Equal(12.5m, await Db.Dec(cs, "SELECT `qty` FROM `epc_qm_order` WHERE `id` = 1"));
            Assert.Single(StubAudit.Last);

            var invalid = await service.CreateAsync(new ErpQmOrderCreateWriteRequest(CompanyId: 0));
            Assert.False(invalid.Succeeded);
        }
        finally
        {
            await Db.Exec(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task RfqResponseSavesAgainstExistingLineOnly()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.Exec(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_scm_rfq_lines` (`id` int(11) NOT NULL AUTO_INCREMENT, `rfq_id` int(11) NOT NULL, PRIMARY KEY (`id`))");
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_scm_rfq_responses` (`id` int(11) NOT NULL AUTO_INCREMENT, `rfq_id` int(11) NOT NULL,"
                + " `rfq_line_id` int(11) NOT NULL, `supplier_id` int(11) NOT NULL, `unit_price` decimal(18,4) NOT NULL DEFAULT 0,"
                + " `lead_time_days` int(11) NOT NULL DEFAULT 0, `notes` text, `time_created` int(11) NOT NULL DEFAULT 0,"
                + " PRIMARY KEY (`id`))");
            await Db.Exec(cs, "INSERT INTO `epc_scm_rfq_lines` (`id`,`rfq_id`) VALUES (10,5)");

            var service = new ErpRfqResponseWriteService(new Db.Connections(cs));
            var ok = await service.AddAsync(new ErpRfqResponseWriteRequest(RfqId: 5, RfqLineId: 10, SupplierId: 3, UnitPrice: 42.5m, LeadTimeDays: 7));

            Assert.True(ok.Succeeded, ok.Message);
            Assert.Equal(42.5m, await Db.Dec(cs, "SELECT `unit_price` FROM `epc_scm_rfq_responses` WHERE `id` = 1"));

            var missingLine = await service.AddAsync(new ErpRfqResponseWriteRequest(RfqId: 5, RfqLineId: 99, SupplierId: 3, UnitPrice: 1m));
            Assert.False(missingLine.Succeeded);
            Assert.Equal("invalid", missingLine.Code);

            var negative = await service.AddAsync(new ErpRfqResponseWriteRequest(RfqId: 5, RfqLineId: 10, SupplierId: 3, UnitPrice: -1m));
            Assert.False(negative.Succeeded);
        }
        finally
        {
            await Db.Exec(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    [Fact]
    public async Task RtlPosSaleWritesTxnAndLines()
    {
        var (admin, cs) = Db.New();
        if (admin is null) return;
        await Db.Exec(admin, "CREATE DATABASE `" + Db.Name(cs) + "`");
        try
        {
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_rtl_txn` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `channel_id` int(11) NOT NULL DEFAULT 0, `receipt_no` varchar(32) NOT NULL DEFAULT '',"
                + " `gross` decimal(14,2) NOT NULL DEFAULT 0, `discount` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `net` decimal(14,2) NOT NULL DEFAULT 0, `tax` decimal(14,2) NOT NULL DEFAULT 0,"
                + " `total` decimal(14,2) NOT NULL DEFAULT 0, `tender_type` varchar(16) NOT NULL DEFAULT 'cash',"
                + " `txn_time` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_rtl_txn_line` (`id` int(11) NOT NULL AUTO_INCREMENT, `txn_id` int(11) NOT NULL,"
                + " `item_id` int(11) NOT NULL, `qty` double NOT NULL DEFAULT 0, `unit_price` double NOT NULL DEFAULT 0,"
                + " `line_discount` double NOT NULL DEFAULT 0, `line_net` double NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_rtl_discount` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0,"
                + " `channel_id` int(11) NOT NULL DEFAULT 0, `disc_type` varchar(16) NOT NULL DEFAULT '',"
                + " `value` double NOT NULL DEFAULT 0, `active` tinyint(1) NOT NULL DEFAULT 1,"
                + " `starts` int(11) NOT NULL DEFAULT 0, `ends` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`))");
            await Db.Exec(
                cs,
                "CREATE TABLE `epc_erp_pm_legal_entities` (`id` int(11) NOT NULL AUTO_INCREMENT, `active` tinyint(1) NOT NULL DEFAULT 1, PRIMARY KEY (`id`))");
            await Db.Exec(cs, "INSERT INTO `epc_erp_pm_legal_entities` (`id`,`active`) VALUES (1,1)");

            var service = new ErpRtlPosSaleWriteService(new Db.Connections(cs));
            var result = await service.SaleAsync(
                new ErpRtlPosSaleRequest(CompanyId: 1, ChannelId: 1, Tender: "card", TaxRate: 5, Lines:
                [
                    new ErpRtlPosSaleLine(7, 2, 100),
                    new ErpRtlPosSaleLine(8, 1, 50),
                ]));

            Assert.True(result.Ok, result.Message);
            Assert.Equal(250m, result.Gross);
            Assert.Equal(2, await Db.Dec(cs, "SELECT COUNT(*) FROM `epc_rtl_txn_line` WHERE `txn_id` = 1"));
            Assert.Equal(1m, await Db.Dec(cs, "SELECT `company_id` FROM `epc_rtl_txn` WHERE `id` = 1"));
        }
        finally
        {
            await Db.Exec(admin, "DROP DATABASE IF EXISTS `" + Db.Name(cs) + "`");
        }
    }

    private sealed class StubLedger : IErpGlLedgerWriteService
    {
        public Task<ErpJournalResult> ManualJournalAsync(ErpManualJournalInput input, int adminId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ErpReversedJournalResult> ReverseJournalAsync(long journalId, long reverseDate, string note, int adminId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ErpReversedJournalResult> ReverseJournalAsync(DbConnection connection, long journalId, long reverseDate, string note, int adminId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<long> CreateCoaAccountAsync(ErpCoaAccountInput input, int adminId, CancellationToken ct = default)
            => Task.FromResult(9L);

        public Task<long> CreateCashAccountAsync(ErpCashAccountInput input, int adminId, CancellationToken ct = default)
            => Task.FromResult(9L);
    }

    private sealed class StubAudit : IErpAuditLogWriter
    {
        public static readonly List<string> Last = [];

        public Task LogAsync(
            DbConnection connection,
            DbTransaction? transaction,
            int adminId,
            string action,
            string entityType,
            long entityId,
            string summary,
            IReadOnlyDictionary<string, string?>? detail,
            CancellationToken cancellationToken = default)
        {
            Last.Clear();
            Last.Add(action);
            return Task.CompletedTask;
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

        public static async Task Exec(string connectionString, string sql)
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task<decimal> Dec(string cs, string sql)
        {
            await using var connection = new MySqlConnection(cs);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            return Convert.ToDecimal(await command.ExecuteScalarAsync());
        }

        public static async Task<string?> Str(string cs, string sql)
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
