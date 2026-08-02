using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacySessionParityReporterTests
{
    [Fact]
    public void BuildReportNamesCookieHeaderAndPermissionGaps()
    {
        var report = new LegacySessionParityReporter().BuildReport();

        Assert.Contains("admin_session/admin_u_id cookies", report.SupportedInputs);
        Assert.Contains("session/u_id cookies", report.SupportedInputs);
        Assert.Contains("sessions.type=1", report.SupportedInputs);
        Assert.Contains("admin+customer sessions table checks", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-API-Key header", report.SupportedInputs);
        Assert.Equal("session-db-checks-wired-awaiting-staging", report.Status);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("permissions", StringComparison.OrdinalIgnoreCase));
    }
}
