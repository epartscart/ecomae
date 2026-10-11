using EcomAE.Platform.Bos;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosRoleCentreTests
{
    private static readonly OperatingCompanyRef Dubai = new(1, "MAIN", "Dubai Main");

    [Fact]
    public void RoleCentreHref_UsesDedicatedApps()
    {
        Assert.Equal(EcomAeRoutes.BosRoleCentresApp, OperatingNavigationContext.RoleCentreHref(null, null));
        Assert.Equal("/bos/role-sales-app?company=2", OperatingNavigationContext.RoleCentreHref("Sales", 2));
        Assert.Equal(EcomAeRoutes.BosRoleCeoApp, OperatingNavigationContext.RoleCentreHref("ceo", null));
    }

    [Fact]
    public void Project_CeoUsesSourcedRowsAndDoesNotInventOmittedMeasures()
    {
        var load = Load(
            Doc("erp.sales_order", "Sales", 1, "SO-1", "Draft sale", 100m),
            Doc("crm.opportunity", "Relationship", 2, "Opp", "proposal", 40m, 50));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [Dubai], null, "ceo");
        var attention = BosCommandCentreComposer.Project(context, load, true, true);

        var view = BosRoleCentreComposer.Project("ceo", context, load, attention);

        Assert.Equal("ceo", view.Role);
        Assert.Contains(view.Kpis, kpi => kpi.Key == "erp.sales_order" && kpi.Sourced && kpi.ValueText != null && kpi.ValueText.Contains("100.00", StringComparison.Ordinal));
        Assert.Contains(view.Kpis, kpi => kpi.Key == "weighted_pipeline" && kpi.Sourced && kpi.ValueText == "20.00");
        Assert.DoesNotContain(view.Kpis, kpi => kpi.Sourced && string.Equals(kpi.Label, "EBITDA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(view.NotSourced, label => label.Equals("EBITDA", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Kpis.Where(kpi => kpi.Sourced).Select(kpi => kpi.ValueText), value => value == "0.00" && !attention.DisplayedNumbers.Contains(value!));
    }

    [Fact]
    public void Project_CfoDoesNotShowSalesFigures()
    {
        var load = Load(Doc("erp.sales_order", "Sales", 1, "SO-1", "Draft sale", 99m));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [Dubai], null, "cfo");
        var attention = BosCommandCentreComposer.Project(context, load, true, true);

        var view = BosRoleCentreComposer.Project("cfo", context, load, attention);

        Assert.DoesNotContain(view.Kpis, kpi => kpi.Key == "erp.sales_order");
        Assert.Contains(view.NotSourced, label => label.Equals("treasury forecast", StringComparison.Ordinal));
        Assert.Empty(view.Attention.DisplayedNumbers);
    }

    [Fact]
    public void Project_PurchasingIncludesStockExceptionsFromTheSameErpRead()
    {
        var load = Load(
            Doc("erp.purchase_order", "Procurement", 3, "PO-1", "Draft buy", 12m),
            Doc("erp.stock", "Inventory", 4, "SKU-1", "Filter", null));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [Dubai], null, "purchasing");
        var attention = BosCommandCentreComposer.Project(context, load, true, false);

        var view = BosRoleCentreComposer.Project("purchasing", context, load, attention);

        Assert.DoesNotContain(attention.TenantRows, row => row.Source == "erp.stock");
        Assert.Contains(view.Kpis, kpi => kpi.Key == "erp.stock" && kpi.Sourced && kpi.ValueText == "1");
        Assert.Contains(view.NotSourced, label => label.Equals("supplier score", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_UnreadSourceIsNotRenderedAsZero()
    {
        var load = new BosCommandCentreLoad(true, DateTimeOffset.UnixEpoch,
        [
            new BosSourceSlice("erp.gl_journal", "Finance", false, false, []),
        ]);
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [], null, "cfo");
        var attention = BosCommandCentreComposer.Project(context, load, true, true);

        var view = BosRoleCentreComposer.Project("cfo", context, load, attention);

        var journal = Assert.Single(view.Kpis);
        Assert.False(journal.Sourced);
        Assert.Null(journal.ValueText);
        Assert.DoesNotContain("0", journal.SourceLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void RoleCentreRoutesAreAdminSurfaces()
    {
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin(EcomAeRoutes.BosRoleCentresApp));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin(EcomAeRoutes.BosRoleCeoApp));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin(EcomAeRoutes.BosRoleOperationsApp));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin(EcomAeRoutes.ControlPanelCustomer360App));
    }

    [Fact]
    public void RoleCentrePageConsumesTheAttentionReader()
    {
        var page = Read("aspnet/src/EcomAE.Platform/Components/Pages/BosRoleCentreApp.razor");
        Assert.Contains("IBosCommandCentreReadService", page, StringComparison.Ordinal);
        Assert.Contains("BosRoleCentreComposer.Project", page, StringComparison.Ordinal);
        Assert.Contains("Not sourced", page, StringComparison.Ordinal);
        Assert.DoesNotContain("coming soon", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/bos/role-ceo-app", page, StringComparison.Ordinal);
    }

    private static BosCommandCentreLoad Load(params BosSourceDocument[] documents)
    {
        var slices = documents
            .GroupBy(document => document.Source)
            .Select(group => new BosSourceSlice(group.Key, group.First().Category, true, true, group.ToList()))
            .ToList();
        return new BosCommandCentreLoad(true, DateTimeOffset.UnixEpoch, slices);
    }

    private static BosSourceDocument Doc(
        string source,
        string category,
        long id,
        string reference,
        string title,
        decimal? amount,
        int? probability = null)
        => new(source, category, id, reference, title, "open", amount, probability, null);

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
