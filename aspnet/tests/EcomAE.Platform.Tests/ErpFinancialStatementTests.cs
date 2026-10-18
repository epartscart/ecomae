using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinancialStatementTests
{
    [Fact]
    public void Pages_OpenPostedStatements()
    {
        var report = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpReportCenterApp.razor"));
        Assert.Contains("Profit and loss", report, StringComparison.Ordinal);
        Assert.Contains("Balance sheet", report, StringComparison.Ordinal);
        Assert.Contains("Total assets", report, StringComparison.Ordinal);
        Assert.Contains("Total liabilities and equity", report, StringComparison.Ordinal);
        Assert.Contains("business_unit", report, StringComparison.Ordinal);
        Assert.Contains("consolidation", report, StringComparison.Ordinal);
        Assert.Contains("IErpFinancialStatementReadService", report, StringComparison.Ordinal);

        var external = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpExternalReportingApp.razor"));
        Assert.Contains("Posted books", external, StringComparison.Ordinal);
        Assert.Contains("IErpFinancialStatementReadService", external, StringComparison.Ordinal);
        Assert.Contains("fin__consolidated_financial_statements", external, StringComparison.Ordinal);

        var advanced = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("Profit and loss", advanced, StringComparison.Ordinal);
        Assert.Contains("\"consolidation\"", advanced, StringComparison.Ordinal);

        var workspace = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("level=consolidation", workspace, StringComparison.Ordinal);
        Assert.Contains("ExistingHref", workspace, StringComparison.Ordinal);

        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFinancialStatementReadService.cs"));
        Assert.Contains("CASE WHEN j.`id` IS NOT NULL THEN l.`debit`", service, StringComparison.Ordinal);
        Assert.Contains("epc_cons_ic", service, StringComparison.Ordinal);
        Assert.Contains("epc_fx_rates", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostedBooks_SplitCompanyUnitConsolidationAndCurrency()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_fs_" + Guid.NewGuid().ToString("N")[..12];
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
            var connections = new Connections(cs);
            IErpGlPostingService posting = new ErpGlPostingService(new ErpVoucherNumberService());
            await using (var connection = await connections.OpenAsync())
            {
                await posting.EnsureSchemaAsync(connection);
                var coa = await CoaIdsAsync(connection);
                var today = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = today,
                    Reference = "C1",
                    Description = "Company 1 sale",
                    SourceType = "manual",
                    CompanyId = 1,
                    BusinessUnitId = 7,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["1000"], 100m, 0m, ""),
                    new ErpGlLine(coa["4000"], 0m, 100m, ""),
                ], 1);
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = today,
                    Reference = "C2",
                    Description = "Company 2 cost",
                    SourceType = "manual",
                    CompanyId = 2,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["5000"], 40m, 0m, ""),
                    new ErpGlLine(coa["2000"], 0m, 40m, ""),
                ], 1);

                await using (var fx = connection.CreateCommand())
                {
                    fx.CommandText =
                        "CREATE TABLE IF NOT EXISTS `epc_fx_rates` (" +
                        "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY," +
                        "`base_currency` CHAR(3) NOT NULL," +
                        "`target_currency` CHAR(3) NOT NULL," +
                        "`rate` DECIMAL(16,8) NOT NULL," +
                        "`inverse_rate` DECIMAL(16,8) NOT NULL," +
                        "`effective_date` DATE NOT NULL)";
                    await fx.ExecuteNonQueryAsync();
                }

                await using (var rate = connection.CreateCommand())
                {
                    rate.CommandText =
                        "INSERT INTO `epc_fx_rates` (`base_currency`,`target_currency`,`rate`,`inverse_rate`,`effective_date`) " +
                        "VALUES ('AED','USD',0.27,3.70370370,CURDATE())";
                    await rate.ExecuteNonQueryAsync();
                }

                await using (var ic = connection.CreateCommand())
                {
                    ic.CommandText =
                        "CREATE TABLE `epc_cons_ic` (`id` int NOT NULL AUTO_INCREMENT, `txn_type` varchar(20) NOT NULL, `amount` decimal(18,2) NOT NULL, PRIMARY KEY (`id`))";
                    await ic.ExecuteNonQueryAsync();
                }

                await using (var rows = connection.CreateCommand())
                {
                    rows.CommandText = "INSERT INTO `epc_cons_ic` (`txn_type`,`amount`) VALUES ('sale',25),('loan',10)";
                    await rows.ExecuteNonQueryAsync();
                }
            }

            var read = new ErpFinancialStatementReadService(connections);
            var company = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "company", "1", "0", "USD"));
            Assert.Equal("live", company.Source);
            Assert.Equal(100m, company.IncomeAccounting);
            Assert.Equal(0m, company.ExpenseAccounting);
            Assert.Equal(100m, company.NetProfitAccounting);
            Assert.Equal("USD", company.ReportingCurrency);
            Assert.Equal(27m, Round(company.NetProfitAccounting * company.Rate));
            var sales = Assert.Single(company.Lines, line => line.Code == "4000");
            Assert.Equal(27m, sales.Reporting);

            var unit = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "business_unit", "1", "7", ""));
            Assert.Equal(100m, unit.IncomeAccounting);
            Assert.Equal(0m, unit.ExpenseAccounting);
            Assert.Contains("Opening balances", unit.Message, StringComparison.Ordinal);

            var other = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "company", "2", "0", ""));
            Assert.Equal(0m, other.IncomeAccounting);
            Assert.Equal(40m, other.ExpenseAccounting);
            Assert.Equal(-40m, other.NetProfitAccounting);

            var groupPl = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "consolidation", "0", "0", "USD"));
            Assert.Equal("USD", groupPl.AccountingCurrency);
            Assert.Equal("USD", groupPl.ReportingCurrency);
            Assert.Equal(1m, groupPl.Rate);
            Assert.Equal(20.25m, groupPl.IncomeAccounting);
            Assert.Equal(4.05m, groupPl.ExpenseAccounting);
            Assert.Equal(16.20m, groupPl.NetProfitAccounting);
            Assert.Contains("head office reporting currency USD", groupPl.Message, StringComparison.Ordinal);
            Assert.Contains("AED to USD at 0.27", groupPl.Message, StringComparison.Ordinal);
            Assert.Contains(groupPl.Lines, line => line.Code == "ELIM-REV" && line.Accounting == -6.75m);

            var groupBs = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("balance_sheet", "consolidation", "0", "0", "USD"));
            Assert.Equal(24.30m, groupBs.AssetAccounting);
            Assert.Equal(8.10m, groupBs.LiabilityAccounting);
            Assert.Equal(0m, groupBs.EquityAccounting);
            Assert.Equal(0m, groupBs.BalanceCheckAccounting);
            Assert.Equal(27m, Assert.Single(groupBs.Lines, line => line.Code == "1000").Reporting);
            Assert.Equal(-2.70m, Assert.Single(groupBs.Lines, line => line.Code == "ELIM-ASSET").Reporting);

            await using (var connection = await connections.OpenAsync())
            {
                var coa = await CoaIdsAsync(connection);
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Reference = "EU",
                    Description = "Euro company sale",
                    SourceType = "manual",
                    CompanyId = 3,
                    CurrencyCode = "EUR",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["1000"], 50m, 0m, ""),
                    new ErpGlLine(coa["4000"], 0m, 50m, ""),
                ], 1);
                await using var rate = connection.CreateCommand();
                rate.CommandText =
                    "INSERT INTO `epc_fx_rates` (`base_currency`,`target_currency`,`rate`,`inverse_rate`,`effective_date`) " +
                    "VALUES ('EUR','USD',1.10,0.90909091,CURDATE())";
                await rate.ExecuteNonQueryAsync();
            }

            var mixed = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "consolidation", "0", "0", "USD"));
            Assert.Equal(75.25m, mixed.IncomeAccounting);
            Assert.Equal(4.05m, mixed.ExpenseAccounting);
            Assert.Contains("EUR to USD at 1.1", mixed.Message, StringComparison.Ordinal);
            var euroCash = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("balance_sheet", "consolidation", "0", "0", "USD"));
            Assert.Equal(79.30m, euroCash.AssetAccounting);
            Assert.Equal(0m, euroCash.BalanceCheckAccounting);

            var missing = await read.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "company", "1", "0", "EUR"));
            Assert.Equal("AED", missing.ReportingCurrency);
            Assert.Equal(1m, missing.Rate);
            Assert.Contains("No exchange rate from AED to EUR", missing.Message, StringComparison.Ordinal);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static async Task<Dictionary<string, long>> CoaIdsAsync(DbConnection connection)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `code`, `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1000','2000','4000','5000')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids[reader.GetString(0)] = reader.GetInt64(1);
        }

        Assert.Equal(4, ids.Count);
        return ids;
    }

    private static string FindRepoFile(string relative)
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

    private sealed class Connections(string cs) : IErpWriteConnectionFactory
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
