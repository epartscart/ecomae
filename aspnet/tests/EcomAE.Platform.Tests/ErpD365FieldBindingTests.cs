using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365FieldBindingTests
{
    [Fact]
    public void LandedCostVoyage_LinksVendorItemAndBranch()
    {
        Assert.True(ErpD365WorkspaceCatalog.TryGet("lc_goods_in_transit", out var page));
        var binding = ErpD365FieldBinding.For(page);
        Assert.Equal(ErpD365FieldBinding.Supplier, binding.PartyKind);
        Assert.Equal(ErpD365FieldBinding.Item, binding.NameKind);
        Assert.Equal(ErpD365FieldBinding.Branch, binding.BranchKind);
        Assert.True(binding.LinksParty);
        Assert.True(binding.LinksName);
        Assert.True(binding.LinksBranch);
    }

    [Fact]
    public void PurchaseAndSalesDocuments_LinkTheRightParty()
    {
        Assert.True(ErpD365WorkspaceCatalog.TryGet("ps_consignment", out var purchase));
        var bought = ErpD365FieldBinding.For(purchase);
        Assert.Equal(ErpD365FieldBinding.Supplier, bought.PartyKind);
        Assert.Equal(ErpD365FieldBinding.Item, bought.NameKind);

        var sale = ErpD365WorkspaceCatalog.All.First(p => p.Area == "sales" && p.Kind == ErpD365Part3Catalog.FormKind.Order);
        Assert.Equal(ErpD365FieldBinding.Customer, ErpD365FieldBinding.For(sale).PartyKind);
        Assert.Equal(ErpD365FieldBinding.Item, ErpD365FieldBinding.For(sale).NameKind);
    }

    [Fact]
    public void Receivables_LinkCustomer_AndLedgerLinksAccount()
    {
        var customer = ErpD365WorkspaceCatalog.All.First(p => p.Area == "ar" && p.Kind == ErpD365Part3Catalog.FormKind.Order);
        Assert.Equal(ErpD365FieldBinding.Customer, ErpD365FieldBinding.For(customer).PartyKind);

        var ledger = ErpD365WorkspaceCatalog.All.First(p =>
            (p.Area == "gl" || p.Area == "finance" || p.Area == "general_ledger")
            && p.Kind == ErpD365Part3Catalog.FormKind.Master);
        var binding = ErpD365FieldBinding.For(ledger);
        Assert.Equal(ErpD365FieldBinding.Account, binding.PartyKind);
        Assert.False(binding.LinksName);
        Assert.False(binding.LinksBranch);
    }

    [Fact]
    public void PayableMatchingPolicy_UsesDocumentedMatchKinds()
    {
        Assert.True(ErpD365WorkspaceCatalog.TryGet("ap_matching_policy", out var page));
        var options = ErpD365FieldBinding.For(page).KindOptions;
        Assert.Contains("Two-way matching", options);
        Assert.Contains("Three-way matching", options);
        Assert.Contains("Invoice totals matching", options);
        Assert.Contains("Charges matching", options);
    }

    [Fact]
    public void WarehouseLoad_UsesOneBranchField()
    {
        Assert.True(ErpD365WorkspaceCatalog.TryGet("wh_all_loads", out var page));
        var binding = ErpD365FieldBinding.For(page);
        Assert.Equal(ErpD365FieldBinding.Branch, binding.PartyKind);
        Assert.Equal(ErpD365FieldBinding.Item, binding.NameKind);
        Assert.False(binding.LinksBranch);
    }

    [Fact]
    public void ProjectMaster_NamesTheProject()
    {
        var project = ErpD365WorkspaceCatalog.All.First(p => p.Area == "projects" && p.Kind == ErpD365Part3Catalog.FormKind.Master);
        Assert.Equal(ErpD365FieldBinding.Project, ErpD365FieldBinding.For(project).NameKind);
    }

    [Fact]
    public void Queue_DoesNotTreatEquipmentAsAVendor()
    {
        var queue = ErpD365WorkspaceCatalog.All.First(p => p.Kind == ErpD365Part3Catalog.FormKind.Queue);
        Assert.False(ErpD365FieldBinding.For(queue).LinksParty);
    }

    [Fact]
    public void CompanyOrDefault_KeepsTheOpenCompany()
    {
        Assert.Equal(1, ErpD365FieldBinding.CompanyOrDefault(0));
        Assert.Equal(4, ErpD365FieldBinding.CompanyOrDefault(4));
    }

    [Fact]
    public void WorkspaceForm_UsesMasterPickersAndCompany()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor");
        Assert.Contains("ErpD365FieldBinding", text, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"party\"", text, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"extra\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"company_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpD365FollowPrefill", text, StringComparison.Ordinal);
        Assert.Contains("NameHeading", text, StringComparison.Ordinal);
        Assert.Contains("PartyHeading", text, StringComparison.Ordinal);
        Assert.Contains("ShowsLinkedMaster", text, StringComparison.Ordinal);
        var writer = Read("aspnet/src/EcomAE.Platform/Erp/ErpD365WorkspaceWriteService.cs");
        Assert.Contains("ErpD365FieldBinding.CompanyOrDefault", writer, StringComparison.Ordinal);
        Assert.DoesNotContain("VALUES (1,", writer, StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
