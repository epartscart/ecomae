using System.Data.Common;
using EcomAE.Platform.Erp;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpStatementIncomeTests
{
    [Fact]
    public void AccountClass_KeepsIncomeAndDoesNotHideTheJournalCompany()
    {
        Assert.Equal("revenue", ErpAccountClass.Normalize("income", "1000"));
        Assert.Equal("revenue", ErpAccountClass.Normalize("", "4100"));
        Assert.Equal("expense", ErpAccountClass.Normalize("", "6210"));
        Assert.Equal("asset", ErpAccountClass.Normalize("asset", "4000"));
        Assert.Equal(string.Empty, ErpAccountClass.Normalize("", "abc"));

        Assert.True(ErpAccountClass.InCompany(1, 9, 1));
        Assert.False(ErpAccountClass.InCompany(2, 0, 1));
        Assert.True(ErpAccountClass.InCompany(2, 1, 1));
        Assert.False(ErpAccountClass.InCompany(0, 0, 1));
        Assert.True(ErpAccountClass.InCompany(5, 5, 0));

        Assert.True(ErpAccountClass.IsProfitAndLossClose("adjustment", "YE-1", "year-end close", "Year-end close FY2025"));
        Assert.True(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "p&l close", ""));
        Assert.True(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "FY-CLOSE-12-1", "Clear income"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "YE-DEP", "", "Year-end depreciation"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "YE-FX", "", "Year-end currency revaluation"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "FY-DEPR-12-1", "Year-end depreciation"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "FY-FX-12-1", "Year-end currency revaluation"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "FX-REVAL 2026-06-10", "Period-end FX revaluation of open foreign-currency receivables"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("adjustment", "", "DEPR-2026-07", "Fixed asset depreciation 2026-07"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("manual", "YE-1", "year-end close", "Year-end close FY2025"));
        Assert.False(ErpAccountClass.IsProfitAndLossClose("manual", "", "FY-CLOSE-12-1", "Clear income"));
    }

    [Fact]
    public void PeriodProfit_ShowsRentalGrossProfitAndLeavesGeneralExpensesOutsideIt()
    {
        var totals = ErpIfrs18Face.PeriodProfit(
        [
            ("Rental income", "revenue", 1000m),
            ("Property operating expenses", "expense", 400m),
            ("General expenses", "expense", 100m),
        ]);

        Assert.Equal(1000m, totals.Income);
        Assert.Equal(500m, totals.Expense);
        Assert.Equal(600m, totals.GrossProfit);
        Assert.Equal(500m, totals.NetProfit);
        Assert.Equal(ErpIfrs18Category.Investing, ErpIfrs18Face.ClassifyProfit("Fair-value gain on investment property"));
    }

    [Fact]
    public void ProfitOrLoss_AlwaysPrintsGrossProfit()
    {
        var statement = new ErpFinancialStatement(
            new ErpFinancialStatementQuery(ErpFinancialStatementKind.ProfitAndLoss, ErpFinancialStatementLevel.Company, 1, 0, ""),
            "AED",
            "AED",
            1m,
            "",
            [new ErpFinancialStatementLine("6100", "General expenses", "expense", 100m, 100m)],
            0, 100m, -100m, 0, 0, 0, 0,
            "Company 1",
            "test",
            "",
            []);

        var rows = ErpIfrs18Face.ProfitOrLoss(statement);
        Assert.Equal(0m, rows.Single(row => row.Label == ErpIfrs18Face.GrossProfit).Accounting);
    }

    [Fact]
    public void Pages_ShowIncomeExpensesAndGrossProfitForTheSelectedDates()
    {
        var trial = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpTrialBalanceApp.razor"));
        Assert.Contains("Income", trial, StringComparison.Ordinal);
        Assert.Contains("Expenses", trial, StringComparison.Ordinal);
        Assert.Contains("Gross profit", trial, StringComparison.Ordinal);
        Assert.Contains("year-end close", trial, StringComparison.Ordinal);
        Assert.Contains("#1f4e79", trial, StringComparison.Ordinal);

        var report = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpReportCenterApp.razor"));
        Assert.Contains("name=\"from\"", report, StringComparison.Ordinal);
        Assert.Contains("name=\"to\"", report, StringComparison.Ordinal);
        Assert.Contains(">Income<", report, StringComparison.Ordinal);
        Assert.Contains(">Expenses<", report, StringComparison.Ordinal);
        Assert.Contains(">Gross profit<", report, StringComparison.Ordinal);

        var parameters = ErpTrialBalanceParameters.FromQuery(null, null, null, null, null, null, null, null, null, null);
        Assert.Equal(2000, parameters.Limit);
    }

    [Fact]
    public async Task PostedClose_KeepsIncomeOnTheStatements()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_pl_" + Guid.NewGuid().ToString("N")[..12];
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
            var day = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            await using (var connection = await connections.OpenAsync())
            {
                await posting.EnsureSchemaAsync(connection);
                var coa = await CoaIdsAsync(connection);
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = day,
                    Reference = "RENT",
                    Description = "Rental income",
                    SourceType = "manual",
                    CompanyId = 1,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["1000"], 1000m, 0m, ""),
                    new ErpGlLine(coa["4000"], 0m, 1000m, ""),
                ], 1);
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = day,
                    Reference = "OPEX",
                    Description = "Property cost",
                    SourceType = "manual",
                    CompanyId = 1,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["5000"], 400m, 0m, ""),
                    new ErpGlLine(coa["2000"], 0m, 400m, ""),
                ], 1);
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = day,
                    Reference = "YE",
                    Description = "Year-end close FY2025",
                    SourceType = "adjustment",
                    CompanyId = 1,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["4000"], 1000m, 0m, ""),
                    new ErpGlLine(coa["5000"], 0m, 400m, ""),
                    new ErpGlLine(coa["3100"], 0m, 600m, ""),
                ], 1);

                await using var stamp = connection.CreateCommand();
                stamp.CommandText =
                    "UPDATE `epc_erp_gl_lines` l INNER JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` "
                    + "SET l.`legal_entity_id` = 9 WHERE a.`code` = '4000'";
                await stamp.ExecuteNonQueryAsync();
            }

            var statements = new ErpFinancialStatementReadService(connections);
            var profit = await statements.ReadAsync(ErpFinancialStatementQuery.FromQuery(
                "pl", "company", "1", "0", "", "2025-01-01", "2026-10-17"));
            Assert.Equal("live", profit.Source);
            Assert.Equal(1000m, profit.IncomeAccounting);
            Assert.Equal(400m, profit.ExpenseAccounting);
            Assert.Equal(600m, profit.NetProfitAccounting);
            var face = ErpIfrs18Face.ProfitOrLoss(profit);
            Assert.Equal(600m, face.Single(row => row.Label == ErpIfrs18Face.GrossProfit).Accounting);
            Assert.Contains(face, row => row.Label == "Sales revenue" && row.Accounting == 1000m);
            Assert.Contains(face, row => row.Label == "Cost of goods sold" && row.Accounting == -400m);

            var position = await statements.ReadAsync(ErpFinancialStatementQuery.FromQuery(
                "balance_sheet", "company", "1", "0", "", "2025-01-01", "2026-10-17"));
            Assert.Equal(1000m, position.AssetAccounting);
            Assert.Equal(400m, position.LiabilityAccounting);
            Assert.Equal(600m, position.EquityAccounting);
            Assert.Equal(0m, position.NetProfitAccounting);
            Assert.Equal(0m, position.BalanceCheckAccounting);
            Assert.Equal(600m, position.Lines.Single(line => line.Code == "3100").Accounting);
            Assert.Equal(1000m, position.Lines.Single(line => line.Code == "1000").Accounting);

            var trial = await new ErpTrialBalanceReadService(connections).ReadAsync(
                ErpTrialBalanceParameters.FromQuery(
                    "2025-01-01", "2026-10-17", "current", "column", "1", "1",
                    "main_account", "0", null, null, null, null, null, "1"));
            Assert.Equal(1000m, trial.Income);
            Assert.Equal(400m, trial.Expenses);
            Assert.Equal(600m, trial.GrossProfit);
            Assert.Equal(600m, trial.NetProfit);
            var sales = trial.Lines.Single(line => line.Code == "4000");
            Assert.Equal(1000m, sales.Debit);
            Assert.Equal(1000m, sales.Credit);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Dictionary<string, long>> CoaIdsAsync(DbConnection connection)
    {
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `code`, `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1000','2000','3100','4000','5000')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids[Convert.ToString(reader.GetValue(0)) ?? ""] = Convert.ToInt64(reader.GetValue(1));
        }

        Assert.Equal(5, ids.Count);
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
