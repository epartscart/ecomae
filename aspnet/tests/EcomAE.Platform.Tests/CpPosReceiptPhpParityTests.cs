using System.Reflection;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards printable POS receipt HTML (PHP epc_pos_receipt_html twin).
/// </summary>
public sealed class CpPosReceiptPhpParityTests
{
    [Fact]
    public void FormatQty_MatchesPhpRtrimNumberFormat()
    {
        Assert.Equal("1", CpPosWriteService.FormatQty(1m));
        Assert.Equal("1.25", CpPosWriteService.FormatQty(1.250m));
        Assert.Equal("2.5", CpPosWriteService.FormatQty(2.5m));
        Assert.Equal("0.001", CpPosWriteService.FormatQty(0.001m));
    }

    [Fact]
    public void CpPosReceiptApp_IsPrintableWithoutBlazorClicks()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPosReceiptApp.razor"));
        Assert.Contains("@page \"/cp/pos/receipt/{SaleId:long}\"", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-pos-receipt", text, StringComparison.Ordinal);
        Assert.Contains("onclick=\"window.print()\"", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CpPosOverviewApp_LinksReceipt()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor"));
        Assert.Contains("/cp/pos/receipt/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt HTML stay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterPosReceipt()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("/cp/pos/receipt/{saleId:long}", routes, StringComparison.Ordinal);
        var catalog = SurfacePayloadContractCatalog.Functions;
        Assert.Contains("/cp/pos/receipt/{id}", catalog.First(f => f.AspNetRouteOrCapability == "/cp/pos-overview-app").Notes, StringComparison.Ordinal);
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
