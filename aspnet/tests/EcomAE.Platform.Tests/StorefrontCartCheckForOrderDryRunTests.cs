using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCartCheckForOrderDryRunTests
{
    private static StorefrontCartLineDigest Line(long id, bool checkedForOrder) =>
        new(id, 10m, 1m, checkedForOrder, 2, "Bosch", "0986", "Pad", "1", "1", 1m, 10m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var result = StorefrontCartCheckForOrderDryRun.EvaluateOwnedLines(
            [Line(10, true)],
            new StorefrontCartCheckForOrderRequest([10], ConfirmWrites: true));

        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.CutoverAllowed);
        Assert.False(result.WouldWrite);
    }

    [Fact]
    public void TogglePlansNextCheckedWithoutWriting()
    {
        var result = StorefrontCartCheckForOrderDryRun.EvaluateOwnedLines(
            [Line(10, true), Line(11, false)],
            new StorefrontCartCheckForOrderRequest([10, 11]));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal("ok", result.ValidationCode);
        Assert.True(result.WouldWrite);
        Assert.Equal(0, result.Writes);
        Assert.Equal(2, result.Planned.Count);
        Assert.Equal(0, result.Planned[0].NextChecked);
        Assert.Equal(1, result.Planned[1].NextChecked);
        Assert.Contains("NOT executed", result.SimulatedSql, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingLineIsInvalid()
    {
        var result = StorefrontCartCheckForOrderDryRun.EvaluateOwnedLines(
            [Line(10, false)],
            new StorefrontCartCheckForOrderRequest([99]));

        Assert.Equal("cart_item_not_found", result.ValidationCode);
        Assert.False(result.WouldWrite);
    }
}
