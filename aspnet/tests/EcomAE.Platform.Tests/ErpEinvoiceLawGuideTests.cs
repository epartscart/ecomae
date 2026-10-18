using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEinvoiceLawGuideTests
{
    [Fact]
    public void UpdatedLaw_MovesTheLargeProviderAppointmentAndKeepsTheGoLive()
    {
        Assert.Equal("30 October 2026", ErpEinvoiceLaw.AmendedAspDate);
        Assert.Equal("31 July 2026", ErpEinvoiceLaw.EarlierAspDate);
        Assert.Contains(ErpEinvoiceLaw.Phases, phase => phase.When == "30 October 2026" && phase.Duty.Contains("31 July 2026", StringComparison.Ordinal));
        Assert.Contains(ErpEinvoiceLaw.Phases, phase => phase.When == "1 January 2027");
        Assert.Contains(ErpEinvoiceLaw.Phases, phase => phase.When == "1 July 2027");
        Assert.Contains(ErpEinvoiceLaw.Phases, phase => phase.When == "1 October 2027");
        Assert.Equal(5, ErpEinvoiceLaw.Corners.Count);
        Assert.Equal("Federal Tax Authority", ErpEinvoiceLaw.Corners[4].Title);
        Assert.Contains(ErpEinvoiceLaw.Scope, cell => cell.Code == "B2B" && cell.InScope);
        Assert.Contains(ErpEinvoiceLaw.Scope, cell => cell.Code == "B2C" && !cell.InScope);
        Assert.Equal("0235:1002003004", ErpEinvoiceLaw.Endpoint("100200300400003"));
        Assert.Equal(string.Empty, ErpEinvoiceLaw.Endpoint("123"));
    }

    [Fact]
    public void Page_KeepsTheVisualGuideBesideTheSellerForm()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor"));
        Assert.Contains("Visual guide", page, StringComparison.Ordinal);
        Assert.Contains("Overall summary", page, StringComparison.Ordinal);
        Assert.Contains("Drill down", page, StringComparison.Ordinal);
        Assert.Contains("epc-ei-board", page, StringComparison.Ordinal);
        Assert.Contains("Five corners", page, StringComparison.Ordinal);
        Assert.Contains("ErpEinvoiceLaw.Corners", page, StringComparison.Ordinal);
        Assert.Contains("ErpEinvoiceLaw.Phases", page, StringComparison.Ordinal);
        Assert.Contains("ErpEinvoiceLaw.Scope", page, StringComparison.Ordinal);
        Assert.Contains("Article 65(5)", page, StringComparison.Ordinal);
        Assert.Contains("Cabinet Decision No. 106 of 2025", page, StringComparison.Ordinal);
        Assert.Contains("24 months from 1 January 2027", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/einvoice-save-seller\"", page, StringComparison.Ordinal);
        Assert.Contains("Save seller", page, StringComparison.Ordinal);
        Assert.Contains("name=\"sellerLegalRegNo\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"force\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ei-hero", page, StringComparison.Ordinal);
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

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
