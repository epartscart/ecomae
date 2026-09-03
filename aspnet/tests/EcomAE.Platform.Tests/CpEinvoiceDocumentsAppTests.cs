using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpEinvoiceDocumentsAppTests
{
    [Fact]
    public void AppMatchesPhpEinvoiceSections()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor"));
        Assert.Contains("@page \"/cp/einvoice-documents-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/einvoice-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("UAE Electronic Invoicing", razor, StringComparison.Ordinal);
        Assert.Contains("5-corner Peppol", razor, StringComparison.Ordinal);
        Assert.Contains("einv_section", razor, StringComparison.Ordinal);
        foreach (var section in new[] { "dashboard", "invoices", "create", "seller", "buyers", "asp", "guide" })
        {
            Assert.Contains($"\"{section}\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("Readiness checklist", razor, StringComparison.Ordinal);
        Assert.Contains("Generate electronic Tax Invoice", razor, StringComparison.Ordinal);
        Assert.Contains("Accredited Service Provider", razor, StringComparison.Ordinal);
        Assert.Contains("0235:9900000098", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("asp_api_key", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("xml_content", razor, StringComparison.Ordinal);
        Assert.Equal("/cp/einvoice-documents-app", EcomAeRoutes.ControlPanelEinvoiceDocumentsApp);
        Assert.Equal("/erp/einvoice-app", EcomAeRoutes.ErpEinvoiceApp);
    }

    [Fact]
    public void WorkspaceDigestOmitsSecretsInSql()
    {
        Assert.Contains("asp_api_key", LegacySurfaceDashboardSql.SelectCpEinvoiceSettings, StringComparison.Ordinal);
        Assert.Contains("<> 'asp_api_key'", LegacySurfaceDashboardSql.SelectCpEinvoiceSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("xml_content", LegacySurfaceDashboardSql.SelectCpEinvoiceDocumentRows, StringComparison.Ordinal);
        Assert.DoesNotContain("seller_json", LegacySurfaceDashboardSql.SelectCpEinvoiceDocumentRows, StringComparison.Ordinal);
        Assert.DoesNotContain("payload_json", LegacySurfaceDashboardSql.SelectCpEinvoiceEvents, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "epc-demo-provision-public.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
