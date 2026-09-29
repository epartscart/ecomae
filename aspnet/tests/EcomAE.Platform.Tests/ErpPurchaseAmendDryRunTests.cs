using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseAmendDryRunTests
{
    private static ErpPurchaseDigest Purchase(long id, string status = "draft") =>
        new(id, 3, "Supplier Co", 1_700_000_000, $"PI-{id}", 250m, status, 0, []);

    [Fact]
    public void AmendIsDryRunValidatedAsAspNetOwned()
    {
        var result = ErpPurchaseAmendDryRun.EvaluateAgainstPurchases(
            [Purchase(9)],
            new ErpPurchaseAmendRequest(9, "INV-9", "updated"));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.False(result.PhpAuthoritative);
        Assert.True(result.WouldWrite);
        Assert.Contains(result.SimulatedSql, sql => sql.Contains("NOT executed", StringComparison.Ordinal));
    }
}
