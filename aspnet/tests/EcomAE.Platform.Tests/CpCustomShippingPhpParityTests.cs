using System.Reflection;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/carriers-app custom-shipping save / submit writes.
/// </summary>
public sealed class CpCustomShippingPhpParityTests
{
    [Fact]
    public void CpCarriersApp_EmitsCustomShippingWriteForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCarriersApp.razor"));
        Assert.Contains("/cp/custom-shipping/write", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"save\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"submit\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"itemsJson\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"declarationId\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"blNumber\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"invoiceAmountAed\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"totalCostAed\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"supplierDetail\"", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-cs-save", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-cs-list", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.Contains("/CP/shop/logistics/carriers", text, StringComparison.Ordinal);
        Assert.Contains("/ERP/?epc_erp_shell=1&area=logistics&tab=custom_shipping", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/shop/finance/epc_custom_shipping\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterCustomShippingWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("CpCustomShippingWrite", routes, StringComparison.Ordinal);
        Assert.Contains("/cp/custom-shipping/write", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCustomShippingWriteService", program, StringComparison.Ordinal);
        Assert.Contains("ICpCustomShippingWriteDryRun", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCustomShippingWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new CpCustomShippingWriteDryRun();
        var blocked = dry.Evaluate(new CpCustomShippingWriteRequest("save", false));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var refused = dry.Evaluate(new CpCustomShippingWriteRequest("save", true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void TypesByCategory_MatchPhpFirstImportType()
    {
        Assert.Equal("Import to Local from ROW", CpCustomShippingWriteService.TypesByCategory["import"][0]);
        Assert.Contains("Export statisitical Declaration", CpCustomShippingWriteService.TypesByCategory["export"]);
        Assert.Contains("Temporay Export from local to FZ", CpCustomShippingWriteService.TypesByCategory["export"]);
    }

    [Fact]
    public void ValidateItems_MatchesPhpMessages()
    {
        Assert.Equal(
            "Add at least one declaration line item (HS code, country of origin, quantity).",
            CpCustomShippingWriteService.ValidateItems([]));
        var missingHs = CpCustomShippingWriteService.NormalizeItems(
            [new CpCustomShippingLineInput("", "AE", "Oil", 1)]);
        Assert.Equal("Line 1: HS code is required", CpCustomShippingWriteService.ValidateItems(missingHs));
        var parsed = CpCustomShippingWriteService.ParseItemsJson(
            """[{"hs_code":"8708.99","country_of_origin":"AE","qty":2}]""");
        Assert.Single(parsed);
        Assert.Equal(2, parsed[0].Quantity);
        Assert.Null(CpCustomShippingWriteService.ValidateItems(CpCustomShippingWriteService.NormalizeItems(parsed)));
    }

    [Fact]
    public void Catalog_KeepsDigestWiredStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var shell = catalog.First(item => item.AspNetRouteOrCapability.Contains("/cp/carriers-app", StringComparison.Ordinal));
        Assert.Equal("digest-wired-awaiting-dual-sample", shell.Status);
        Assert.Contains("/cp/custom-shipping/write", shell.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", catalog.First(f => f.AspNetRouteOrCapability == "/cp/custom-shipping/write").Status);
        Assert.True(EcomAE.Platform.Presentation.ErpPhpTabRouteMap.TryMapTab("custom_shipping", out var erpHref));
        Assert.Equal("/cp/carriers-app", erpHref);
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
