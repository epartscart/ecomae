using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeTaxFtaFetchPhpParityTests
{
    [Fact]
    public void TaxCategory_MatchesPhpFamilies()
    {
        Assert.Equal("vat", ErpUaeTaxFtaLegislation.TaxCategory("Federal Decree-Law No. 8 of 2017 on Value Added Tax", "Laws"));
        Assert.Equal("corporate_tax", ErpUaeTaxFtaLegislation.TaxCategory("Federal Decree-Law No. 47 of 2022 on Corporate Tax", "Laws"));
        Assert.Equal("einvoicing", ErpUaeTaxFtaLegislation.TaxCategory("E-invoicing and Peppol PINT-AE mandate", "Decisions"));
        Assert.Equal("excise", ErpUaeTaxFtaLegislation.TaxCategory("Federal Decree-Law on Excise Tax", "Laws"));
        Assert.Equal("procedures", ErpUaeTaxFtaLegislation.TaxCategory("Tax Procedures Law", "Laws"));
    }

    [Fact]
    public void ParseListHtml_ReadsCategoryTitleDatesPdfAndNewBadge()
    {
        var items = ErpUaeTaxFtaLegislation.ParseListHtml(FixtureHtml());
        Assert.Equal(3, items.Count);
        Assert.Equal("Laws", items[0].Category);
        Assert.Contains("Value Added Tax", items[0].Title, StringComparison.Ordinal);
        Assert.Equal("01 Jan 2018", items[0].IssueDate);
        Assert.Equal("02 Jan 2018", items[0].PublishDate);
        Assert.Equal("/Datafolder/vat-decree-8.pdf", items[0].PdfUrl);
        Assert.Equal("vat", items[0].TaxCategory);
        Assert.True(items[0].FtaNewBadge);
        Assert.Equal("einvoicing", items[1].TaxCategory);
        Assert.Equal("corporate_tax", items[2].TaxCategory);
        Assert.Equal("/Datafolder/vat-decree-8.pdf", items[0].ItemKey);
    }

    [Fact]
    public void Enrich_BuildsTopicActionsForEinvoiceAndCt()
    {
        var raw = new ErpUaeTaxFtaItem("slug", "k", "E-invoicing Peppol PINT-AE decision", "01 Jan 2026", "", "Decisions", "einvoicing", "");
        var ei = ErpUaeTaxFtaLegislation.Enrich(raw);
        Assert.Equal("einvoicing-decision", ei.PatternKey);
        Assert.Contains(ei.ComplianceActions ?? [], a => a.Contains("0235:TIN", StringComparison.Ordinal));
        Assert.Contains("PINT-AE", ei.ErpSummary, StringComparison.Ordinal);

        var ct = ErpUaeTaxFtaLegislation.Enrich(raw with { Title = "Federal Decree-Law No. 47 of 2022 on Corporate Tax", Category = "Laws" });
        Assert.Equal("corporate-tax-47-2022", ct.PatternKey);
        Assert.Equal("corporate_tax", ct.TaxCategory);
        Assert.Contains(ct.ComplianceActions ?? [], a => a.Contains("375,000", StringComparison.Ordinal));
    }

    [Fact]
    public void MarkDiffFlags_SetsNewAndChanged()
    {
        var items = new List<ErpUaeTaxFtaItem>
        {
            new("a", "key-a", "Title A", "01 Jan 2026", "", "Laws", "vat", ""),
            new("b", "key-b", "Title B", "02 Feb 2026", "", "Laws", "vat", ""),
        };
        ErpUaeTaxFtaLegislation.MarkDiffFlags(items, new Dictionary<string, string> { ["key-a"] = "01 Jan 2025" });
        Assert.True(items[0].IsChanged);
        Assert.True(items[0].IsUpdated);
        Assert.True(items[1].IsNew);
    }

    [Fact]
    public void CacheHasLegislation_AndPrevKeys()
    {
        const string json = """{"legislation":[{"item_key":"k1","issue_date":"01 Jan 2026"},{"slug":"s2","issue_date":"02 Feb 2026"}]}""";
        Assert.True(ErpUaeTaxFtaLegislation.CacheHasLegislation(json));
        Assert.False(ErpUaeTaxFtaLegislation.CacheHasLegislation("{}"));
        var prev = ErpUaeTaxFtaLegislation.PrevKeysFromPayloadJson(json);
        Assert.Equal("01 Jan 2026", prev["k1"]);
        Assert.Equal("02 Feb 2026", prev["s2"]);
    }

    [Fact]
    public void ExtractFormFields_SkipsSubmitButtons()
    {
        var fields = ErpUaeTaxFtaLegislation.ExtractFormFields(
            """<input name="__VIEWSTATE" value="abc" /><input type="submit" name="btnSearch" value="Go" /><input name="q" value="vat" />""");
        Assert.Equal("abc", fields["__VIEWSTATE"]);
        Assert.Equal("vat", fields["q"]);
        Assert.False(fields.ContainsKey("btnSearch"));
    }

    [Fact]
    public void ReadPagerMeta_ReadsItemsAndPageCount()
    {
        var (total, pages) = ErpUaeTaxFtaLegislation.ReadPagerMeta("""120 Items found {"_pageCount":4}""");
        Assert.Equal(120, total);
        Assert.Equal(4, pages);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpUaeTaxFtaFetchDryRun().Evaluate(new ErpUaeTaxFtaFetchRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpUaeTaxFtaFetchDryRun().Evaluate(new ErpUaeTaxFtaFetchRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void UaeTaxEinvoiceAml_PostNativeFtaFetchForm()
    {
        foreach (var relative in new[]
        {
            "aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAmlComplianceApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpVatApp.razor",
        })
        {
            var text = File.ReadAllText(FindRepoFile(relative));
            Assert.Contains("/erp/uae-tax/fta-fetch", text, StringComparison.Ordinal);
            Assert.Contains("ClientErpPath.PrefixIfBound", text, StringComparison.Ordinal);
            Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
            Assert.Contains("name=\"force\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Legislation ingest stays on the Classic twin", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Program_RegistersFtaFetchWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpUaeTaxFtaFetchWriteService", text, StringComparison.Ordinal);
        Assert.Contains("IErpUaeFtaHttpClient", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFtaFetchLiveGated()
    {
        var form = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/uae-tax/fta-fetch");
        Assert.Equal("write-live-gated", form.Status);
        Assert.Contains("epc_uae_fta_fetch_legislation_updates", form.Notes, StringComparison.Ordinal);
        Assert.Contains("core UAE library", form.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", form.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/uae-tax-fta-fetch").Status);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpUaeTaxFtaFetch", text, StringComparison.Ordinal);
        Assert.Contains("HandleUaeTaxFtaFetchAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxFtaFetchWriteService.cs"));
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("fta_legislation_index", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxFtaLegislation.cs")), StringComparison.Ordinal);
        Assert.Contains("CacheKey", service, StringComparison.Ordinal);
        Assert.Contains("ErpUaeTaxComplianceSchema.EnsureSchemaAsync", service, StringComparison.Ordinal);
        Assert.Contains("CoreLibrarySeed", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_uae_tax_compliance_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreLibrarySeed_CoversVatCtEinvoiceExciseAndProcedures()
    {
        var items = ErpUaeTaxFtaLegislation.CoreLibrarySeed();
        Assert.True(items.Count >= 8);
        Assert.Contains(items, i => i.TaxCategory == "vat");
        Assert.Contains(items, i => i.TaxCategory == "corporate_tax");
        Assert.Contains(items, i => i.TaxCategory == "einvoicing");
        Assert.Contains(items, i => i.TaxCategory == "excise");
        Assert.Contains(items, i => i.TaxCategory == "procedures");
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.ErpSummary)));
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.ItemKey)));
    }

    [Fact]
    public void ComplianceApp_IsPresentableAndKeepsFetchForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("epc-tax-fetch", text, StringComparison.Ordinal);
        Assert.Contains("epc-tax-kpis", text, StringComparison.Ordinal);
        Assert.Contains("Fetch FTA legislation", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("ClientErpPathRewriteMiddleware.OriginalPathItem", text, StringComparison.Ordinal);
        Assert.Contains("name=\"csrf_guard_key\"", text, StringComparison.Ordinal);
        Assert.Contains("FetchAction", text, StringComparison.Ordinal);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
            "/cp/client-erp/indj/erp/uae-tax-compliance-app", out _));
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
            "/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", out var mapped));
        Assert.Equal("/erp/uae-tax-compliance-app", mapped);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaHelper_CreatesTheFourLegislationTables()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxComplianceSchema.cs"));
        Assert.Contains("epc_uae_tax_compliance_cache", text, StringComparison.Ordinal);
        Assert.Contains("epc_uae_tax_legislation_items", text, StringComparison.Ordinal);
        Assert.Contains("epc_uae_ct_adjustments", text, StringComparison.Ordinal);
        Assert.Contains("epc_uae_tax_legislation_checklist", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS", text, StringComparison.Ordinal);
        Assert.Contains("EnsureVatRefundsAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CacheMaxAge_Is24Hours()
    {
        Assert.Equal(86400, ErpUaeTaxFtaLegislation.CacheMaxAgeSeconds);
        Assert.Equal("https://tax.gov.ae/en/legislation.aspx", ErpUaeTaxFtaLegislation.LegislationUrl);
        Assert.Equal("ePartsCart-ERP-UAE-Tax-Compliance/2.0", ErpUaeTaxFtaLegislation.UserAgent);
    }

    [Fact]
    public void HrOverview_ShowsLabourLawPackFetch()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("Fetch labour-law pack", text, StringComparison.Ordinal);
        Assert.Contains("name=\"law_country\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"law_fetch\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpHrLabourLawPack", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.Equal("AE", ErpHrLabourLawPack.Resolve("AE").Code);
        Assert.Contains("mohre.gov.ae", ErpHrLabourLawPack.AuthorityUrl("AE"), StringComparison.Ordinal);
        Assert.Equal("generic", ErpHrLabourLawPack.Resolve("ZZ").Code);
    }

    [Fact]
    public void HrLawTab_MapsToStatutoryView()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/ErpPhpTabRouteMap.cs"));
        Assert.Contains("[\"hr_law\"] = \"/erp/hr-overview-app?hv=statutory\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetch_OnThrowawayDatabase_CreatesSchemaAndLoadsCoreLibrary()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_cpw_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using var adminConnection = new MySqlConnection(admin);
        await adminConnection.OpenAsync();
        await using (var create = adminConnection.CreateCommand())
        {
            create.CommandText = "CREATE DATABASE `" + database + "`";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        try
        {
            var writes = new ErpUaeTaxFtaFetchWriteService(new FixedConnections(connectionString), new EmptyFtaHttp());
            var result = await writes.FetchAsync(new ErpUaeTaxFtaFetchWriteRequest(Force: true));
            Assert.True(result.Succeeded, result.Message);
            Assert.True(result.ItemCount >= 8, result.Message);
            Assert.Contains("core UAE", result.Message, StringComparison.Ordinal);
            Assert.Equal(result.ItemCount.ToString(CultureInfo.InvariantCulture), await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_uae_tax_legislation_items"));
            Assert.True(Convert.ToInt32(await ScalarAsync(connectionString, "SELECT COUNT(*) FROM epc_uae_tax_compliance_cache"), CultureInfo.InvariantCulture) >= 1);
        }
        finally
        {
            await using var drop = adminConnection.CreateCommand();
            drop.CommandText = "DROP DATABASE IF EXISTS `" + database + "`";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class EmptyFtaHttp : IErpUaeFtaHttpClient
    {
        public Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class FixedConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public FixedConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
    }

    private static string FixtureHtml() =>
        """
        headerTable
        <div class="d-flex">Federal Decree-Law No. 8 of 2017 on Value Added Tax<div class="clear5"></div>
        <span>Issue Date :</span> <span class="lastmodifiedDate">01 Jan 2018</span>
        <span>Publish Date :</span> <span class="lastmodifiedDate">02 Jan 2018</span>
        <span class="newicon">New</span>
        <span class="tag_category">Laws</span>
        <a href="/Datafolder/vat-decree-8.pdf">PDF</a>
        <div class="d-flex">Cabinet Decision on E-invoicing and Peppol PINT-AE<div class="clear5"></div>
        <span>Issue Date :</span> <span class="lastmodifiedDate">15 Mar 2026</span>
        <span class="tag_category">Decisions</span>
        <a href="/Datafolder/pint-ae.pdf">PDF</a>
        <div class="d-flex">Federal Decree-Law No. 47 of 2022 on Corporate Tax<div class="clear5"></div>
        <span>Issue Date :</span> <span class="lastmodifiedDate">01 Dec 2022</span>
        <span class="tag_category">Laws</span>
        <a href="/Datafolder/ct-47.pdf">PDF</a>
        ctrlContentArea_ctlOpenData_dvPager
        """;

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt)) return alt;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
