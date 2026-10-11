using System.Data.Common;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCustomer360WorkspaceTests
{
    [Fact]
    public void Compose_MarksUnreadFinanceAsNotSourced()
    {
        var totals = new CpCrmCustomer360(8, 2, 150m, 80m, 1, 1, 40m, 1, 3, 2, 99.5m);
        var workspace = CpCrmCustomer360Composer.Compose(
            8,
            true,
            true,
            "buyer@example.com",
            "buyer@example.com",
            "+971",
            "8",
            "Trade",
            totals,
            new CpCrmCustomer360Credit(false, null, null, null, "", "", null),
            [new CpCrmLeadRow(1, "Buyer Co", "Sam", "buyer@example.com", "+971", "web", "qualified", 0, 20m, "", 1, 2)],
            [new CpCrmOpportunityRow(9, 1, "Pads", "proposal", 150m, 40, 0, 0, 8, "", 1, 2, "Buyer Co")],
            [],
            [new CpCrmQuoteRow(3, 9, 1, 8, "Q-1", "accepted", "AED", 40m, 0, "", 1, 2, "Pads")],
            [new CpCrmTicketRow(4, 8, 0, "Fitment", "open", "high", 0, 1, 2, "buyer@example.com")],
            [new CpCrmOrderRow(11, 1, true, 99.5m, "Paid")],
            true,
            [],
            false,
            null);

        Assert.Equal(8, workspace.Totals.UserId);
        Assert.Equal(2, workspace.Totals.OppCount);
        Assert.Equal(99.5m, workspace.Totals.SalesRevenue);
        Assert.Contains(workspace.Header, field => field.Key == "ytd_sales" && field.Sourced && field.ValueText == "99.50");
        Assert.Contains(workspace.Header, field => field.Key == "outstanding" && !field.Sourced && field.ValueText is null);
        Assert.Contains(workspace.Header, field => field.Key == "credit_limit" && !field.Sourced && field.ValueText is null);
        Assert.Contains(workspace.Header, field => field.Key == "margin" && !field.Sourced);
        var invoices = Assert.Single(workspace.Tabs, tab => tab.Key == "invoices");
        Assert.Equal("not-sourced", invoices.SourceKind);
        var orders = Assert.Single(workspace.Tabs, tab => tab.Key == "orders");
        Assert.Equal("erp", orders.SourceKind);
        Assert.Contains(orders.Lines, line => line.Caption == "Order #11");
        Assert.Equal(17, workspace.Tabs.Count);
    }

    [Fact]
    public void Compose_DoesNotChangeTheJsonCustomer360Shape()
    {
        var totals = new CpCrmCustomer360(4, 1, 10m, 0m, 0, 0, 0m, 0, 0, 0, 0m);
        Assert.Equal(4, totals.UserId);
        Assert.Equal(1, totals.OppCount);
        Assert.Equal(10m, totals.OppOpenValue);
        var ctor = typeof(CpCrmCustomer360).GetConstructors().Single();
        Assert.Equal(11, ctor.GetParameters().Length);
    }

    [Fact]
    public void Href_KeepsUserAndKnownTab()
    {
        Assert.Equal(EcomAeRoutes.ControlPanelCustomer360App, CpCrmCustomer360Composer.Href(0));
        Assert.Equal("/cp/customer-360-app?user_id=8&tab=orders", CpCrmCustomer360Composer.Href(8, "orders"));
        Assert.Equal("/cp/customer-360-app?user_id=8", CpCrmCustomer360Composer.Href(8, "not-a-tab"));
    }

    [Fact]
    public async Task Workspace_OnAnUnconfiguredDatabaseStaysEmpty()
    {
        var desk = new CpCrmDeskService(new UnconfiguredConnections(), new MissingRates());
        var workspace = await desk.Workspace360Async(8);

        Assert.False(workspace.Found);
        Assert.False(workspace.DatabaseConfigured);
        Assert.Equal(8, workspace.Totals.UserId);
        Assert.Empty(workspace.Header);
        Assert.DoesNotContain(workspace.Header, field => field.ValueText == "0.00");
    }

    [Fact]
    public void WorkspacePageOpensSourceRecords()
    {
        var page = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpCustomer360App.razor");
        var board = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpCrmBoardApp.razor");
        Assert.Contains("Workspace360Async", page, StringComparison.Ordinal);
        Assert.Contains("Not sourced", page, StringComparison.Ordinal);
        Assert.Contains("ControlPanelCustomer360App", board, StringComparison.Ordinal);
        Assert.Contains("CpCrmCustomer360Composer.Href", board, StringComparison.Ordinal);
        Assert.DoesNotContain("coming soon", page, StringComparison.OrdinalIgnoreCase);
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

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private sealed class MissingRates : ICpCurrencyLiveRatesService
    {
        public Task<(string IsoCode, string Alpha)> GetMainCurrencyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(("784", "AED"));

        public Task<CpCurrencyLivePreview> PreviewAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CpCurrencyLiveApplyResult> ApplyAsync(IReadOnlyCollection<string>? onlyIsoCodes, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CpCurrencyFxSchedule> GetScheduleAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CpCurrencyFxTickResult> TickAsync(bool force, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
