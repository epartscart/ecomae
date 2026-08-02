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
        Assert.Contains(report.SupportedInputs, item => item.Contains("for_backend", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("backend group claims", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-API-Key header", report.SupportedInputs);
        Assert.Equal("module-acl-probe-wired-awaiting-staging", report.Status);
        Assert.Contains("modules_access/open modules", report.SupportedInputs);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("modules_access", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("inheritance", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("login", StringComparison.OrdinalIgnoreCase));
    }
}
