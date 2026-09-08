using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUaeTaxLegislationAskPhpParityTests
{
    [Fact]
    public void Tokenize_DropsStopwordsAndKeepsVatRatePercent()
    {
        var tokens = ErpUaeTaxLegislationAsk.Tokenize("Please tell me the VAT rate 5%");
        Assert.Contains("vat", tokens);
        Assert.Contains("rate", tokens);
        Assert.Contains("5%", tokens);
        Assert.DoesNotContain("please", tokens);
        Assert.DoesNotContain("tell", tokens);
        Assert.DoesNotContain("the", tokens);
    }

    [Fact]
    public void IntentBoosts_VatRateAndCorporateTax()
    {
        var vat = ErpUaeTaxLegislationAsk.IntentBoosts("What is the VAT rate?");
        Assert.Equal(25, vat["vat-decree-8-2017"]);
        Assert.True(vat["__tax_vat"] > 0);

        var ct = ErpUaeTaxLegislationAsk.IntentBoosts("Corporate tax threshold 375000");
        Assert.Equal(24, ct["corporate-tax-47-2022"]);
        Assert.True(ct.ContainsKey("ct-executive-regulation"));
    }

    [Fact]
    public void ScoreAndSynthesize_VatRateCitesDecreeLaw8()
    {
        var item = ErpUaeTaxFtaLegislation.Enrich(new ErpUaeTaxFtaItem(
            "vat-8",
            "/Datafolder/vat-decree-8.pdf",
            "Federal Decree-Law No. 8 of 2017 on Value Added Tax",
            "01 Jan 2018",
            "02 Jan 2018",
            "Laws",
            "vat",
            "/Datafolder/vat-decree-8.pdf"));
        const string q = "What is the VAT rate?";
        var tokens = ErpUaeTaxLegislationAsk.Tokenize(q);
        var boosts = ErpUaeTaxLegislationAsk.IntentBoosts(q);
        Assert.True(ErpUaeTaxLegislationAsk.ScoreItem(item, tokens, boosts) > 20);
        var answer = ErpUaeTaxLegislationAsk.Answer(q, [item]);
        Assert.True(answer.Ok);
        Assert.Contains(answer.Answer, line => line.Contains("5%", StringComparison.Ordinal));
        Assert.Contains(answer.Answer, line => line.Contains("Decree-Law No. 8", StringComparison.Ordinal));
        Assert.Equal("Empty question", ErpUaeTaxLegislationAsk.Answer("", [item]).Message);
        Assert.Equal("No legislation items", ErpUaeTaxLegislationAsk.Answer(q, []).Message);
        Assert.Equal("Indicative only; confirm with tax advisor and FTA EmaraTax.", answer.Disclaimer);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpUaeTaxLegislationAskDryRun().Evaluate(new ErpUaeTaxLegislationAskRequest(Question: "VAT rate"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpUaeTaxLegislationAskDryRun().Evaluate(new ErpUaeTaxLegislationAskRequest(ConfirmWrites: true, Question: "VAT rate")).ValidationCode);

        var regen = new ErpUaeTaxLegislationRegenSummariesDryRun().Evaluate(new ErpUaeTaxLegislationRegenSummariesRequest());
        Assert.Equal("dry-run-validated", regen.Status);
        Assert.Equal(0, regen.Writes);
        Assert.False(regen.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpUaeTaxLegislationRegenSummariesDryRun().Evaluate(new ErpUaeTaxLegislationRegenSummariesRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void UaeTaxApp_PostsNativeAskAndRegenForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("/erp/uae-tax/legislation/ask", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/legislation/regen", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"question\"", text, StringComparison.Ordinal);
        Assert.Contains("Ask legislation", text, StringComparison.Ordinal);
        Assert.Contains("Regen summaries", text, StringComparison.Ordinal);
        Assert.Contains("PDF excerpts stay on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAskAndRegenWriteServices()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpUaeTaxLegislationAskWriteService", text, StringComparison.Ordinal);
        Assert.Contains("IErpUaeTaxLegislationRegenWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAskAndRegenLiveGated()
    {
        var ask = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/uae-tax/legislation/ask");
        Assert.Equal("write-live-gated", ask.Status);
        Assert.Contains("epc_uae_tax_legislation_ask", ask.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", ask.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", ask.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/uae-tax-legislation-ask").Status);

        var regen = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/uae-tax/legislation/regen");
        Assert.Equal("write-live-gated", regen.Status);
        Assert.Contains("epc_uae_tax_legislation_backfill_summaries", regen.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/uae-tax-legislation-regen-summaries").Status);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpUaeTaxLegislationAsk", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpUaeTaxLegislationRegen", text, StringComparison.Ordinal);
        Assert.Contains("HandleUaeTaxLegislationAskAsync", text, StringComparison.Ordinal);
        Assert.Contains("HandleUaeTaxLegislationRegenAsync", text, StringComparison.Ordinal);
        var ask = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxLegislationAskWriteService.cs"));
        var askCore = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxLegislationAsk.cs"));
        var regen = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxLegislationRegenWriteService.cs"));
        var lib = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpUaeTaxLegislationLibrary.cs"));
        Assert.Contains("legislation_qa_history", askCore, StringComparison.Ordinal);
        Assert.Contains("HistoryCacheKey", ask, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", lib, StringComparison.Ordinal);
        Assert.Contains("PDF excerpts stay on the Classic twin", regen, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", ask, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", regen, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", lib, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_uae_tax_compliance_ensure_schema", ask, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_uae_tax_compliance_ensure_schema", regen, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemsFromCacheJson_ReadsFtaPayload()
    {
        const string json = """{"legislation":[{"item_key":"k1","slug":"s1","title":"Federal Decree-Law No. 8 of 2017","issue_date":"01 Jan 2018","tax_category":"vat","erp_summary":"5% VAT"}]}""";
        var items = ErpUaeTaxFtaLegislation.ItemsFromCacheJson(json);
        Assert.Single(items);
        Assert.Equal("k1", items[0].ItemKey);
        Assert.Contains("5% VAT", items[0].ErpSummary, StringComparison.Ordinal);
    }

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
