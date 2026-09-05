using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/ajax/cons-entity-save live writes without inventing schema ensure.</summary>
public sealed class ErpConsEntitySavePhpParityTests
{
    [Fact]
    public void ConsolidationsApp_EmitsSaveEntityForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));
        Assert.Contains("/erp/ajax/cons-entity-save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/cons-entity-delete", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Save entity", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterEntitySaveWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpAjaxConsEntitySave", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/cons-entity-save", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpConsEntitySaveWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpConsEntitySaveWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/ajax/cons-entity-save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_cons_entity_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("HOLD", ErpConsEntitySaveWriteService.NormalizeCode(" hold "));
        Assert.Equal("", ErpConsEntitySaveWriteService.NormalizeCode(""));
        Assert.Equal("AED", ErpConsEntitySaveWriteService.NormalizeCurrency(""));
        Assert.Equal("USD", ErpConsEntitySaveWriteService.NormalizeCurrency("usd"));
        Assert.Equal(100m, ErpConsEntitySaveWriteService.ClampOwnership(150));
        Assert.Equal(0m, ErpConsEntitySaveWriteService.ClampOwnership(-4));
        Assert.Equal(33.333m, ErpConsEntitySaveWriteService.ClampOwnership(33.3334m));
    }

    [Fact]
    public void DryRun_RequiresCodeNameAndRefusesConfirm()
    {
        var ok = new ErpConsEntitySaveDryRun().Evaluate(new ErpConsEntitySaveRequest(0, "SUB1", "Sub one"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missingCode = new ErpConsEntitySaveDryRun().Evaluate(new ErpConsEntitySaveRequest(0, "", "Sub"));
        Assert.Equal("invalid_request", missingCode.ValidationCode);
        var missingName = new ErpConsEntitySaveDryRun().Evaluate(new ErpConsEntitySaveRequest(0, "SUB1", ""));
        Assert.Equal("invalid_request", missingName.ValidationCode);
        var refused = new ErpConsEntitySaveDryRun().Evaluate(new ErpConsEntitySaveRequest(0, "SUB1", "Sub", true));
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
