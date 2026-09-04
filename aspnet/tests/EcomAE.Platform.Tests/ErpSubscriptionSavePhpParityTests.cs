using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/subscriptions/save live writes without inventing cycle generate.</summary>
public sealed class ErpSubscriptionSavePhpParityTests
{
    [Fact]
    public void SalesOrdersApp_EmitsSubscriptionSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("/erp/subscriptions/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/subscriptions/status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Save subscription", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterSubscriptionSaveWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpSubscriptionsSave", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/subscriptions/save", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpSubscriptionSaveWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpSubscriptionSaveWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/subscriptions/save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_sub_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("monthly", ErpSubscriptionSaveWriteService.NormalizeCycle("bogus"));
        Assert.Equal("annual", ErpSubscriptionSaveWriteService.NormalizeCycle("annual"));
        const long now = 1_788_547_200;
        Assert.Equal(now, ErpSubscriptionSaveWriteService.ResolveStartUnix("", now));
        Assert.Equal(1_788_480_000, ErpSubscriptionSaveWriteService.ResolveStartUnix("2026-09-04", now));
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
