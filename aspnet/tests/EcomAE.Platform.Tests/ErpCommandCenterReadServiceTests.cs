using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCommandCenterReadServiceTests
{
    [Fact]
    public void FormattingMatchesPhpNumberFormat()
    {
        Assert.Equal("1,234,567.50", ErpCommandCenterReadService.Money(1234567.5m));
        Assert.Equal("0.00", ErpCommandCenterReadService.Money(0m));
        Assert.Equal("12,000", ErpCommandCenterReadService.Count(12000));
        Assert.Equal("Soft close", ErpCommandCenterReadService.PeriodValue("soft_close"));
        Assert.Equal("#dc3545", ErpCommandCenterReadService.PeriodColor("locked"));
        Assert.Equal("#28a745", ErpCommandCenterReadService.PeriodColor("open"));
    }

    [Fact]
    public void RoleWidgetsAndQuickActionsMatchPhp()
    {
        var fin = ErpCommandCenterReadService.RoleWidgets("finance_user");
        Assert.Equal("finance", fin["layout"]);
        Assert.Equal(5, ((IReadOnlyList<IReadOnlyDictionary<string, object?>>)fin["widgets"]!).Count);
        Assert.Equal("executive", ErpCommandCenterReadService.RoleWidgets("unknown")["layout"]);

        Assert.Equal(8, ErpCommandCenterReadService.QuickActions(string.Empty).Count);
        var wh = ErpCommandCenterReadService.QuickActions("warehouse_user");
        Assert.Single(wh);
        Assert.Equal("inv_movement", wh[0]["id"]);
        Assert.Empty(ErpCommandCenterReadService.QuickActions("nobody"));
    }

    [Fact]
    public void ServiceNeverCreatesSchema()
    {
        var root = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(root);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform/Erp/ErpCommandCenterReadService.cs"));
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", src, StringComparison.OrdinalIgnoreCase);
    }
}
