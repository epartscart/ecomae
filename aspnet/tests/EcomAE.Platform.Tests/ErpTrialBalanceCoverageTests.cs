using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTrialBalanceCoverageTests
{
    [Fact]
    public void Parameters_MatchLedgerTrialBalanceListPage()
    {
        var parameters = ErpTrialBalanceParameters.FromQuery(
            "2026-01-01", "2026-03-31", "operations", "include_in_period",
            "0", "1", "company", "1", "1000", "50");
        Assert.Equal(new DateOnly(2026, 1, 1), parameters.FromDate);
        Assert.Equal(new DateOnly(2026, 3, 31), parameters.ToDate);
        Assert.Equal(ErpTrialBalancePostingLayer.Operations, parameters.PostingLayer);
        Assert.Equal(ErpTrialBalanceOpeningDisplay.IncludeInPeriod, parameters.OpeningDisplay);
        Assert.False(parameters.IncludeClosingTransactions);
        Assert.True(parameters.IncludeClosingAdjustments);
        Assert.Equal(ErpTrialBalanceDimensionSet.MainAccountCompany, parameters.DimensionSet);
        Assert.True(parameters.SeparateDimensionColumns);
        Assert.Equal("1000", parameters.AccountCode);
        Assert.Equal(50, parameters.Limit);
        Assert.True(string.IsNullOrEmpty(parameters.Interval));
        Assert.True(parameters.FromUnix < parameters.ToUnix);
        Assert.Contains("layer=operations", parameters.QueryString(), StringComparison.Ordinal);
        Assert.StartsWith("/erp/trial-balance-app?", parameters.AppHref(), StringComparison.Ordinal);
        Assert.StartsWith("/erp/trial-balance/export?", parameters.ExportHref(), StringComparison.Ordinal);
    }

    [Fact]
    public void Parameters_SwapInvertedDatesAndDefaultLayer()
    {
        var parameters = ErpTrialBalanceParameters.FromQuery(
            "2026-12-31", "2026-01-01", null, null, null, null, null, null, null, null);
        Assert.Equal(new DateOnly(2026, 1, 1), parameters.FromDate);
        Assert.Equal(new DateOnly(2026, 12, 31), parameters.ToDate);
        Assert.Equal(ErpTrialBalancePostingLayer.Current, parameters.PostingLayer);
        Assert.Equal(ErpTrialBalanceOpeningDisplay.Column, parameters.OpeningDisplay);
        Assert.True(parameters.IncludeClosingTransactions);
        Assert.True(parameters.IncludeClosingAdjustments);
    }

    [Fact]
    public void Math_BuildsSignedOpeningPeriodAndClosing()
    {
        var debit = ErpTrialBalanceParameters.BuildLine(
            "1000", "Cash", "asset", "debit", 100m, 20m, 5m, 40m, 10m);
        Assert.Equal(115m, debit.Opening);
        Assert.Equal(40m, debit.Debit);
        Assert.Equal(10m, debit.Credit);
        Assert.Equal(30m, debit.Amount);
        Assert.Equal(145m, debit.Closing);

        var folded = ErpTrialBalanceParameters.BuildLine(
            "1000", "Cash", "asset", "debit", 100m, 0m, 0m, 10m, 0m,
            openingDisplay: ErpTrialBalanceOpeningDisplay.IncludeInPeriod);
        Assert.Equal(0m, folded.Opening);
        Assert.Equal(110m, folded.Debit);
        Assert.Equal(110m, folded.Closing);

        var hidden = ErpTrialBalanceParameters.BuildLine(
            "1000", "Cash", "asset", "debit", 100m, 20m, 0m, 10m, 0m,
            openingDisplay: ErpTrialBalanceOpeningDisplay.Exclude);
        Assert.Equal(0m, hidden.Opening);
        Assert.Equal(10m, hidden.Debit);
        Assert.Equal(10m, hidden.Closing);

        var credit = ErpTrialBalanceParameters.BuildLine(
            "2000", "AP", "liability", "credit", 80m, 0m, 0m, 5m, 15m);
        Assert.Equal(-80m, credit.Opening);
        Assert.Equal(-90m, credit.Closing);
    }

    [Fact]
    public void Filters_ClosingAndPostingLayer()
    {
        Assert.True(ErpTrialBalanceParameters.IsClosingTransaction("adjustment", "YE-1", "year-end close", ""));
        Assert.True(ErpTrialBalanceParameters.IsClosingAdjustment("adjustment", "", "", "Closing adjustment"));
        Assert.False(ErpTrialBalanceParameters.IsClosingTransaction("manual", "GV-1", "", "rent"));
        Assert.True(ErpTrialBalanceParameters.IsTaxLayer("FTA-2026"));
        Assert.False(ErpTrialBalanceParameters.IsTaxLayer(""));

        var current = ErpTrialBalanceParameters.FromQuery("2026-01-01", "2026-12-31", "current", "column", "0", "0", null, null, null, null);
        Assert.False(current.AcceptsJournal("adjustment", "YE-1", "year-end", "", ""));
        Assert.True(current.AcceptsJournal("manual", "GV-1", "", "", ""));

        var tax = ErpTrialBalanceParameters.FromQuery("2026-01-01", "2026-12-31", "tax", "column", "1", "1", null, null, null, null);
        Assert.False(tax.AcceptsJournal("manual", "GV-1", "", "", ""));
        Assert.True(tax.AcceptsJournal("manual", "GV-1", "", "", "AE-VAT"));

        var operations = ErpTrialBalanceParameters.FromQuery("2026-01-01", "2026-12-31", "operations", "column", "1", "1", null, null, null, null);
        Assert.False(operations.AcceptsJournal("manual", "GV-1", "", "", "AE-VAT"));
        Assert.True(operations.AcceptsJournal("manual", "GV-1", "", "", ""));
    }

    [Fact]
    public void Parameters_DateIntervalAndFilterMatchLedgerPage()
    {
        var month = ErpTrialBalanceParameters.FromQuery(
            "2020-01-01", "2020-12-31", null, null, null, null, null, null, null, null,
            "this_month", null);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(new DateOnly(today.Year, today.Month, 1), month.FromDate);
        Assert.Equal(today, month.ToDate);
        Assert.Equal("this_month", month.Interval);
        Assert.Contains("interval=this_month", month.QueryString(), StringComparison.Ordinal);

        var lastYear = ErpTrialBalanceParameters.ForInterval("last_year", new DateOnly(2026, 10, 13));
        Assert.Equal(new DateOnly(2025, 1, 1), lastYear.FromDate);
        Assert.Equal(new DateOnly(2025, 12, 31), lastYear.ToDate);
        Assert.Equal("Last year", ErpTrialBalanceParameters.IntervalLabel("last_year"));

        var filtered = ErpTrialBalanceParameters.FromQuery(
            "2026-01-01", "2026-12-31", "current", "column", "1", "1", null, null, null, null,
            null, "cash");
        Assert.Equal("cash", filtered.Filter);
        Assert.True(filtered.MatchesFilter("1000", "Cash"));
        Assert.False(filtered.MatchesFilter("2000", "Accounts payable"));
        Assert.Contains("q=cash", filtered.QueryString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_ValidatesDatesAndDryRun()
    {
        Assert.Equal("invalid", ErpTrialBalanceWriteService.Validate(new ErpTrialBalanceSnapshotRequest())!.Code);
        Assert.Null(ErpTrialBalanceWriteService.Validate(new ErpTrialBalanceSnapshotRequest(FromDate: "2026-01-01", ToDate: "2026-03-31")));
        var dry = new ErpTrialBalanceDryRun();
        var ok = dry.Evaluate(new ErpTrialBalanceSnapshotRequest(FromDate: "2026-01-01", ToDate: "2026-03-31"), false);
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.True(ok.WouldWrite);
        Assert.Equal(0, ok.Writes);
        var refused = dry.Evaluate(new ErpTrialBalanceSnapshotRequest(FromDate: "2026-01-01", ToDate: "2026-03-31"), true);
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
    }

    [Fact]
    public void RoutesAndChrome_OpenDedicatedListPage()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("enterprise_reports", out var enterprise));
        Assert.Equal("/erp/trial-balance-app", enterprise);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("jw_trial_balance", out var jewellery));
        Assert.Equal("/erp/trial-balance-app", jewellery);
        Assert.Equal("/erp/trial-balance-app", EcomAeRoutes.ErpTrialBalanceApp);
        Assert.Equal("/erp/trial-balance/snapshot", EcomAeRoutes.ErpTrialBalanceSnapshot);
        Assert.Equal("/erp/trial-balance/export", EcomAeRoutes.ErpTrialBalanceExport);
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Href == "/erp/trial-balance-app");
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability.Contains("/erp/trial-balance/snapshot", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
    }

    [Fact]
    public void BusinessUnitDimension_ParsesAndKeepsQuery()
    {
        var parameters = ErpTrialBalanceParameters.FromQuery(
            "2026-01-01", "2026-12-31", null, null, null, null, "business_unit", null, null, null, null, null, "7", "1");
        Assert.Equal(ErpTrialBalanceDimensionSet.MainAccountBusinessUnit, parameters.DimensionSet);
        Assert.Equal(7, parameters.BusinessUnitId);
        Assert.Equal(1, parameters.CompanyId);
        Assert.Contains("dimension_set=business_unit", parameters.QueryString(), StringComparison.Ordinal);
        Assert.Contains("bu=7", parameters.QueryString(), StringComparison.Ordinal);
        Assert.Contains("company=1", parameters.QueryString(), StringComparison.Ordinal);
        Assert.Equal("Main account + business unit", ErpTrialBalanceParameters.DimensionLabel(parameters.DimensionSet));
        Assert.True(ErpTrialBalanceParameters.UsesUnitLedger(parameters.DimensionSet, 0));
    }

    [Fact]
    public void App_EmitsListPageWithoutForbiddenWords()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpTrialBalanceApp.razor"));
        Assert.Contains("@page \"/erp/trial-balance-app\"", text, StringComparison.Ordinal);
        Assert.Contains("Parameters", text, StringComparison.Ordinal);
        Assert.Contains("Date interval", text, StringComparison.Ordinal);
        Assert.Contains("Calculate balances", text, StringComparison.Ordinal);
        Assert.Contains("Financial dimension set", text, StringComparison.Ordinal);
        Assert.Contains("value=\"business_unit\"", text, StringComparison.Ordinal);
        Assert.Contains("In balance", text, StringComparison.Ordinal);
        Assert.Contains("/erp/intercompany-app", text, StringComparison.Ordinal);
        Assert.Contains("Posting layer", text, StringComparison.Ordinal);
        Assert.Contains(">Amount<", text, StringComparison.Ordinal);
        Assert.Contains("AmountTotal.ToString", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CreditTotal).ToString", text, StringComparison.Ordinal);
        Assert.Contains("Accounting source explorer", text, StringComparison.Ordinal);
        Assert.Contains("Cash flow forecasts", text, StringComparison.Ordinal);
        Assert.Contains("Include closing transactions", text, StringComparison.Ordinal);
        Assert.Contains("Trial balance snapshots", text, StringComparison.Ordinal);
        Assert.Contains("Open in Excel", text, StringComparison.Ordinal);
        Assert.Contains("Voucher transactions", text, StringComparison.Ordinal);
        Assert.Contains("ErpTrialBalanceParameters.SnapshotPath", text, StringComparison.Ordinal);
        Assert.Equal("/erp/trial-balance/snapshot", ErpTrialBalanceParameters.SnapshotPath);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaAndRoutes_AreRegistered()
    {
        var schema = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        Assert.Contains("epc_erp_tb_snapshots", schema, StringComparison.Ordinal);
        Assert.Contains("EnsureTrialBalanceSnapshotsAsync", schema, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpTrialBalanceReadService", program, StringComparison.Ordinal);
        Assert.Contains("IErpTrialBalanceWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleTrialBalanceSnapshotAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleTrialBalanceExportAsync", module, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteService_InsertsSnapshotOnThrowawayDb()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_tb_" + Guid.NewGuid().ToString("N")[..12];
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
            var read = new ErpTrialBalanceReadService(connections);
            await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "current", "column", "1", "1", null, null, null, null));
            var before = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var inPeriod = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var yearEnd = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            await using (var connection = await connections.OpenAsync())
            {
                await using var update = connection.CreateCommand();
                update.CommandText = "UPDATE `epc_erp_coa_accounts` SET `opening_balance` = 50 WHERE `code` IN ('1000','2000')";
                await update.ExecuteNonQueryAsync();
                await using var ids = connection.CreateCommand();
                ids.CommandText = "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1000','2000') ORDER BY `code`";
                var coa = new List<long>();
                await using (var reader = await ids.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        coa.Add(reader.GetInt64(0));
                    }
                }

                Assert.Equal(2, coa.Count);
                async Task InsertJournal(string no, long date, string source, string reference, string description, string tax, decimal debit, decimal credit)
                {
                    await using var header = connection.CreateCommand();
                    header.CommandText =
                        "INSERT INTO `epc_erp_gl_journals` (`journal_no`,`journal_date`,`reference`,`description`,`source_type`,`source_id`,`company_id`,`uae_tax_legislation_ref`,`admin_id`,`active`,`time_created`) " +
                        "VALUES (@no,@date,@ref,@desc,@src,0,1,@tax,1,1,@date)";
                    header.Parameters.Add(new MySqlParameter("@no", no));
                    header.Parameters.Add(new MySqlParameter("@date", date));
                    header.Parameters.Add(new MySqlParameter("@ref", reference));
                    header.Parameters.Add(new MySqlParameter("@desc", description));
                    header.Parameters.Add(new MySqlParameter("@src", source));
                    header.Parameters.Add(new MySqlParameter("@tax", tax));
                    await header.ExecuteNonQueryAsync();
                    var journalId = 0L;
                    await using (var last = connection.CreateCommand())
                    {
                        last.CommandText = "SELECT LAST_INSERT_ID()";
                        journalId = Convert.ToInt64(await last.ExecuteScalarAsync());
                    }

                    await using var line1 = connection.CreateCommand();
                    line1.CommandText = "INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (@j,@coa,@d,@c,'')";
                    line1.Parameters.Add(new MySqlParameter("@j", journalId));
                    line1.Parameters.Add(new MySqlParameter("@coa", coa[0]));
                    line1.Parameters.Add(Money("@d", debit));
                    line1.Parameters.Add(Money("@c", 0m));
                    await line1.ExecuteNonQueryAsync();
                    await using var line2 = connection.CreateCommand();
                    line2.CommandText = "INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (@j,@coa,@d,@c,'')";
                    line2.Parameters.Add(new MySqlParameter("@j", journalId));
                    line2.Parameters.Add(new MySqlParameter("@coa", coa[1]));
                    line2.Parameters.Add(Money("@d", 0m));
                    line2.Parameters.Add(Money("@c", credit));
                    await line2.ExecuteNonQueryAsync();
                }

                await InsertJournal("OB-1", before, "opening", "open", "prior year", "", 20m, 20m);
                await InsertJournal("GV-1", inPeriod, "manual", "rent", "February rent", "", 40m, 40m);
                await InsertJournal("YE-1", yearEnd, "adjustment", "year-end close", "year-end close", "", 8m, 8m);
                await InsertJournal("TX-1", inPeriod, "manual", "vat", "VAT journal", "AE-VAT", 3m, 3m);
            }

            var writes = new ErpTrialBalanceWriteService(connections, read);
            var saved = await writes.SaveSnapshotAsync(new ErpTrialBalanceSnapshotRequest(
                FromDate: "2026-01-01",
                ToDate: "2026-12-31",
                TrackingId: "TB-TEST"));
            Assert.True(saved.Succeeded, saved.Message);
            Assert.True(saved.Id > 0);

            var full = await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "current", "column", "1", "1", null, null, null, "1000"));
            var cash = Assert.Single(full.Lines, line => line.Code == "1000");
            Assert.Equal(70m, cash.Opening);
            Assert.Equal(51m, cash.Debit);
            Assert.Equal(121m, cash.Closing);
            Assert.Contains(full.Snapshots, snap => snap.TrackingId == "TB-TEST");

            var noClose = await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "current", "column", "0", "1", null, null, null, null));
            var cashNoClose = Assert.Single(noClose.Lines, line => line.Code == "1000");
            Assert.Equal(43m, cashNoClose.Debit);

            var operations = await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "operations", "column", "1", "1", null, null, null, null));
            var cashOps = Assert.Single(operations.Lines, line => line.Code == "1000");
            Assert.Equal(48m, cashOps.Debit);

            var tax = await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "tax", "column", "1", "1", null, null, "1000", null));
            var cashTax = Assert.Single(tax.Lines, line => line.Code == "1000");
            Assert.Equal(3m, cashTax.Debit);
            Assert.Contains(tax.Vouchers, v => v.JournalNo == "TX-1");

            await using (var connection = await connections.OpenAsync())
            {
                await using var ids = connection.CreateCommand();
                ids.CommandText = "SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` IN ('1000','2000') ORDER BY `code`";
                var coa = new List<long>();
                await using (var reader = await ids.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        coa.Add(reader.GetInt64(0));
                    }
                }

                await using var header = connection.CreateCommand();
                header.CommandText =
                    "INSERT INTO `epc_erp_gl_journals` (`journal_no`,`journal_date`,`reference`,`description`,`source_type`,`source_id`,`company_id`,`business_unit_id`,`admin_id`,`active`,`time_created`) " +
                    "VALUES ('BU-7',@date,'unit','Business unit rent','manual',0,1,7,1,1,@date)";
                header.Parameters.Add(new MySqlParameter("@date", inPeriod));
                await header.ExecuteNonQueryAsync();
                long journalId;
                await using (var last = connection.CreateCommand())
                {
                    last.CommandText = "SELECT LAST_INSERT_ID()";
                    journalId = Convert.ToInt64(await last.ExecuteScalarAsync());
                }

                await using var line1 = connection.CreateCommand();
                line1.CommandText = "INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (@j,@coa,15,0,'')";
                line1.Parameters.Add(new MySqlParameter("@j", journalId));
                line1.Parameters.Add(new MySqlParameter("@coa", coa[0]));
                await line1.ExecuteNonQueryAsync();
                await using var line2 = connection.CreateCommand();
                line2.CommandText = "INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (@j,@coa,0,15,'')";
                line2.Parameters.Add(new MySqlParameter("@j", journalId));
                line2.Parameters.Add(new MySqlParameter("@coa", coa[1]));
                await line2.ExecuteNonQueryAsync();
            }

            var unitTb = await read.ReadAsync(ErpTrialBalanceParameters.FromQuery(
                "2026-01-01", "2026-12-31", "current", "column", "1", "1", "business_unit", null, null, null, null, null, "7", "1"));
            Assert.Equal(15m, unitTb.DebitTotal);
            Assert.Equal(15m, unitTb.CreditTotal);
            Assert.Equal(0m, unitTb.OpeningTotal);
            Assert.Equal(0m, unitTb.ClosingTotal);
            var unitCash = Assert.Single(unitTb.Lines, line => line.Code == "1000");
            Assert.Equal("7", unitCash.Company);
            Assert.Equal(0m, unitCash.Opening);
            Assert.Equal(15m, unitCash.Debit);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static MySqlParameter Money(string name, decimal value)
        => new(name, MySqlDbType.Decimal) { Value = value };

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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
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
