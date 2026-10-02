using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlManualJournalIdempotencyTests
{
    [Fact]
    public async Task ManualJournalReplayReturnsTheOriginalJournalWithoutADuplicateWrite()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var connectionString =
            $"Server=127.0.0.1;Port=3306;Database=ecomae;User ID=ecomae;Password={password};AllowUserVariables=true;";
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        var key = "devin-b5-idem-" + Guid.NewGuid().ToString("N");
        var reference = "DEVIN-B5-IDEM-" + Guid.NewGuid().ToString("N")[..12];
        var service = new ErpGlLedgerWriteService(
            new TestConnectionFactory(connectionString),
            new ErpGlPostingService(new ErpVoucherNumberService()),
            new ErpAuditLogWriter());

        try
        {
            var accounts = await ActiveAccountsAsync(connection);
            var input = new ErpManualJournalInput
            {
                IdempotencyKey = key,
                Reference = reference,
                Description = "B5 idempotency rehearsal",
                JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Lines =
                [
                    new ErpGlLine(accounts[0], 17m, 0m, "B5 debit"),
                    new ErpGlLine(accounts[1], 0m, 17m, "B5 credit"),
                ],
            };

            var first = await service.ManualJournalAsync(input, adminId: 1);
            var replay = await service.ManualJournalAsync(input, adminId: 1);

            Assert.Equal(first, replay);
            Assert.Equal(
                1L,
                await ScalarAsync(
                    connection,
                    "SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `reference` = ?",
                    reference));
            Assert.Equal(
                1L,
                await ScalarAsync(
                    connection,
                    "SELECT COUNT(*) FROM `epc_erp_idempotency` WHERE `idem_key` = ? AND `user_id` = 1",
                    key));
        }
        finally
        {
            await ExecuteAsync(connection, "DELETE FROM `epc_erp_idempotency` WHERE `idem_key` = ?", key);
            await ExecuteAsync(
                connection,
                "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.id = l.journal_id WHERE j.reference = ?",
                reference);
            await ExecuteAsync(connection, "DELETE FROM `epc_erp_gl_journals` WHERE `reference` = ?", reference);
        }
    }

    private static async Task<long[]> ActiveAccountsAsync(MySqlConnection connection)
    {
        await using var command = new MySqlCommand(
            "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `active` = 1 ORDER BY `id` LIMIT 2",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<long>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt64(0));
        }

        if (ids.Count < 2)
        {
            throw new InvalidOperationException("Throwaway database does not contain two active COA accounts.");
        }

        return ids.ToArray();
    }

    private static async Task<long> ScalarAsync(MySqlConnection connection, string sql, string value)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@p0", value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, string value)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@p0", value);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public TestConnectionFactory(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
