using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/marketing-app live create without inventing campaign update/delete.</summary>
public sealed class ErpMarketingPhpParityTests
{
    [Fact]
    public void ErpMarketingApp_EmitsWriteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpMarketingApp.razor"));
        Assert.Contains("/erp/marketing/create", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Create campaign", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterMarketingWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpMarketingCreate", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/marketing/create", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMarketingWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpMarketingCreateDryRun", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpMarketingWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new ErpMarketingCreateDryRun();
        var blocked = dry.Evaluate(new ErpMarketingCreateRequest("Spring promo"));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var empty = dry.Evaluate(new ErpMarketingCreateRequest(""));
        Assert.Equal("Campaign", empty.Name);
        var refused = dry.Evaluate(new ErpMarketingCreateRequest("Spring promo", true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/marketing/create", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_erp_marketing_create", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void DateDefaults_MatchPhp()
    {
        const long now = 1_788_547_200;
        Assert.Equal(now, ErpMarketingWriteService.ResolveStartUnix("", now));
        Assert.Equal(now + 86400L * 30, ErpMarketingWriteService.ResolveEndUnix("", now));
        Assert.Equal(1_788_480_000, ErpMarketingWriteService.ResolveStartUnix("2026-09-04", now));
        Assert.Equal(1_788_566_399, ErpMarketingWriteService.ResolveEndUnix("2026-09-04", now));
        Assert.Contains("paused", ErpMarketingWriteService.AllowedStatuses);
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
