using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/ajax/ins-claim-add live writes without inventing schema ensure.</summary>
public sealed class ErpInsClaimAddPhpParityTests
{
    [Fact]
    public void InsuranceApp_EmitsLogClaimForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("/erp/ajax/ins-claim-add", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/ins-claim-status", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/ins-doc-delete", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Log claim", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterClaimAddWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpAjaxInsClaimAdd", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/ins-claim-add", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpInsClaimAddWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpInsClaimAddWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/ajax/ins-claim-add", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_ins_claim_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("notified", ErpInsClaimAddWriteService.NormalizeStatus(""));
        Assert.Equal("notified", ErpInsClaimAddWriteService.NormalizeStatus("nope"));
        Assert.Equal("settled", ErpInsClaimAddWriteService.NormalizeStatus("settled"));
        Assert.Equal(0, ErpInsClaimAddWriteService.ResolveDateUnix(""));
        Assert.Equal(1_788_480_000, ErpInsClaimAddWriteService.ResolveDateUnix("2026-09-04"));
        Assert.Equal(1_700_000_000, ErpInsClaimAddWriteService.ResolveNotifiedUnix("", 1_700_000_000));
        Assert.Equal(1_788_480_000, ErpInsClaimAddWriteService.ResolveNotifiedUnix("2026-09-04", 1_700_000_000));
    }

    [Fact]
    public void DryRun_ValidatesAndRefusesConfirm()
    {
        var ok = new ErpInsClaimAddDryRun().Evaluate(new ErpInsClaimAddRequest(0, 3, "CL-1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missing = new ErpInsClaimAddDryRun().Evaluate(new ErpInsClaimAddRequest(-1));
        Assert.Equal("invalid_request", missing.ValidationCode);
        var refused = new ErpInsClaimAddDryRun().Evaluate(new ErpInsClaimAddRequest(0, 3, "CL-1", true));
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
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

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
