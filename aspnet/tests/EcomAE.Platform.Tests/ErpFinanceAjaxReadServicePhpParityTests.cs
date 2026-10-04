using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinanceAjaxReadServicePhpParityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 13, 45, 10, TimeSpan.Zero);

    [Fact]
    public void DocTypeDefaultsToReceivables()
    {
        Assert.True(ErpFinanceAjaxReadService.IsPayables("ap"));
        Assert.False(ErpFinanceAjaxReadService.IsPayables("ar"));
        Assert.False(ErpFinanceAjaxReadService.IsPayables(null));
        Assert.False(ErpFinanceAjaxReadService.IsPayables("AP"));
    }

    [Fact]
    public void DateWindowMatchesPhpDefaults()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), ErpFinanceAjaxReadService.FromUnix(null, Now));
        Assert.Equal(Now.ToUnixTimeSeconds(), ErpFinanceAjaxReadService.ToUnix("", Now));
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), ErpFinanceAjaxReadService.FromUnix("2026-09-05", Now));
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), ErpFinanceAjaxReadService.ToUnix("2026-09-05", Now));
    }

    [Fact]
    public void OrderIdFilterMatchesCtypeDigit()
    {
        Assert.Equal(123, ErpFinanceAjaxReadService.OrderIdFilter("123"));
        Assert.Equal(0, ErpFinanceAjaxReadService.OrderIdFilter("INV-123"));
        Assert.Equal(0, ErpFinanceAjaxReadService.OrderIdFilter("-5"));
    }

    [Fact]
    public void LimitsAndSqlMirrorPhp()
    {
        Assert.Equal(50, ErpFinanceAjaxReadService.PeriodLogLimit);
        Assert.Equal(200, ErpFinanceAjaxReadService.InvoiceListLimit);
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "aspnet/src/EcomAE.Platform/Erp/ErpFinanceAjaxReadService.cs"));
        Assert.Contains("HAVING outstanding > 0.005", src, StringComparison.Ordinal);
        Assert.Contains("`doc_category` IN ('tax_invoice','commercial_invoice')", src, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN `users` u ON u.`user_id` = d.`user_id`", src, StringComparison.Ordinal);
        Assert.Contains("ORDER BY d.`issue_date` DESC, d.`id` DESC", src, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
