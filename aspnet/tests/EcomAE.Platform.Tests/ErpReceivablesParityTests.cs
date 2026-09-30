using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Leftover from the ERP empty-tab digest wiring: receivables is PHP
/// <c>epc_erp_receivables</c> customers, not aging buckets.
/// </summary>
public sealed class ErpReceivablesParityTests
{
    [Fact]
    public void ReceivablesAppUsesPhpReceivablesDigestNotAging()
    {
        var text = ReadApp("ErpReceivablesApp.razor");
        Assert.Contains("BuildErpReceivablesDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("OrderReceivableDue", text, StringComparison.Ordinal);
        Assert.Contains("CompleteOrderCount", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildErpAgingDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodCloseCatalogRecordsLiveGuardedLifecycle()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        var page = ReadApp("ErpPeriodCloseApp.razor");

        Assert.Contains("live-period-lifecycle", catalog, StringComparison.Ordinal);
        Assert.Contains("period_id", page, StringComparison.Ordinal);
        Assert.Contains("set-lock", page, StringComparison.Ordinal);
        Assert.Contains("soft-close", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AgendaCatalogRecordsLiveEventPersistence()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        var page = ReadApp("ErpAgendaApp.razor");
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Modules", "ErpModule.cs"));

        Assert.Contains("live-event-lifecycle", catalog, StringComparison.Ordinal);
        Assert.Contains("ErpAgendaEventsSave", module, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.Contains("BuildErpAgendaEventDetailAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ContactsCatalogRecordsLiveMasterDataWrites()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        var page = ReadApp("ErpContactsApp.razor");
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Modules", "ErpModule.cs"));

        Assert.Contains("live-contact-master", catalog, StringComparison.Ordinal);
        Assert.Contains("/erp/customers/master-save", page, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/parties/save", page, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/addresses/save", page, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/party-contacts/save", page, StringComparison.Ordinal);
        Assert.Contains("ErpContactsPartiesSave", module, StringComparison.Ordinal);
        Assert.Contains("ErpContactsAddressesSave", module, StringComparison.Ordinal);
        Assert.Contains("ErpContactsPartyContactsSave", module, StringComparison.Ordinal);
    }

    [Fact]
    public void WithholdingCatalogRecordsLiveTaxLifecycle()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        var page = ReadApp("ErpWithholdingApp.razor");
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet", "src", "EcomAE.Platform", "Modules", "ErpModule.cs"));

        Assert.Contains("live-withholding-lifecycle", catalog, StringComparison.Ordinal);
        Assert.Contains("txn_id", page, StringComparison.Ordinal);
        Assert.Contains("/erp/withholding/codes/save", page, StringComparison.Ordinal);
        Assert.Contains("/erp/withholding/txns/record", page, StringComparison.Ordinal);
        Assert.Contains("/erp/withholding/txns/certificate", page, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/wht-settle", page, StringComparison.Ordinal);
        Assert.Contains("ErpWithholdingCodesSave", module, StringComparison.Ordinal);
        Assert.Contains("ErpWithholdingTxnsRecord", module, StringComparison.Ordinal);
        Assert.Contains("ErpWithholdingTxnsCertificate", module, StringComparison.Ordinal);
        Assert.Contains("ErpAjaxWhtSettle", module, StringComparison.Ordinal);
    }

    [Fact]
    public void OnPremisesAppWiresLicenseDigest()
    {
        var text = ReadApp("ErpOnPremisesApp.razor");
        Assert.Contains("ListOnPremisesLicensesAsync", text, StringComparison.Ordinal);
        Assert.Contains("LicenseKeyPreview", text, StringComparison.Ordinal);
        Assert.Contains("UsersMax", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("receivables", "/erp/receivables-app")]
    [InlineData("on_premises", "/erp/on-premises-app")]
    [InlineData("print_designer", "/erp/print-designer-app")]
    [InlineData("vat", "/erp/vat-app")]
    public void CriticalTabsHaveDedicatedApps(string tab, string expected)
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab(tab, out var href));
        Assert.Equal(expected, href);
    }

    [Fact]
    public void ModuleAppListsTabCatalogWithoutStackLabels()
    {
        var text = ReadApp("ErpModuleApp.razor");
        Assert.Contains("ERP tab catalog", text, StringComparison.Ordinal);
        Assert.Contains("_hasDedicatedApp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
    }

    private static string ReadApp(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
