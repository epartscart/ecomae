using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Purchase invoices must commit the AP row and the GL journal together.
/// A missing COA account refuses the write instead of leaving <c>gl_journal_id = 0</c>.
/// </summary>
public sealed class ErpPurchaseInvoiceJournalAtomicityTests
{
    [Fact]
    public async Task FailedPurchaseGlRollsBackTheInvoiceAndVoucher()
    {
        var (admin, cs) = NewDatabase();
        if (admin is null) return;
        await ExecuteAsync(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            await SeedAsync(cs);
            await ExecuteAsync(cs, "DELETE FROM `epc_erp_coa_accounts` WHERE `code` IN ('5000','2000')");
            var service = NewService(cs);

            await Assert.ThrowsAsync<ErpWriteException>(() => service.CreateAsync(
                new ErpPurchaseInvoiceInput { SupplierId = 3, AmountExVat = 80m, Note = "atomic-fail" },
                adminId: 1));

            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_purchases`"));
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_supplier_accounting`"));
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_gl_journals`"));
            Assert.Equal(0L, await ScalarAsync(cs, "SELECT IFNULL(MAX(`last_seq`),0) FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = 'PI'"));
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    [Fact]
    public async Task PurchaseInvoicePostsJournalAndStoresTheId()
    {
        var (admin, cs) = NewDatabase();
        if (admin is null) return;
        await ExecuteAsync(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            await SeedAsync(cs);
            var service = NewService(cs);
            var result = await service.CreateAsync(
                new ErpPurchaseInvoiceInput { SupplierId = 3, AmountExVat = 40m, Note = "atomic-ok" },
                adminId: 1);

            Assert.True(result.PurchaseId > 0);
            Assert.True(result.GlJournalId > 0);
            Assert.Equal(1L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `id` = " + result.PurchaseId + " AND `gl_journal_id` = " + result.GlJournalId));
            Assert.Equal(1L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_supplier_accounting` WHERE `purchase_id` = " + result.PurchaseId + " AND `gl_journal_id` = " + result.GlJournalId));
            Assert.Equal(1L, await ScalarAsync(cs, "SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `id` = " + result.GlJournalId + " AND `source_type` = 'purchase'"));
        }
        finally
        {
            await ExecuteAsync(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    private static ErpPurchaseInvoiceWriteService NewService(string cs)
    {
        var vouchers = new ErpVoucherNumberService();
        return new ErpPurchaseInvoiceWriteService(
            new Connections(cs),
            vouchers,
            new ErpTaxAmountCalculator(),
            new ErpGlPostingService(vouchers),
            new ErpAuditLogWriter());
    }

    private static async Task SeedAsync(string cs)
    {
        await using var seed = new MySqlConnection(cs);
        await seed.OpenAsync();
        await ErpGlChartOfAccountsSeeder.EnsureAsync(seed, CancellationToken.None);
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
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
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
