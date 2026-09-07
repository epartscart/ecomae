using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_wht_certificate_issue</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpWhtCertificatePhpParityTests
{
    [Fact]
    public void WithholdingApp_PostsNativeCertificateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWithholdingApp.razor"));
        Assert.Contains("action=\"/erp/withholding/txns/certificate\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"certificate_no\"", text, StringComparison.Ordinal);
        Assert.Contains("Issue certificate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Certificate minting stays on the classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCertificateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWhtCertificateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpWhtCertificateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCertificateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/withholding/txns/certificate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_wht_certificate_issue", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/wht-certificate");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpWhtCertificateDryRun().Evaluate(new ErpWhtCertificateRequest(Id: 1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpWhtCertificateDryRun().Evaluate(new ErpWhtCertificateRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Transaction not found", missing.Detail);

        var confirm = new ErpWhtCertificateDryRun().Evaluate(new ErpWhtCertificateRequest(Id: 1, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void DefaultCertNo_MatchesPhpPad()
    {
        Assert.Equal("WHT-2026-00001", ErpWhtCertificateWriteService.DefaultCertNo(1, 2026));
        Assert.Equal("WHT-2026-00042", ErpWhtCertificateWriteService.DefaultCertNo(42, 2026));
        Assert.Equal("WHT-2026-12345", ErpWhtCertificateWriteService.DefaultCertNo(12345, 2026));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpWithholdingTxnsCertificate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxWhtCertificate", text, StringComparison.Ordinal);
        Assert.Contains("IErpWhtCertificateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleWhtCertificateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWhtCertificateWriteService.cs"));
        Assert.Contains("Certificate issued: ", service, StringComparison.Ordinal);
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
