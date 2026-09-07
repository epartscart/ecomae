using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_wht_record</c> twin: SSR form, DI, catalog.
/// Certificate minting and schema ensure stay PHP.
/// </summary>
public sealed class ErpWhtRecordPhpParityTests
{
    [Fact]
    public void WithholdingApp_PostsNativeRecordForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWithholdingApp.razor"));
        Assert.Contains("action=\"/erp/withholding/txns/record\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"base_amount\"", text, StringComparison.Ordinal);
        Assert.Contains("Apply withholding", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Record and certificate minting stay on the classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersRecordWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWhtRecordWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpWhtRecordWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksRecordLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/withholding/txns/record");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_wht_record", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/wht-record");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpWhtRecordDryRun().Evaluate(new ErpWhtRecordRequest(CodeId: 1, BaseAmount: 10000));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missingCode = new ErpWhtRecordDryRun().Evaluate(new ErpWhtRecordRequest());
        Assert.Equal("invalid_request", missingCode.ValidationCode);
        Assert.Equal("Withholding code not found", missingCode.Detail);

        var baseZero = new ErpWhtRecordDryRun().Evaluate(new ErpWhtRecordRequest(CodeId: 1, BaseAmount: 0));
        Assert.Equal("invalid_request", baseZero.ValidationCode);
        Assert.Equal("Base amount must be positive", baseZero.Detail);

        var confirm = new ErpWhtRecordDryRun().Evaluate(new ErpWhtRecordRequest(ConfirmWrites: true, CodeId: 1, BaseAmount: 100));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Calc_MatchesPhpRoundHalfUp()
    {
        Assert.Equal(500.00m, ErpWhtRecordWriteService.Calc(10000, 5));
        Assert.Equal(250.00m, ErpWhtRecordWriteService.Calc(2500, 10));
        Assert.Equal(12.35m, ErpWhtRecordWriteService.Calc(123.45m, 10));
        Assert.Equal("Withholding code not found", ErpWhtRecordWriteService.Validate(0, 100));
        Assert.Equal("Base amount must be positive", ErpWhtRecordWriteService.Validate(1, 0));
        Assert.Null(ErpWhtRecordWriteService.Validate(1, 0.01m));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpWithholdingTxnsRecord", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxWhtRecord", text, StringComparison.Ordinal);
        Assert.Contains("IErpWhtRecordWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleWhtRecordAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWhtRecordWriteService.cs"));
        Assert.Contains("Withholding applied", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
