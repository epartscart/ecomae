using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCartDeleteDryRunTests
{
    private static StorefrontCartLineDigest Line(long id, int productType = 2) =>
        new(id, 10m, 1m, true, productType, "Bosch", "0986", "Pad", "1", "1", 1m, 10m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var result = StorefrontCartDeleteDryRun.EvaluateOwnedLines(
            [Line(10)],
            new StorefrontCartDeleteRequest([10], ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.False(result.CutoverAllowed);
    }

    [Fact]
    public void Type2DeleteIsDryRunValidated()
    {
        var result = StorefrontCartDeleteDryRun.EvaluateOwnedLines(
            [Line(10), Line(11)],
            new StorefrontCartDeleteRequest([10, 11]));
        Assert.Equal("dry-run-validated", result.Status);
        Assert.True(result.WouldWrite);
        Assert.Equal(0, result.Writes);
        Assert.Contains("DELETE", result.SimulatedSql, StringComparison.Ordinal);
        Assert.Contains("NOT executed", result.SimulatedSql, StringComparison.Ordinal);
    }

    [Fact]
    public void Type1IsNotInThisSlice()
    {
        var result = StorefrontCartDeleteDryRun.EvaluateOwnedLines(
            [Line(10, productType: 1)],
            new StorefrontCartDeleteRequest([10]));
        Assert.Equal("product_type_unsupported", result.ValidationCode);
        Assert.False(result.WouldWrite);
    }

    [Fact]
    public void AlienCartIsRejected()
    {
        var result = StorefrontCartDeleteDryRun.EvaluateOwnedLines(
            [Line(10)],
            new StorefrontCartDeleteRequest([99]));
        Assert.Equal("alien_cart", result.ValidationCode);
    }
}
