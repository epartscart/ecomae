using System.Text.Json;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpQmOrderCreateTests
{
    [Fact]
    public void DryRunBlocksWritesAndPreservesPhpFields()
    {
        var result = new ErpQmOrderCreateDryRun().Evaluate(
            new ErpQmOrderCreateRequest(2, 17, "po", "B4-QO-1", 88, 1.25m));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal("ok", result.ValidationCode);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.Equal(2, result.CompanyId);
        Assert.Equal(17, result.PlanId);
        Assert.Equal("po", result.RefType);
        Assert.Equal("B4-QO-1", result.RefId);
        Assert.Equal(88, result.ItemId);
        Assert.Equal(1.25m, result.Qty);
    }

    [Fact]
    public void DryRunRejectsMissingCompany()
    {
        var result = new ErpQmOrderCreateDryRun().Evaluate(
            new ErpQmOrderCreateRequest(0, 17, "item", "missing-company", 0, 0));

        Assert.Equal("company_required", result.ValidationCode);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
    }

    [Fact]
    public void ConfirmedDryRunRequestIsRefused()
    {
        var result = new ErpQmOrderCreateDryRun().Evaluate(
            new ErpQmOrderCreateRequest(2, 17, "item", "confirmed", 0, 0, true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
    }

    [Fact]
    public void DryRunPreservesPhpDefaults()
    {
        var result = new ErpQmOrderCreateDryRun().Evaluate(
            new ErpQmOrderCreateRequest(2));

        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(result.ToPayload(new { })));
        var intended = payload.RootElement.GetProperty("intended");

        Assert.Equal("ok", result.ValidationCode);
        Assert.Null(result.RefType);
        Assert.Null(result.RefId);
        Assert.Equal(0, result.PlanId);
        Assert.Equal(0, result.ItemId);
        Assert.Equal(0, result.Qty);
        Assert.Equal("item", intended.GetProperty("ref_type").GetString());
        Assert.Equal(string.Empty, intended.GetProperty("ref_id").GetString());
    }

    [Fact]
    public void DryRunRejectsNegativeNumericValues()
    {
        var result = new ErpQmOrderCreateDryRun().Evaluate(
            new ErpQmOrderCreateRequest(2, -1, "item", "negative", 0, 0));

        Assert.Equal("invalid_request", result.ValidationCode);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
    }
}
