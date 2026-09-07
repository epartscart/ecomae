using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/contracts/save live writes without inventing sign or OCR.</summary>
public sealed class ErpContractSavePhpParityTests
{
    [Fact]
    public void ErpContractsApp_EmitsSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContractsApp.razor"));
        Assert.Contains("/erp/contracts/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/ctr-status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Save contract", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterContractSaveWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpContractsSave", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/contracts/save", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpContractSaveWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpContractSaveWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/contracts/save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_ctr_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void DateDefault_IsZeroAndYmdParses()
    {
        Assert.Equal(0, ErpContractSaveWriteService.ResolveUnix(""));
        Assert.Equal(1_788_480_000, ErpContractSaveWriteService.ResolveUnix("2026-09-04"));
        Assert.Equal(1_788_480_000, ErpContractSaveWriteService.ResolveUnix("1788480000"));
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
