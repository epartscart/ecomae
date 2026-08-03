using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyApiClientParityReporterTests
{
    [Fact]
    public void BuildReportNamesPrefixesAndDatabaseGaps()
    {
        var report = new LegacyApiClientParityReporter().BuildReport();

        Assert.Contains("epc_catalog_", report.SupportedPrefixes);
        Assert.Contains("daily quota", report.EnforcedRules);
        Assert.Contains("price lookup exact-route gate", report.EnforcedRules);
        Assert.Equal("auth-wired-awaiting-staging-keys", report.Status);
        Assert.Contains("catalog exact-route gate", report.EnforcedRules);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("diagnose_smoke_db.sh", StringComparison.Ordinal)
            || gap.Contains("ensure_epc_api_clients_table.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("apply_epc_api_clients_ddl.sh", StringComparison.Ordinal)
            || gap.Contains("align_tenant_registry_to_php_db.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("epc_api_clients", StringComparison.Ordinal));
    }
}
