using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontGarageDeleteDryRunTests
{
    private static StorefrontGarageVehicleDigest Car(long id) =>
        new(id, "My car", "Toyota", "Camry", "2019", "VIN", 0);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontGarageDeleteDryRun.EvaluateAgainstVehicles(
            5, [Car(9)], new StorefrontGarageDeleteRequest(9, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void OtherUsersCarIsInvalid()
    {
        var r = StorefrontGarageDeleteDryRun.EvaluateAgainstVehicles(
            5, [Car(99)], new StorefrontGarageDeleteRequest(9));
        Assert.Equal("garage_not_owned", r.ValidationCode);
    }

    [Fact]
    public void DeleteIsDryRunValidated()
    {
        var r = StorefrontGarageDeleteDryRun.EvaluateAgainstVehicles(
            5, [Car(9)], new StorefrontGarageDeleteRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("DELETE", StringComparison.Ordinal));
    }
}
