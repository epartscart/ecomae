using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantInstallationControlPlaneTests
{
    [Fact]
    public void Installation_progresses_through_cloud_package_and_enrollment_stages()
    {
        var now = DateTimeOffset.UtcNow;
        var state = TenantInstallationControlPlane.Start("Tenant_A", TenantDeploymentKind.OnPremises, now);

        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.ProvisioningCloudTenant,
            now.AddMinutes(1));
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.PackageReady,
            now.AddMinutes(2),
            enrollmentRequestId: "enroll-123");
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.AwaitingLocalExecution,
            now.AddMinutes(3));
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.EnrollingInstallation,
            now.AddMinutes(4),
            enrollmentId: "installation-123");
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.Synchronizing,
            now.AddMinutes(5));
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.Ready,
            now.AddMinutes(6));

        Assert.Equal("tenant_a", state.TenantKey);
        Assert.Equal(TenantInstallationStage.Ready, state.Stage);
        Assert.Equal(100, state.Percent);
        Assert.Equal("enroll-123", state.EnrollmentRequestId);
        Assert.Equal("installation-123", state.EnrollmentId);
    }

    [Fact]
    public void Invalid_installation_transition_is_rejected()
    {
        var state = TenantInstallationControlPlane.Start("tenant-a", TenantDeploymentKind.Cloud, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            TenantInstallationControlPlane.Advance(
                state,
                TenantInstallationStage.Ready,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Failure_preserves_stage_reason_without_exposing_credentials()
    {
        var state = TenantInstallationControlPlane.Start("tenant-a", TenantDeploymentKind.OnPremises, DateTimeOffset.UtcNow);
        var failed = TenantInstallationControlPlane.Fail(
            state,
            "local-execution-failed",
            "The setup package could not start.",
            DateTimeOffset.UtcNow);

        Assert.Equal(TenantInstallationStage.Failed, failed.Stage);
        Assert.Equal("local-execution-failed", failed.FailureCode);
        Assert.DoesNotContain("password", failed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manifest_contains_cloud_enrollment_link_but_not_database_credentials()
    {
        var manifest = TenantInstallationControlPlane.CreateManifest(
            "tenant-a",
            "https://control.ecomae.com",
            "2026.09.1",
            DateTimeOffset.UtcNow.AddHours(2));

        Assert.Equal("https://control.ecomae.com/api/v1/tenant-installations/enroll", manifest.EnrollmentUrl);
        Assert.Equal("on-premises", manifest.DeploymentKind);
        Assert.StartsWith("enr_", manifest.EnrollmentRequestId, StringComparison.Ordinal);
        Assert.NotEqual("request-123", manifest.EnrollmentRequestId);
        Assert.DoesNotContain("password", manifest.EnrollmentUrl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_", manifest.EnrollmentUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cloud_installation_can_reach_ready_without_local_package_execution()
    {
        var now = DateTimeOffset.UtcNow;
        var state = TenantInstallationControlPlane.Start("tenant-a", TenantDeploymentKind.Cloud, now);

        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.ProvisioningCloudTenant,
            now.AddMinutes(1));
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.Synchronizing,
            now.AddMinutes(2));
        state = TenantInstallationControlPlane.Advance(
            state,
            TenantInstallationStage.Ready,
            now.AddMinutes(3));

        Assert.Equal(TenantInstallationStage.Ready, state.Stage);
        Assert.Equal(100, state.Percent);
    }

    [Fact]
    public void Sync_envelope_requires_tenant_scope_direction_and_sha256_hash()
    {
        var valid = new TenantSyncEnvelope(
            "env-1",
            "tenant-a",
            "installation-1",
            "customer",
            "42",
            1,
            "onpremises-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{\"id\":42}"),
            DateTimeOffset.UtcNow);

        var result = TenantInstallationControlPlane.ValidateSyncEnvelope(valid);

        Assert.True(result.Accepted);
        Assert.Equal("accepted", result.Code);
    }

    [Fact]
    public void Sync_envelope_rejects_unsupported_direction()
    {
        var invalid = new TenantSyncEnvelope(
            "env-1",
            "tenant-a",
            "installation-1",
            "customer",
            "42",
            1,
            "broadcast",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            DateTimeOffset.UtcNow);

        var result = TenantInstallationControlPlane.ValidateSyncEnvelope(invalid);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-direction", result.Code);
    }

    [Fact]
    public void Tenant_key_uses_ascii_site_key_alphabet()
    {
        Assert.Throws<ArgumentException>(() =>
            TenantInstallationControlPlane.Start("тенант", TenantDeploymentKind.Cloud, DateTimeOffset.UtcNow));
        Assert.Equal("tenant_a", TenantInstallationControlPlane.NormalizeTenantKey("Tenant_A"));
    }

    [Fact]
    public void Sync_envelope_rejects_unicode_tenant_keys()
    {
        var invalid = new TenantSyncEnvelope(
            "env-1",
            "тenant-a",
            "installation-1",
            "customer",
            "42",
            1,
            "onpremises-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            DateTimeOffset.UtcNow);

        var result = TenantInstallationControlPlane.ValidateSyncEnvelope(invalid);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-tenant-key", result.Code);
    }
}
