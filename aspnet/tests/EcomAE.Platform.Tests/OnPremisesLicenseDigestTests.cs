using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class OnPremisesLicenseDigestTests
{
    [Fact]
    public void SelectSqlOmitsSecretsAndNotes()
    {
        var sql = LegacySurfaceDashboardSql.SelectOnPremisesLicenses;
        Assert.Contains("epc_onprem_licenses", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`notes`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`fingerprint`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`ip`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("modules_json", sql, StringComparison.Ordinal);
        Assert.Contains("license_key", sql, StringComparison.Ordinal);
        Assert.Contains("customer_name", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LIC-2026-ABCD-EFGH", "LIC-…EFGH")]
    [InlineData("short", "short")]
    [InlineData("", "")]
    public void LicenseKeyIsMasked(string raw, string expected)
    {
        Assert.Equal(expected, SurfaceDashboardSummaryReporter.MaskOnPremisesLicenseKey(raw));
    }
}
