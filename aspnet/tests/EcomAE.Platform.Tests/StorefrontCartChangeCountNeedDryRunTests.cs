using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCartChangeCountNeedDryRunTests
{
    private static StorefrontCartLineDigest Line(
        long id = 10,
        decimal countNeed = 2,
        int productType = 2,
        decimal minOrder = 1,
        decimal t2Exist = 10) =>
        new(id, 12.5m, countNeed, true, productType, "Bosch", "0986424590", "Pad", "1", "1", minOrder, t2Exist);

    [Fact]
    public void ConfirmWritesIsAlwaysRefused()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line()],
            new StorefrontCartChangeCountNeedRequest(10, 3, ConfirmWrites: true));

        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.CutoverAllowed);
        Assert.False(result.WouldWrite);
        Assert.Equal("confirm_writes_refused", result.ValidationCode);
    }

    [Fact]
    public void ValidType2IncreaseIsDryRunValidatedWithoutWrite()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line(countNeed: 2, minOrder: 1, t2Exist: 10)],
            new StorefrontCartChangeCountNeedRequest(10, 3));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal("ok", result.ValidationCode);
        Assert.True(result.WouldWrite);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.CutoverAllowed);
        Assert.Contains("NOT executed", result.SimulatedSql, StringComparison.Ordinal);
        Assert.Contains("ajax_change_count_need.php", result.PhpAjax, StringComparison.Ordinal);
    }

    [Fact]
    public void NotEnoughStockDoesNotClaimWrite()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line(countNeed: 2, t2Exist: 2)],
            new StorefrontCartChangeCountNeedRequest(10, 5));

        Assert.Equal("not_enough", result.ValidationCode);
        Assert.False(result.WouldWrite);
        Assert.Equal(0, result.Writes);
    }

    [Fact]
    public void ProductType1IsNotInThisSlice()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line(productType: 1)],
            new StorefrontCartChangeCountNeedRequest(10, 3));

        Assert.Equal("dry-run-needs-sample", result.Status);
        Assert.Equal("product_type_unsupported", result.ValidationCode);
        Assert.False(result.WouldWrite);
    }

    [Fact]
    public void MissingLineIsInvalid()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line(id: 99)],
            new StorefrontCartChangeCountNeedRequest(10, 3));

        Assert.Equal("cart_item_not_found", result.ValidationCode);
        Assert.Equal("dry-run-invalid", result.Status);
    }

    [Fact]
    public void PayloadKeepsCutoverFalse()
    {
        var result = StorefrontCartChangeCountNeedDryRun.EvaluateOwnedLines(
            [Line()],
            new StorefrontCartChangeCountNeedRequest(10, 3));
        var json = System.Text.Json.JsonSerializer.Serialize(result.ToPayload(new { kind = "Customer" }));
        Assert.Contains("\"cutoverAllowed\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"writes\":0", json, StringComparison.Ordinal);
        Assert.Contains("\"writesBlocked\":true", json, StringComparison.Ordinal);
    }
}
