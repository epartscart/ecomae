using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFyWriteServicePhpParityTests
{
    [Fact]
    public void PeriodStatusesMatchPhpAllowedSet()
    {
        Assert.Equal(new[] { "open", "closed", "locked" }, ErpFyWriteService.AllowedPeriod);
    }

    [Fact]
    public void ServiceSqlMatchesPhpAndDoesNotCreateSchema()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Erp/ErpFyWriteService.cs"));
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_fy_years` SET `status` = 'open', `closed_at` = 0 WHERE `id` = ? AND `status` = 'closed'", src, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_fy_periods` SET `status` = ? WHERE `year_id` = ? AND `period_no` = ?", src, StringComparison.Ordinal);
        Assert.Contains("\"Invalid period status\"", src, StringComparison.Ordinal);
        Assert.Contains("\"Year reopened\"", src, StringComparison.Ordinal);
        Assert.Contains("\"Period status updated\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void NoLiveRouteDryRunStillClaimsUnimplemented()
    {
        var dir = Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Migration");
        foreach (var name in new[] { "ErpFyReopen", "ErpFyPeriodStatus", "ErpFyClose", "ErpWorkflowStatus", "ErpSubscriptionStatus", "ErpInvSetReorderLevel", "ErpHrUpdateDays", "ErpPrjaRecognize", "ErpTransferVoucher", "ErpShortcutDelete", "ErpShortcutDeleteKey", "ErpShortcutReset", "ErpErpFavAdd", "ErpErpFavRemove", "ErpCtrStatus", "ErpFaCreateAsset", "ErpFaRunDepreciation" })
        {
            var src = File.ReadAllText(Path.Combine(dir, name + "DryRun.cs"));
            Assert.DoesNotContain("is not implemented", src, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
