using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WaveBErpAjaxRegistryDryRunTests
{
    private readonly ErpAjaxWriteCatalog _catalog = new();
    private readonly ErpAjaxWriteRegistryDryRun _registry;

    public WaveBErpAjaxRegistryDryRunTests() => _registry = new ErpAjaxWriteRegistryDryRun(_catalog);

    [Fact]
    public void CatalogCoversAllAjaxErpActionsWithoutCutover()
    {
        var report = _catalog.BuildReport();
        Assert.Equal(321, report.TotalActions);
        Assert.Equal(report.TotalActions, report.DedicatedDryRuns + report.RegistryDryRuns);
        Assert.Equal(100, report.CoveragePct);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.PhpAuthoritative);
        Assert.True(report.DedicatedDryRuns >= 200);
        Assert.True(report.RegistryDryRuns >= 90);
    }

    [Fact]
    public void RegistryValidatesKnownActionAndBlocksWrites()
    {
        var r = _registry.Evaluate(new ErpAjaxWriteRegistryRequest("agenda_save"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.True(r.WritesBlocked);
        Assert.False(r.CutoverAllowed);
        Assert.True(r.PhpAuthoritative);
    }

    [Fact]
    public void RegistryRefusesConfirmWrites()
    {
        var r = _registry.Evaluate(new ErpAjaxWriteRegistryRequest("agenda_save", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void RegistryRejectsUnknownAction()
    {
        var r = _registry.Evaluate(new ErpAjaxWriteRegistryRequest("not_a_real_action_zz"));
        Assert.Equal("dry-run-unknown-action", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void EditLockAcquireValidated()
    {
        var r = new ErpEditLockAcquireDryRun().Evaluate(new("so:1"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void BosWfDecideValidated()
    {
        var r = new ErpBosWfDecideDryRun().Evaluate(new(3, true, "ok"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void BankReconcileValidated()
    {
        var r = new ErpBankReconcileDryRun().Evaluate(new());
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void OnPremisesSetupWizardAndBackupValidated()
    {
        var setup = new OnPremisesSetupWizardDryRun().Evaluate(new("demo"));
        Assert.Equal("dry-run-validated", setup.Status);
        Assert.Equal(0, setup.Writes);
        Assert.False(setup.CutoverAllowed);
        var backup = new OnPremisesBackupDryRun().Evaluate(new("nightly"));
        Assert.Equal("dry-run-validated", backup.Status);
        Assert.Equal(0, backup.Writes);
    }

    [Fact]
    public void PathBoardMentionsAjaxCatalogAndStaysBelow100()
    {
        var report = new AspNetZeroPhpPathReporter().BuildReport();
        Assert.InRange(report.HonestCompletionPct, 90, 99);
        Assert.False(report.CutoverAllowed);
        Assert.Contains(report.Phases, p => p.Id == "4-function-parity" && p.Detail.Contains("ajax_erp", StringComparison.Ordinal));
        Assert.Contains(report.NextBuilds, n => n.Contains("on-premises-aspnet", StringComparison.OrdinalIgnoreCase)
            || n.Contains("setup-wizard", StringComparison.OrdinalIgnoreCase));
    }
}
