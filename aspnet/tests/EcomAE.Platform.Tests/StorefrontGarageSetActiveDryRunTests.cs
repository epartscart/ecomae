using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontGarageSetActiveDryRunTests
{
    private static StorefrontGarageVehicleDigest Car(long id, int active = 0) =>
        new(id, "My car", "Toyota", "Camry", "2019", "VIN", active);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontGarageSetActiveDryRun.EvaluateAgainstVehicles(
            5, [Car(9)], new StorefrontGarageSetActiveRequest(9, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void OtherUsersCarIsInvalid()
    {
        var r = StorefrontGarageSetActiveDryRun.EvaluateAgainstVehicles(
            5, [Car(99)], new StorefrontGarageSetActiveRequest(9));
        Assert.Equal("garage_not_owned", r.ValidationCode);
    }

    [Fact]
    public void SetActiveIsDryRunValidated()
    {
        var r = StorefrontGarageSetActiveDryRun.EvaluateAgainstVehicles(
            5, [Car(9), Car(10, 1)], new StorefrontGarageSetActiveRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldActivate);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("active`=1", StringComparison.Ordinal));
    }

    [Fact]
    public void AlreadyActiveTogglesOff()
    {
        var r = StorefrontGarageSetActiveDryRun.EvaluateAgainstVehicles(
            5, [Car(9, 1)], new StorefrontGarageSetActiveRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.WouldActivate);
        Assert.Contains(r.SimulatedSql, s => s.Contains("toggle-off", StringComparison.Ordinal));
    }
}
