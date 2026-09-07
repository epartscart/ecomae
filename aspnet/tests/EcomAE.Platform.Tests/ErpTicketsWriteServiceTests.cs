using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTicketsWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresSubject()
    {
        var result = await new ErpTicketsWriteService(new UnusedConnections())
            .CreateAsync(new ErpTicketsCreateRequest(ClientName: "Al Noor"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Subject is required.", result.Message);
    }

    [Fact]
    public async Task ReplyRequiresTicketAndMessage()
    {
        var missingTicket = await new ErpTicketsWriteService(new UnusedConnections())
            .ReplyAsync(new ErpTicketsReplyRequest(Message: "Noted"));
        Assert.False(missingTicket.Succeeded);
        Assert.Equal("invalid", missingTicket.Code);

        var missingMessage = await new ErpTicketsWriteService(new UnusedConnections())
            .ReplyAsync(new ErpTicketsReplyRequest(TicketId: 9));
        Assert.False(missingMessage.Succeeded);
        Assert.Equal("invalid", missingMessage.Code);
        Assert.Equal("Reply message is required.", missingMessage.Message);
    }
}
