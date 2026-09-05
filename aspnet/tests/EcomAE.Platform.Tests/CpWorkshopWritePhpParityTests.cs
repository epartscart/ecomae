using System.Reflection;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards PHP <c>epc_ws_job_create</c> / <c>epc_ws_job_add_line</c> / appointment twins.
/// </summary>
public sealed class CpWorkshopWritePhpParityTests
{
    [Fact]
    public void FormatJobNo_MatchesPhpSprintf()
    {
        Assert.Equal("WS-260905-001", CpWorkshopWriteService.FormatJobNo("260905", 1));
        Assert.Equal("WS-260905-012", CpWorkshopWriteService.FormatJobNo("260905", 12));
        Assert.Equal("AP-260905-003", CpWorkshopWriteService.FormatAppointmentRef("260905", 3));
    }

    [Fact]
    public void NormalizeStatus_MatchesPhpDefaults()
    {
        Assert.Equal("checkin", CpWorkshopWriteService.NormalizeJobStatus(null));
        Assert.Equal("checkin", CpWorkshopWriteService.NormalizeJobStatus("nope"));
        Assert.Equal("in_progress", CpWorkshopWriteService.NormalizeJobStatus("IN_PROGRESS"));
        Assert.Equal("scheduled", CpWorkshopWriteService.NormalizeAppointmentStatus(""));
        Assert.Equal("converted", CpWorkshopWriteService.NormalizeAppointmentStatus("converted"));
        Assert.Equal("labour", CpWorkshopWriteService.NormalizeLineType("labour"));
        Assert.Equal("part", CpWorkshopWriteService.NormalizeLineType("PART"));
        Assert.Equal("part", CpWorkshopWriteService.NormalizeLineType(null));
    }

    [Fact]
    public void RecalcTotals_MatchesPhpChargeableMath()
    {
        var totals = CpWorkshopWriteService.RecalcTotals(
        [
            ("labour", 1.5m, 150m, 5m, 1),
            ("part", 2m, 10m, 5m, 1),
            ("part", 1m, 99m, 5m, 0),
        ]);
        Assert.Equal(20.00m, totals.PartsTotal);
        Assert.Equal(225.00m, totals.LabourTotal);
        Assert.Equal(12.25m, totals.TaxTotal);
        Assert.Equal(257.25m, totals.GrandTotal);
    }

    [Fact]
    public void WorkshopApp_HasNativeCreateFormsWithoutBlazorClicks()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWorkshopApp.razor"));
        Assert.Contains("value=\"create_job\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"add_line\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"create_appointment\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"convert_appointment\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"labour_desc\"", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("create-job, lines, and appointments stay", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogAndMatrix_MarkWorkshopWritesLive()
    {
        var notes = SurfacePayloadContractCatalog.Functions
            .First(f => f.AspNetRouteOrCapability == "/cp/workshop-app").Notes;
        Assert.Contains("create_job", notes, StringComparison.Ordinal);
        Assert.Contains("add_line", notes, StringComparison.Ordinal);
        Assert.Contains("create_appointment", notes, StringComparison.Ordinal);
        Assert.Contains("seed stays PHP", notes, StringComparison.Ordinal);
        var row = PhpVsAspNetRemovalMatrix.Rows.First(r => r.Id == "cp-workshop");
        Assert.Equal("aspnet", row.WritesOwner);
        Assert.Contains("create_job", row.Note, StringComparison.Ordinal);
        Assert.Contains("Seed stays PHP", row.Note, StringComparison.Ordinal);
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
