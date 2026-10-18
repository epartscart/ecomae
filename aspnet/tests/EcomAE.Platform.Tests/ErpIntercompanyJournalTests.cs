using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIntercompanyJournalTests
{
    private static readonly ErpIcAccountIds Accounts = new(11, 22, 15, 21);

    [Fact]
    public void VatFollowsTheTrn()
    {
        Assert.False(ErpIntercompanyTax.ChargeVatOnInvoice("100200300400003", "100 200 300 400 003"));
        Assert.False(ErpIntercompanyTax.ChargeVatOnInvoice("", ""));
        Assert.False(ErpIntercompanyTax.ChargeVatOnInvoice("", "100200300400003"));
        Assert.True(ErpIntercompanyTax.ChargeVatOnInvoice("100200300400003", "100200300400004"));
        Assert.True(ErpIntercompanyTax.ChargeVatOnInvoice("100200300400003", ""));
    }

    [Fact]
    public void SameTrn_AddsReceivableAndPayableWithoutVat()
    {
        var prepared = ErpIntercompanyJournal.Expand(
            [
                new ErpGlLine(500, 200m, 0m, "cost", 1, 0),
                new ErpGlLine(400, 0m, 200m, "income", 2, 0),
            ],
            0,
            0,
            Units(sameTrn: true),
            Entities(),
            Accounts,
            5m);

        Assert.Equal(4, prepared.Lines.Count);
        Assert.DoesNotContain(prepared.Lines, line => line.CoaId is 15 or 21);
        Assert.Contains(prepared.Lines, line => line.CoaId == 22 && line.Credit == 200m && line.BusinessUnitId == 1);
        Assert.Contains(prepared.Lines, line => line.CoaId == 11 && line.Debit == 200m && line.BusinessUnitId == 2);
        Assert.Equal("same_trn", Assert.Single(prepared.Invoices).Treatment);
        Assert.Equal(0m, prepared.HeaderBusinessUnitId);
        AssertBalanced(prepared.Lines);
    }

    [Fact]
    public void DifferentTrn_AddsVatToTheIntercompanyInvoice()
    {
        var prepared = ErpIntercompanyJournal.Expand(
            [
                new ErpGlLine(500, 100m, 0m, "cost", 1, 0),
                new ErpGlLine(400, 0m, 100m, "income", 2, 0),
            ],
            0,
            0,
            Units(sameTrn: false),
            Entities(),
            Accounts,
            5m);

        Assert.Contains(prepared.Lines, line => line.CoaId == 22 && line.Credit == 105m && line.BusinessUnitId == 1 && line.LegalEntityId == 1);
        Assert.Contains(prepared.Lines, line => line.CoaId == 11 && line.Debit == 105m && line.BusinessUnitId == 2 && line.LegalEntityId == 2);
        Assert.Contains(prepared.Lines, line => line.CoaId == 15 && line.Debit == 5m && line.BusinessUnitId == 1);
        Assert.Contains(prepared.Lines, line => line.CoaId == 21 && line.Credit == 5m && line.BusinessUnitId == 2);
        var invoice = Assert.Single(prepared.Invoices);
        Assert.Equal("different_trn", invoice.Treatment);
        Assert.Equal("business_unit", invoice.Scope);
        Assert.Equal(5m, invoice.Vat);
        Assert.Equal("BU2", invoice.SellerCode);
        Assert.Equal("HQ", invoice.BuyerCode);
        Assert.Contains("tax invoice at 5%", prepared.Note, StringComparison.Ordinal);
        AssertBalanced(prepared.Lines);
    }

    [Fact]
    public void DifferentLegalEntities_BalanceWithoutABusinessUnit()
    {
        var prepared = ErpIntercompanyJournal.Expand(
            [
                new ErpGlLine(500, 80m, 0m, "cost", 0, 1),
                new ErpGlLine(400, 0m, 80m, "income", 0, 2),
            ],
            0,
            0,
            Units(sameTrn: true),
            Entities(),
            Accounts,
            5m);

        Assert.Equal("legal_entity", Assert.Single(prepared.Invoices).Scope);
        Assert.Equal("different_trn", prepared.Invoices[0].Treatment);
        Assert.Contains(prepared.Lines, line => line.CoaId == 22 && line.LegalEntityId == 1 && line.Credit == 84m);
        Assert.Contains(prepared.Lines, line => line.CoaId == 11 && line.LegalEntityId == 2 && line.Debit == 84m);
        Assert.Equal(0L, prepared.HeaderLegalEntityId);
        AssertBalanced(prepared.Lines);
    }

    [Fact]
    public async Task PostedJournal_KeepsEachBusinessUnitTrialBalanceInBalance()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_icj_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using (var adminConn = new MySqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using var create = new MySqlCommand("CREATE DATABASE `" + database + "`", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var connections = new JournalConnections(cs);
            var setup = new ErpIntercompanySetupService(connections);
            await setup.ReadAsync();
            long hqId;
            long secondId;
            long mainId;
            long branchId;
            long purchaseId;
            long salesId;
            await using (var connection = await connections.OpenAsync())
            {
                await using (var branch = connection.CreateCommand())
                {
                    branch.CommandText =
                        "INSERT INTO `epc_erp_pm_legal_entities` (`code`,`name`,`country_code`,`currency_code`,`trn`,`active`,`time_created`,`time_updated`)"
                        + " VALUES ('BR2','Branch two','AE','AED','100200300400002',1,UNIX_TIMESTAMP(),UNIX_TIMESTAMP())";
                    await branch.ExecuteNonQueryAsync();
                }

                await using (var trn = connection.CreateCommand())
                {
                    trn.CommandText = "UPDATE `epc_erp_pm_legal_entities` SET `trn` = '100200300400001' WHERE `code` = 'MAIN'";
                    await trn.ExecuteNonQueryAsync();
                }

                await using (var unit = connection.CreateCommand())
                {
                    unit.CommandText =
                        "INSERT INTO `epc_erp_pm_business_units` (`code`,`name`,`legal_entity_id`,`trn`,`active`,`time_created`,`time_updated`)"
                        + " SELECT 'BU2','Second unit', `id`, '', 1, UNIX_TIMESTAMP(), UNIX_TIMESTAMP() FROM `epc_erp_pm_legal_entities` WHERE `code` = 'BR2' LIMIT 1";
                    await unit.ExecuteNonQueryAsync();
                }

                hqId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code` = 'HQ' LIMIT 1");
                secondId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code` = 'BU2' LIMIT 1");
                mainId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = 'MAIN' LIMIT 1");
                branchId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = 'BR2' LIMIT 1");
                purchaseId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = '5180' LIMIT 1");
                salesId = await ScalarAsync(connection, "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = '4180' LIMIT 1");
            }

            var ledger = new ErpGlLedgerWriteService(
                connections,
                new ErpGlPostingService(new ErpVoucherNumberService()),
                new ErpAuditLogWriter());
            var same = await ledger.ManualJournalAsync(new ErpManualJournalInput
            {
                Reference = "IC-SAME",
                Description = "Same registration",
                JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Lines =
                [
                    new ErpGlLine(purchaseId, 200m, 0m, "cost", hqId, mainId),
                    new ErpGlLine(salesId, 0m, 200m, "income", hqId, mainId),
                ],
            }, 1);
            Assert.Equal(string.Empty, same.Note);

            await using (var connection = await connections.OpenAsync())
            {
                await using var trn = connection.CreateCommand();
                trn.CommandText = "UPDATE `epc_erp_pm_business_units` SET `trn` = '100200300400003' WHERE `id` = @id";
                trn.Parameters.Add(new MySqlParameter("@id", secondId));
                await trn.ExecuteNonQueryAsync();
            }

            var crossed = await ledger.ManualJournalAsync(new ErpManualJournalInput
            {
                Reference = "IC-TRN",
                Description = "Different registration",
                JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Lines =
                [
                    new ErpGlLine(purchaseId, 100m, 0m, "cost", hqId, mainId),
                    new ErpGlLine(salesId, 0m, 100m, "income", secondId, branchId),
                ],
            }, 1);
            Assert.Contains("tax invoice at 5%", crossed.Note, StringComparison.Ordinal);

            var read = new ErpTrialBalanceReadService(connections);
            var buyer = await read.ReadAsync(UnitQuery(hqId));
            var seller = await read.ReadAsync(UnitQuery(secondId));
            Assert.Equal(buyer.DebitTotal, buyer.CreditTotal);
            Assert.Equal(seller.DebitTotal, seller.CreditTotal);
            Assert.Equal(0m, buyer.ClosingTotal);
            Assert.Equal(0m, seller.ClosingTotal);
            Assert.Equal(105m, Assert.Single(buyer.Lines, line => line.Code == "2180").Credit);
            Assert.Equal(5m, Assert.Single(buyer.Lines, line => line.Code == "1150").Debit);
            Assert.Equal(105m, Assert.Single(seller.Lines, line => line.Code == "1180").Debit);
            Assert.Equal(5m, Assert.Single(seller.Lines, line => line.Code == "2100").Credit);

            var buyerBooks = await read.ReadAsync(CompanyQuery(mainId));
            var sellerBooks = await read.ReadAsync(CompanyQuery(branchId));
            Assert.Equal(buyerBooks.DebitTotal, buyerBooks.CreditTotal);
            Assert.Equal(sellerBooks.DebitTotal, sellerBooks.CreditTotal);

            await using (var connection = await connections.OpenAsync())
            {
                Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `epc_erp_ic_relationships` WHERE `scope` = 'business_unit' AND `from_code` = 'BU2' AND `to_code` = 'HQ'"));
                Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM `epc_erp_ic_invoices` WHERE `treatment` = 'different_trn' AND `vat_amount` = 5"));
            }
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ErpTrialBalanceParameters UnitQuery(long businessUnitId)
        => ErpTrialBalanceParameters.FromQuery(
            "2026-01-01", "2026-12-31", "current", "column", "1", "1", "business_unit", null, null, null, null, null,
            businessUnitId.ToString(), null);

    private static ErpTrialBalanceParameters CompanyQuery(long companyId)
        => ErpTrialBalanceParameters.FromQuery(
            "2026-01-01", "2026-12-31", "current", "column", "1", "1", "company", null, null, null, null, null,
            null, companyId.ToString());

    private static Dictionary<long, ErpIcPartyRef> Units(bool sameTrn)
        => new()
        {
            [1] = new ErpIcPartyRef(1, "HQ", "", 1),
            [2] = new ErpIcPartyRef(2, "BU2", sameTrn ? "" : "100200300400002", sameTrn ? 1 : 2),
        };

    private static Dictionary<long, ErpIcPartyRef> Entities()
        => new()
        {
            [1] = new ErpIcPartyRef(1, "MAIN", "100200300400001", 0),
            [2] = new ErpIcPartyRef(2, "BR2", "100200300400002", 0),
        };

    private static void AssertBalanced(IReadOnlyList<ErpGlLine> lines)
    {
        Assert.Equal(
            lines.Sum(line => line.Debit),
            lines.Sum(line => line.Credit));
        foreach (var group in lines.GroupBy(line => (line.BusinessUnitId, line.LegalEntityId)))
        {
            Assert.Equal(group.Sum(line => line.Debit), group.Sum(line => line.Credit));
        }
    }

    private static async Task<long> ScalarAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0L : Convert.ToInt64(value);
    }

    private sealed class JournalConnections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
