using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCartAddDryRunTests
{
    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontCartAddDryRun.EvaluateProduct(
            new StorefrontCartAddRequest(2, "Bosch", "0986", 1, 12m, ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void Type2AddIsValidated()
    {
        var r = StorefrontCartAddDryRun.EvaluateProduct(
            new StorefrontCartAddRequest(2, "Bosch", "0986424590", 2, 15m, MinOrder: 1, Exist: 10));
        Assert.Equal("ok", r.ValidationCode);
        Assert.True(r.WouldWrite);
        Assert.Contains("INSERT", r.SimulatedSql!, StringComparison.Ordinal);
        Assert.Contains("NOT executed", r.SimulatedSql!, StringComparison.Ordinal);
    }

    [Fact]
    public void Type1Unsupported()
    {
        var r = StorefrontCartAddDryRun.EvaluateProduct(
            new StorefrontCartAddRequest(1, "X", "Y", 1, 1m));
        Assert.Equal("product_type_unsupported", r.ValidationCode);
    }
}
