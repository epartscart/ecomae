using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class OnPremisesLicenseActivateDryRunTests
{
    private readonly OnPremisesLicenseActivateDryRun _dryRun = new();

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = _dryRun.Evaluate(new OnPremisesLicenseActivateRequest(
            "LIC-2026-ABCD-EFGH", "fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void InvalidKeyFormatIsRejected()
    {
        var r = _dryRun.Evaluate(new OnPremisesLicenseActivateRequest(
            "DEMO-KEY-XXXX", "fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.Equal("invalid_key_format", r.ValidationCode);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void MissingFingerprintIsRejected()
    {
        var r = _dryRun.Evaluate(new OnPremisesLicenseActivateRequest("LIC-2026-ABCD-EFGH", ""));
        Assert.Equal("missing_fingerprint", r.ValidationCode);
    }

    [Fact]
    public void ValidPayloadIsDryRunValidated()
    {
        var r = _dryRun.Evaluate(new OnPremisesLicenseActivateRequest(
            "LIC-2026-ABCD-EFGH",
            "fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            Hostname: "onprem-demo",
            Ip: "10.0.0.8"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.True(r.PhpAuthoritative);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
