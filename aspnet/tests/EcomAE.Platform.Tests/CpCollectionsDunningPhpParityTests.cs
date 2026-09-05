using System.Reflection;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/collections-dunning-app live queue / profile / process writes.
/// </summary>
public sealed class CpCollectionsDunningPhpParityTests
{
    [Fact]
    public void CpCollectionsDunningApp_EmitsQueueWriteForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("/cp/collections-dunning/write", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"update_status\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"record_payment\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"create_profile\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"add_invoice\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"process\"", text, StringComparison.Ordinal);
        Assert.Contains("/erp/collections/cases/status", text, StringComparison.Ordinal);
        Assert.Contains("AmountDue", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterDunningWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("CpCollectionsDunningWrite", routes, StringComparison.Ordinal);
        Assert.Contains("/cp/collections-dunning/write", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCollectionsDunningWriteService", program, StringComparison.Ordinal);
        Assert.Contains("ICpCollectionsDunningWriteDryRun", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCollectionsDunningWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new CpCollectionsDunningWriteDryRun();
        var blocked = dry.Evaluate(new CpCollectionsDunningWriteRequest("update_status", false));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var refused = dry.Evaluate(new CpCollectionsDunningWriteRequest("update_status", true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void DefaultSteps_MatchPhpSevenStepSequence()
    {
        Assert.Equal(7, CpCollectionsDunningWriteService.DefaultSteps.Count);
        Assert.Equal(1, CpCollectionsDunningWriteService.DefaultSteps[0].Day);
        Assert.Equal("email", CpCollectionsDunningWriteService.DefaultSteps[0].Action);
        Assert.Equal("Friendly Payment Reminder", CpCollectionsDunningWriteService.DefaultSteps[0].Subject);
        Assert.Equal(60, CpCollectionsDunningWriteService.DefaultSteps[^1].Day);
        Assert.Equal("letter", CpCollectionsDunningWriteService.DefaultSteps[^1].Action);
    }

    [Fact]
    public void DaysOverdue_TruncatesLikePhpIntegerDivision()
    {
        var due = new DateTime(2026, 8, 1, 0, 0, 0);
        var now = new DateTime(2026, 9, 5, 12, 0, 0);
        Assert.Equal(35, CpCollectionsDunningWriteService.DaysOverdue("2026-08-01", now));
        Assert.Equal(0, CpCollectionsDunningWriteService.DaysOverdue("2026-09-05", now));
        Assert.Equal(0, CpCollectionsDunningWriteService.DaysOverdue("2026-10-01", now));
        Assert.True(CpCollectionsDunningWriteService.ShouldAdvance(0, 1, CpCollectionsDunningWriteService.DefaultSteps));
        Assert.False(CpCollectionsDunningWriteService.ShouldAdvance(0, 0, CpCollectionsDunningWriteService.DefaultSteps));
        Assert.True(CpCollectionsDunningWriteService.ShouldAdvance(4, 30, CpCollectionsDunningWriteService.DefaultSteps));
        Assert.False(CpCollectionsDunningWriteService.ShouldAdvance(7, 90, CpCollectionsDunningWriteService.DefaultSteps));
        Assert.Equal("letter", CpCollectionsDunningWriteService.NormalizeLogAction("LETTER"));
        Assert.Equal("note", CpCollectionsDunningWriteService.NormalizeLogAction("fax"));
        Assert.Equal(new DateTime(2026, 8, 1).Date, due.Date);
    }

    [Fact]
    public void Catalog_KeepsDigestWiredStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var shell = catalog.First(item => item.AspNetRouteOrCapability.Contains("/cp/collections-dunning-app", StringComparison.Ordinal));
        Assert.Equal("digest-wired-awaiting-dual-sample", shell.Status);
        Assert.Contains("/cp/collections-dunning/write", shell.Notes, StringComparison.Ordinal);
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
