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
        Assert.Equal(321, report.DedicatedDryRuns);
        Assert.Equal(0, report.RegistryDryRuns);
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
    public void BosCatalogCoversAjaxWithoutCutover()
    {
        var report = new BosAjaxWriteCatalog().BuildReport();
        Assert.Equal(231, report.TotalActions);
        Assert.Equal(100, report.CoveragePct);
        Assert.False(report.CutoverAllowed);
        Assert.True(report.PhpAuthoritative);
        Assert.True(report.DedicatedDryRuns > 0);
        Assert.True(report.RegistryDryRuns > 0);
    }

    [Fact]
    public void CpModuleCatalogCoversAjaxWithoutCutover()
    {
        var catalog = new CpModuleAjaxWriteCatalog();
        var report = catalog.BuildReport();
        Assert.Equal(256, report.TotalActions);
        Assert.Equal(100, report.CoveragePct);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.PhpAuthoritative);
        Assert.Equal(174, report.DedicatedDryRuns);
        Assert.Equal(82, report.RegistryDryRuns);
        Assert.True(catalog.TryGet("procurement", "create_supplier", out var entry));
        Assert.Equal("dedicated", entry.Coverage);
        Assert.True(catalog.TryGet("crm", "crm_save_lead", out var crm));
        Assert.Equal("dedicated", crm.Coverage);
        Assert.True(catalog.TryGet("classic_form", "shop_catalogue_product", out var form));
        Assert.Equal("dedicated", form.Coverage);
    }

    [Fact]
    public void CpModuleRegistryAndDedicatedBlockWrites()
    {
        var catalog = new CpModuleAjaxWriteCatalog();
        var registry = new CpModuleAjaxWriteRegistryDryRun(catalog);
        var dedicated = new CpModuleAjaxWriteDedicatedDryRun(catalog);

        var ok = registry.Evaluate(new CpModuleAjaxWriteRegistryRequest("procurement", "create_supplier"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.CutoverAllowed);

        var refused = dedicated.Evaluate(new CpModuleAjaxWriteDedicatedRequest("crm", "crm_save_lead", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
        Assert.Equal(0, refused.Writes);
        Assert.False(refused.CutoverAllowed);

        var unknown = registry.Evaluate(new CpModuleAjaxWriteRegistryRequest("procurement", "not_a_real_action_zz"));
        Assert.Equal("dry-run-unknown-action", unknown.Status);
        Assert.Equal(0, unknown.Writes);
    }

    [Fact]
    public void PathBoardMentionsAjaxCatalogAndStaysBelow100()
    {
        var report = new AspNetZeroPhpPathReporter().BuildReport();
        Assert.InRange(report.HonestCompletionPct, 97, 99);
        Assert.False(report.CutoverAllowed);
        Assert.Contains(report.Phases, p => p.Id == "4-function-parity" && p.Detail.Contains("ajax_erp", StringComparison.Ordinal));
        Assert.Contains(report.Phases, p => p.Id == "4-function-parity" && p.Detail.Contains("module ajax", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextBuilds, n => n.Contains("on-premises-aspnet", StringComparison.OrdinalIgnoreCase)
            || n.Contains("setup-wizard", StringComparison.OrdinalIgnoreCase)
            || n.Contains("module ajax", StringComparison.OrdinalIgnoreCase));
    }
}
