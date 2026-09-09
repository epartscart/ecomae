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

    [Fact]
    public void DetailSqlLoadsNotesExcerptAndOmitsSecrets()
    {
        var sql = LegacySurfaceDashboardSql.SelectOnPremisesLicenseDetail;
        Assert.Contains("epc_onprem_licenses", sql, StringComparison.Ordinal);
        Assert.Contains("notes_excerpt", sql, StringComparison.Ordinal);
        Assert.Contains("LEFT(IFNULL(`notes`, ''), 280)", sql, StringComparison.Ordinal);
        Assert.Contains("`id` = @id", sql, StringComparison.Ordinal);
        Assert.Contains("issued_at", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("license_key", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`fingerprint`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`ip`", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("modules_json", sql, StringComparison.Ordinal);

        var siblings = LegacySurfaceDashboardSql.SelectOnPremisesLicenseStatusSiblings;
        Assert.Contains("epc_onprem_licenses", siblings, StringComparison.Ordinal);
        Assert.Contains("@status", siblings, StringComparison.Ordinal);
        Assert.Contains("`id` <> @id", siblings, StringComparison.Ordinal);
        Assert.DoesNotContain("`notes`", siblings, StringComparison.Ordinal);
        Assert.DoesNotContain("`fingerprint`", siblings, StringComparison.Ordinal);
        Assert.DoesNotContain("`ip`", siblings, StringComparison.Ordinal);
        Assert.DoesNotContain("license_key", siblings, StringComparison.Ordinal);
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
