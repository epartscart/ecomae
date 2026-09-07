using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpReportSchedulerWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresReportName()
    {
        var result = await new ErpReportSchedulerWriteService(new UnusedConnections())
            .CreateAsync(new ErpReportScheduleCreateRequest(ReportType: "sales"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Report name is required.", result.Message);
    }
}
