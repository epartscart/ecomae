using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIntercompanySetupTests
{
    [Fact]
    public void Page_CoversRelationshipsAccountsEliminationTaxAndCurrency()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpIntercompanyApp.razor"));
        Assert.Contains("@page \"/erp/intercompany-app\"", page, StringComparison.Ordinal);
        Assert.Contains("Save relationship", page, StringComparison.Ordinal);
        Assert.Contains("value=\"legal_entity\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"business_unit\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"cost_centre\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"sales\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"purchase\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"receivable\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"payable\"", page, StringComparison.Ordinal);
        Assert.Contains("Save elimination rule", page, StringComparison.Ordinal);
        Assert.Contains("VAT group", page, StringComparison.Ordinal);
        Assert.Contains("Sales group VAT", page, StringComparison.Ordinal);
        Assert.Contains("Purchase group VAT", page, StringComparison.Ordinal);
        Assert.Contains("Article 34", page, StringComparison.Ordinal);
        Assert.Contains("Article 40", page, StringComparison.Ordinal);
        Assert.Contains("arm's length", page, StringComparison.Ordinal);
        Assert.Contains("Reporting currency", page, StringComparison.Ordinal);
        Assert.Contains("Transaction currency", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.Equal("/erp/intercompany-app", EcomAeRoutes.ErpIntercompanyApp);
        Assert.Equal("/erp/intercompany/save", EcomAeRoutes.ErpIntercompanySave);
        Assert.Equal("/erp/intercompany-app", ErpPhpTabRouteMap.All["intercompany"]);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleIntercompanySaveAsync", module, StringComparison.Ordinal);
    }

    [Fact]
    public void UaeVatAndCt_FollowThePairScope()
    {
        Assert.Contains("not a supply", ErpIntercompanyTax.VatTreatment("business_unit", true, 5m, 5m), StringComparison.Ordinal);
        Assert.Contains("not a supply", ErpIntercompanyTax.VatTreatment("cost_centre", false, 5m, 5m), StringComparison.Ordinal);
        Assert.Contains("disregarded", ErpIntercompanyTax.VatTreatment("legal_entity", true, 5m, 5m), StringComparison.Ordinal);
        Assert.Contains("Sales group VAT 5%", ErpIntercompanyTax.VatTreatment("legal_entity", false, 5m, 5m), StringComparison.Ordinal);
        Assert.Contains("purchase group VAT 5%", ErpIntercompanyTax.VatTreatment("legal_entity", false, 5m, 5m), StringComparison.Ordinal);
        Assert.Contains("not a separate taxable person", ErpIntercompanyTax.CtTreatment("cost_centre", true), StringComparison.Ordinal);
        Assert.Contains("Article 40", ErpIntercompanyTax.CtTreatment("legal_entity", true), StringComparison.Ordinal);
        Assert.Contains("Article 34", ErpIntercompanyTax.CtTreatment("legal_entity", false), StringComparison.Ordinal);
        Assert.Contains("arm's length", ErpIntercompanyTax.TransferPricing("cup"), StringComparison.Ordinal);
        Assert.Contains("Cost plus", ErpIntercompanyTax.TransferPricing("cost_plus"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_SeedsAccountsRulesAndEliminatesABalancedPair()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_ic_" + Guid.NewGuid().ToString("N")[..12];
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
            var setup = new ErpIntercompanySetupService(connections);
            var first = await setup.ReadAsync();
            Assert.Equal("live", first.Source);
            Assert.Contains(first.Accounts, row => row.Purpose == "sales" && row.CoaCode == "4180");
            Assert.Contains(first.Accounts, row => row.Purpose == "purchase" && row.CoaCode == "5180");
            Assert.Contains(first.Accounts, row => row.Purpose == "receivable" && row.CoaCode == "1180");
            Assert.Contains(first.Accounts, row => row.Purpose == "payable" && row.CoaCode == "2180");
            Assert.Contains(first.Rules, row => row.Code == "ELIM-PL" && row.Active);
            Assert.Contains(first.Rules, row => row.Code == "ELIM-BS" && row.Active);
            Assert.Equal("AED", first.Policy.ReportingCurrency);
            Assert.Equal(5m, first.Policy.SalesVatRate);
            Assert.Equal(5m, first.Policy.PurchaseVatRate);
            Assert.Contains(first.Parties, row => row.Scope == "legal_entity");
            Assert.Contains(first.Parties, row => row.Scope == "business_unit");
            Assert.Contains(first.Parties, row => row.Scope == "cost_centre");

            var self = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "relationship", "legal_entity", "MAIN", "MAIN", "both", "", "", "", "", "", "", "", true,
                false, false, 5m, 5m, "cup", "AED", "closing_average", "", 1));
            Assert.False(self.Succeeded);

            await using (var connection = await connections.OpenAsync())
            {
                await using var extra = connection.CreateCommand();
                extra.CommandText =
                    "INSERT INTO `epc_erp_pm_legal_entities` (`code`,`name`,`country_code`,`currency_code`,`active`,`time_created`,`time_updated`)"
                    + " VALUES ('BR2','Branch two','AE','AED',1,UNIX_TIMESTAMP(),UNIX_TIMESTAMP())";
                await extra.ExecuteNonQueryAsync();
            }

            var paired = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "relationship", "legal_entity", "MAIN", "BR2", "both", "", "", "", "", "", "", "", true,
                false, false, 5m, 5m, "cup", "AED", "closing_average", "", 1));
            Assert.True(paired.Succeeded, paired.Message);
            var units = first.Parties.Where(row => row.Scope == "business_unit").Select(row => row.Code).ToList();
            if (units.Count >= 1)
            {
                await using var connection = await connections.OpenAsync();
                await using var second = connection.CreateCommand();
                second.CommandText =
                    "INSERT INTO `epc_erp_pm_business_units` (`code`,`name`,`legal_entity_id`,`active`,`time_created`,`time_updated`)"
                    + " SELECT 'BU2','Second unit', `legal_entity_id`, 1, UNIX_TIMESTAMP(), UNIX_TIMESTAMP() FROM `epc_erp_pm_business_units` WHERE `active`=1 LIMIT 1";
                await second.ExecuteNonQueryAsync();
            }

            var buPair = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "relationship", "business_unit", "HQ", "BU2", "customer", "", "", "", "", "", "", "", true,
                false, false, 5m, 5m, "cup", "AED", "closing_average", "", 1));
            Assert.True(buPair.Succeeded, buPair.Message);
            var ccPair = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "relationship", "cost_centre", "CC-ADMIN", "CC-SALES", "vendor", "", "", "", "", "", "", "", true,
                false, false, 5m, 5m, "cup", "AED", "closing_average", "", 1));
            Assert.True(ccPair.Succeeded, ccPair.Message);

            var tax = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "tax", "", "", "", "", "", "", "", "", "", "", "", true,
                true, true, 5m, 5m, "cost_plus", "AED", "closing_average", "", 1));
            Assert.True(tax.Succeeded, tax.Message);
            var currency = await setup.SaveAsync(new ErpIntercompanySaveRequest(
                "currency", "", "", "", "", "", "", "", "", "", "", "", true,
                false, false, 0m, 0m, "cup", "USD", "closing_average", "AED", 1));
            Assert.True(currency.Succeeded, currency.Message);
            var saved = await setup.ReadAsync();
            Assert.Equal(3, saved.Relationships.Count);
            Assert.True(saved.Policy.VatGroup);
            Assert.True(saved.Policy.CtGroup);
            Assert.Equal("cost_plus", saved.Policy.TpMethod);
            Assert.Equal("USD", saved.Policy.ReportingCurrency);
            Assert.Equal("AED", saved.Policy.TransactionCurrency);
            Assert.Equal(5m, saved.Policy.SalesVatRate);

            IErpGlPostingService posting = new ErpGlPostingService(new ErpVoucherNumberService());
            await using (var connection = await connections.OpenAsync())
            {
                var coa = new Dictionary<string, long>(StringComparer.Ordinal);
                await using var ids = connection.CreateCommand();
                ids.CommandText = "SELECT `code`, `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1180','2180','4180','5180')";
                await using var reader = await ids.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    coa[reader.GetString(0)] = reader.GetInt64(1);
                }

                await reader.DisposeAsync();
                await posting.PostJournalAsync(connection, new ErpGlJournalHeader
                {
                    JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Reference = "IC",
                    Description = "Intercompany pair",
                    SourceType = "manual",
                    CompanyId = 1,
                    CurrencyCode = "AED",
                    ExchangeRate = 1m,
                }, [
                    new ErpGlLine(coa["1180"], 100m, 0m, ""),
                    new ErpGlLine(coa["4180"], 0m, 100m, ""),
                    new ErpGlLine(coa["5180"], 100m, 0m, ""),
                    new ErpGlLine(coa["2180"], 0m, 100m, ""),
                ], 1);
            }

            var statements = new ErpFinancialStatementReadService(connections);
            var profit = await statements.ReadAsync(ErpFinancialStatementQuery.FromQuery("pl", "consolidation", "0", "0", ""));
            Assert.Equal(0m, profit.NetProfitAccounting);
            Assert.Contains(profit.Lines, line => line.Code == "ELIM-REV" && line.Accounting == -100m);
            Assert.Contains(profit.Lines, line => line.Code == "ELIM-EXP" && line.Accounting == -100m);
            var sheet = await statements.ReadAsync(ErpFinancialStatementQuery.FromQuery("balance_sheet", "consolidation", "0", "0", ""));
            Assert.Contains(sheet.Lines, line => line.Code == "ELIM-ASSET" && line.Accounting == -100m);
            Assert.Contains(sheet.Lines, line => line.Code == "ELIM-LIAB" && line.Accounting == -100m);
            Assert.Equal(0m, sheet.BalanceCheckAccounting);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
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
