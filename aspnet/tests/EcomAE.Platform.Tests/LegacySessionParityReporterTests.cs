using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacySessionParityReporterTests
{
    [Fact]
    public void BuildReportNamesCookieHeaderAndPermissionGaps()
    {
        var report = new LegacySessionParityReporter().BuildReport();

        Assert.Contains("PHPSESSID cookie", report.SupportedInputs);
        Assert.Contains("X-API-Key header", report.SupportedInputs);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("permissions", StringComparison.OrdinalIgnoreCase));
    }
}
