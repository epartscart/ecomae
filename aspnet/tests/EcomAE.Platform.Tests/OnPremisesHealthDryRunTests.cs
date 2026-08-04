using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class OnPremisesHealthDryRunTests
{
    private readonly OnPremisesHealthDryRun _dryRun = new();

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = _dryRun.Evaluate(new OnPremisesHealthRequest("ABCD-EFGH-1234", "ok", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void ShortLicenseIsInvalid()
    {
        var r = _dryRun.Evaluate(new OnPremisesHealthRequest("abc"));
        Assert.Equal("invalid_payload", r.ValidationCode);
    }

    [Fact]
    public void ValidPayloadIsDryRunValidated()
    {
        var r = _dryRun.Evaluate(new OnPremisesHealthRequest("ABCD-EFGH-1234-5678", "ok", DiskFreeGb: 120));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.True(r.PhpAuthoritative);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
