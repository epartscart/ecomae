using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontGarageNotepadAddDryRunTests
{
    private static StorefrontGarageVehicleDigest Vehicle(long id) =>
        new(id, "My car", "Toyota", "Camry", "2018", "VIN123", 1);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontGarageNotepadAddDryRun.EvaluateProduct(
            5, [Vehicle(1)], new StorefrontGarageNotepadAddRequest(1, "Bosch", "0986", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void EmptyArticleIsInvalid()
    {
        var r = StorefrontGarageNotepadAddDryRun.EvaluateProduct(
            5, [], new StorefrontGarageNotepadAddRequest(0, "Bosch", "  "));
        Assert.Equal("article_required", r.ValidationCode);
    }

    [Fact]
    public void GarageNotOwnedIsInvalid()
    {
        var r = StorefrontGarageNotepadAddDryRun.EvaluateProduct(
            5, [Vehicle(1)], new StorefrontGarageNotepadAddRequest(99, "Bosch", "0986"));
        Assert.Equal("garage_not_owned", r.ValidationCode);
    }

    [Fact]
    public void AddIsDryRunValidated()
    {
        var r = StorefrontGarageNotepadAddDryRun.EvaluateProduct(
            5, [Vehicle(3)], new StorefrontGarageNotepadAddRequest(3, "Bosch", "0986424590", "Pad", 4, 12.5m));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("shop_docpart_garage_notepad", StringComparison.Ordinal));
    }
}
