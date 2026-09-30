using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRfqResponseWriteContractTests
{
    [Fact]
    public void ResponseRequestCarriesPhpScmFields()
    {
        var request = new ErpRfqResponseWriteRequest(
            RfqId: 7,
            RfqLineId: 8,
            SupplierId: 9,
            UnitPrice: 12.3456m,
            LeadTimeDays: 4,
            Notes: "quoted",
            AdminId: 10);

        Assert.Equal(7, request.RfqId);
        Assert.Equal(8, request.RfqLineId);
        Assert.Equal(9, request.SupplierId);
        Assert.Equal(12.3456m, request.UnitPrice);
        Assert.Equal(4, request.LeadTimeDays);
        Assert.Equal("quoted", request.Notes);
        Assert.Equal(10, request.AdminId);
    }
}
