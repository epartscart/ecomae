using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCheckoutCreateDryRunTests
{
    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontCheckoutCreateDryRun.EvaluateShape(1, new StorefrontCheckoutCreateRequest(1, ConfirmWrites: true), cartCount: 2, cartSource: "database");
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void CustomerRequired()
    {
        var r = StorefrontCheckoutCreateDryRun.EvaluateShape(0, new StorefrontCheckoutCreateRequest(1), 1, "database");
        Assert.Equal("customer_required", r.ValidationCode);
    }

    [Fact]
    public void EmptyCartIsRejectedWhenDatabase()
    {
        var r = StorefrontCheckoutCreateDryRun.EvaluateShape(9, new StorefrontCheckoutCreateRequest(1), 0, "database");
        Assert.Equal("cart_empty", r.ValidationCode);
    }

    [Fact]
    public void ValidCheckoutIsDryRunValidated()
    {
        var r = StorefrontCheckoutCreateDryRun.EvaluateShape(9, new StorefrontCheckoutCreateRequest(1, OfficeId: 3), 2, "database");
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
