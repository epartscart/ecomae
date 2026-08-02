using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyApiUsageLoggerTests
{
    [Fact]
    public async Task MigrationLoggerTruncatesFieldsToPhpColumnLimits()
    {
        var logger = new MigrationLegacyApiUsageLogger();

        await logger.LogAsync(new LegacyApiUsageLogEntry(
            new string('a', 80),
            new string('s', 80),
            "",
            42,
            new string('p', 300),
            429,
            true,
            new string('m', 300),
            new string('i', 80)));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(40, entry.Action.Length);
        Assert.Equal(20, entry.Section.Length);
        Assert.Equal("api_client", entry.Source);
        Assert.Equal(255, entry.RequestPath.Length);
        Assert.Equal(255, entry.Message.Length);
        Assert.Equal(45, entry.IpAddress.Length);
    }

    [Fact]
    public void UsageLogSqlMatchesPhpUsageLogTable()
    {
        Assert.Contains("epc_umapi_usage_log", LegacyApiUsageLogSql.InsertUsage);
        Assert.Contains("quota_blocked", LegacyApiUsageLogSql.InsertUsage);
        Assert.Contains("is_live", LegacyApiUsageLogSql.InsertUsage);
    }
}
