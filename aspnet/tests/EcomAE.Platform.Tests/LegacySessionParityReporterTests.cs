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
        Assert.Equal("nested-module-acl-wired-awaiting-staging", report.Status);
        Assert.Contains(report.SupportedInputs, item => item.Contains("modules_access", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.SupportedInputs, item => item.Contains("parent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("issue_smoke_credentials.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("ECOMAE_CUSTOMER_COOKIE_HEADER", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("login", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("OAuth", StringComparison.OrdinalIgnoreCase)
            || gap.Contains("OIDC", StringComparison.OrdinalIgnoreCase));
    }
}
