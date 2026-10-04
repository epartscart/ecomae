using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpMfgWriteServiceTests
{
    [Fact]
    public void ServiceImplementsContract()
    {
        Assert.True(typeof(EcomAE.Platform.Erp.IErpMfgWriteService).IsAssignableFrom(typeof(EcomAE.Platform.Erp.ErpMfgWriteService)));
    }

    [Fact]
    public void IssueResultShapeMatchesPhpPayload()
    {
        var result = new EcomAE.Platform.Erp.ErpMfgWoIssueResult(true, "ok", 7, 12.5m, [new EcomAE.Platform.Erp.ErpMfgIssuedLine(3, 2m, 6.25m)]);
        Assert.Equal(7, result.WorkOrderId);
        Assert.Equal(12.5m, result.MaterialCost);
        Assert.Single(result.Issued);
        Assert.Equal(3, result.Issued[0].ComponentItemId);
    }

    [Fact]
    public void CompleteResultShapeMatchesPhpPayload()
    {
        var result = new EcomAE.Platform.Erp.ErpMfgWoCompleteResult(true, "ok", 7, 2m, 12.5m, 4m, 1.5m, 18m, 9m);
        Assert.Equal(18m, result.TotalCost);
        Assert.Equal(9m, result.UnitCost);
        Assert.Equal(4m, result.LabourCost);
        Assert.Equal(1.5m, result.OverheadCost);
    }
}
