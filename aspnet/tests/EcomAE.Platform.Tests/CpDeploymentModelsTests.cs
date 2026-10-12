using System.Data.Common;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDeploymentModelsTests
{
    [Fact]
    public void Three_models_and_hybrid_sync_pack()
    {
        Assert.Equal(3, CpDeploymentModelsCatalog.Models.Count);
        Assert.Equal(TenantDeploymentKind.Hybrid, CpDeploymentModelsCatalog.ParseKind("hybrid"));
        Assert.Equal("cloud", CpDeploymentModelsCatalog.Classify("erp_only", "platform", null));
        Assert.Equal("on-premises", CpDeploymentModelsCatalog.Classify("commerce", "client", "on_premises"));
        Assert.Equal("hybrid", CpDeploymentModelsCatalog.Classify("erp_only", "platform", "hybrid"));
        Assert.Contains(CpDeploymentModelsCatalog.Models, m => m.Key == "hybrid" && m.Status.Contains("sync agent live", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(CpDeploymentModelsCatalog.Models, m => m.Key == "on-premises" && m.Status.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(CpDeploymentModelsCatalog.Matrix, r => r.Capability.Contains("Who hosts", StringComparison.Ordinal));
        Assert.Contains(CpDeploymentModelsCatalog.Matrix, r => r.Hybrid.Contains("Sync agent live", StringComparison.Ordinal));
    }

    [Fact]
    public void Page_is_super_cp_and_hosts_setup_downloads()
    {
        Assert.Equal("/cp/deployment-models-app", EcomAeRoutes.ControlPanelDeploymentModelsApp);
        Assert.Equal("/cp/deployment-models/setup-pack", EcomAeRoutes.ControlPanelDeploymentModelsSetupPack);
        Assert.Equal("/cp/hybrid-sync/ingest", EcomAeRoutes.CpHybridSyncIngest);
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpDeploymentModelsApp.razor"));
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("CpDeploymentModelsCatalog", razor, StringComparison.Ordinal);
        Assert.Contains("/cp/deployment-models/setup-pack?kind=", razor, StringComparison.Ordinal);
        Assert.Contains("on-premises-aspnet", razor, StringComparison.Ordinal);
        Assert.Contains("setup.cmd", razor, StringComparison.Ordinal);
        Assert.Contains("install.sh", razor, StringComparison.Ordinal);
        Assert.Contains("Do not add leftover runtime files", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET Core", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);

        Assert.Contains(PhpSuperCpBocNav.Areas, a => a.Key == "deployment_models" && a.Href == "/cp/deployment-models-app");
        Assert.Equal("/cp/deployment-models-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_deployment_models"));
    }

    [Fact]
    public void Setup_packs_resolve_from_the_repo_and_reject_traversal()
    {
        var root = FindRepoRoot();
        Assert.Contains(CpDeploymentSetupPackCatalog.Files, f => f.Kind == "on-premises" && f.RelativePath == "install.sh");
        Assert.Contains(CpDeploymentSetupPackCatalog.Files, f => f.Kind == "on-premises-aspnet" && f.RelativePath == "setup.cmd");
        Assert.Contains(CpDeploymentSetupPackCatalog.Files, f => f.Kind == "hybrid" && f.RelativePath == "sync-agent/sync-agent.py");
        Assert.NotNull(CpDeploymentSetupPackCatalog.ResolveFile("on-premises", "install.sh", root));
        Assert.NotNull(CpDeploymentSetupPackCatalog.ResolveFile("on-premises-aspnet", "install.sh", root));
        Assert.NotNull(CpDeploymentSetupPackCatalog.ResolveFile("on-premises-aspnet", "setup.cmd", root));
        Assert.NotNull(CpDeploymentSetupPackCatalog.ResolveFile("hybrid", "install.sh", root));
        Assert.Null(CpDeploymentSetupPackCatalog.Find("hybrid", "../on-premises/install.sh"));
        Assert.Null(CpDeploymentSetupPackCatalog.ResolveFile("hybrid", "../secret", root));
        var zip = CpDeploymentSetupPackCatalog.ZipKind("hybrid", root);
        Assert.True(zip.Length > 100);
        var aspnetZip = CpDeploymentSetupPackCatalog.ZipKind("on-premises-aspnet", root);
        Assert.True(aspnetZip.Length > 100);
        Assert.DoesNotContain(CpDeploymentSetupPackCatalog.ForKind("on-premises-aspnet"), f => f.RelativePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("ecomae-on-premises-setup.zip", CpDeploymentSetupPackCatalog.ZipFileName("on-premises"));
        Assert.Equal("ecomae-on-premises-aspnet-setup.zip", CpDeploymentSetupPackCatalog.ZipFileName("on-premises-aspnet"));
        var install = File.ReadAllText(CpDeploymentSetupPackCatalog.ResolveFile("on-premises-aspnet", "install.sh", root)!);
        Assert.Contains("will not invent", install, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("php-fpm", install, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("setup-wizard.php", install, StringComparison.Ordinal);
    }

    [Fact]
    public void Hybrid_cannot_skip_the_on_premises_package()
    {
        var now = DateTimeOffset.UtcNow;
        var state = TenantInstallationControlPlane.Start("indj", TenantDeploymentKind.Hybrid, now);
        state = TenantInstallationControlPlane.Advance(state, TenantInstallationStage.ProvisioningCloudTenant, now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() =>
            TenantInstallationControlPlane.Advance(state, TenantInstallationStage.Synchronizing, now.AddMinutes(2)));

        state = TenantInstallationControlPlane.Advance(state, TenantInstallationStage.PackageReady, now.AddMinutes(2));
        Assert.Equal(TenantDeploymentKind.Hybrid, state.DeploymentKind);
        Assert.Equal(40, state.Percent);
    }

    [Fact]
    public void Hybrid_sync_directions_are_accepted()
    {
        var envelope = new TenantSyncEnvelope(
            "env-h",
            "indj",
            "installation-1",
            "customer",
            "1",
            1,
            "hybrid-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            DateTimeOffset.UtcNow);
        var result = TenantInstallationControlPlane.ValidateSyncEnvelope(envelope);
        Assert.True(result.Accepted);
    }

    [Fact]
    public void Hybrid_ownership_queues_conflicts_and_does_not_invent_a_send()
    {
        var now = DateTimeOffset.UtcNow;
        var masterFromSite = new TenantSyncEnvelope(
            "env-m",
            "acme",
            "inst_1",
            "customer",
            "c1",
            1,
            "hybrid-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            now);
        var masterDecision = HybridSyncControlPlane.DecideIngest(masterFromSite, null, 0, null);
        Assert.Equal(HybridSyncDecisionKind.Conflict, masterDecision.Kind);
        Assert.Equal("ownership-conflict", masterDecision.Code);

        var txnFromCloud = new TenantSyncEnvelope(
            "env-t",
            "acme",
            "inst_1",
            "sales_order",
            "so1",
            1,
            "cloud-to-hybrid",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            now);
        Assert.Equal(HybridSyncDecisionKind.Conflict, HybridSyncControlPlane.DecideIngest(txnFromCloud, null, 0, null).Kind);

        var txnFromSite = new TenantSyncEnvelope(
            "env-ok",
            "acme",
            "inst_1",
            "sales_order",
            "so1",
            2,
            "hybrid-to-cloud",
            TenantInstallationControlPlane.Sha256Hex("{}"),
            now);
        Assert.Equal(HybridSyncDecisionKind.Accept, HybridSyncControlPlane.DecideIngest(txnFromSite, null, 0, null).Kind);
        Assert.Equal(HybridSyncDecisionKind.Replay, HybridSyncControlPlane.DecideIngest(txnFromSite, "env-ok", 2, "queued").Kind);
        Assert.Equal(HybridSyncDecisionKind.Conflict, HybridSyncControlPlane.DecideIngest(txnFromSite, "env-other", 3, "queued").Kind);

        var empty = HybridSyncControlPlane.DecidePull(0);
        Assert.Equal(HybridSyncDecisionKind.Empty, empty.Kind);
        Assert.Contains("does not invent a send", empty.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(HybridSyncControlPlane.IsEnrollmentRequestId(HybridSyncControlPlane.NewEnrollmentRequestId()));
        Assert.DoesNotContain("password", HybridSyncControlPlane.SanitizePayloadJson("""{"name":"Acme","password":"secret"}"""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hybrid_sync_persists_cursor_and_queues_conflicts()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_hyb_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await ExecAsync(admin, "CREATE DATABASE `" + database + "` DEFAULT CHARACTER SET utf8mb4");
        try
        {
            var sync = new HybridSyncService(new Connections(cs));
            var registered = await sync.RegisterAsync("acme", "inst_test", "enr_testdryrun01", "https://www.ecomae.com", "hybrid", confirmWrites: false);
            Assert.True(registered.DryRun);
            Assert.Equal(0, registered.Writes);

            registered = await sync.RegisterAsync("acme", "inst_test", "enr_testdryrun01", "https://www.ecomae.com", "hybrid", confirmWrites: true);
            Assert.True(registered.Ok);
            Assert.True(registered.Writes > 0);
            Assert.True(await sync.EnrollmentMatchesAsync("acme", "inst_test", "enr_testdryrun01"));

            var payload = """{"doc":"SO-1"}""";
            var envelope = new TenantSyncEnvelope(
                "env_so1",
                "acme",
                "inst_test",
                "sales_order",
                "so1",
                1,
                "hybrid-to-cloud",
                TenantInstallationControlPlane.Sha256Hex(payload),
                DateTimeOffset.UtcNow);
            var dry = await sync.IngestAsync(envelope, payload, confirmWrites: false);
            Assert.True(dry.DryRun);
            Assert.Equal(0, dry.Writes);

            var live = await sync.IngestAsync(envelope, payload, confirmWrites: true);
            Assert.True(live.Ok);
            Assert.Equal("accepted", live.Code);

            var replay = await sync.IngestAsync(envelope, payload, confirmWrites: true);
            Assert.Equal("replay", replay.Code);
            Assert.Equal(0, replay.Writes);

            var masterClash = new TenantSyncEnvelope(
                "env_cust",
                "acme",
                "inst_test",
                "customer",
                "c1",
                1,
                "hybrid-to-cloud",
                TenantInstallationControlPlane.Sha256Hex("{}"),
                DateTimeOffset.UtcNow);
            var conflict = await sync.IngestAsync(masterClash, "{}", confirmWrites: true);
            Assert.Equal("ownership-conflict", conflict.Code);

            var empty = await sync.PullAsync("acme", "inst_test", confirmWrites: false);
            Assert.Equal("empty", empty.Code);
            Assert.Contains("does not invent a send", empty.Message, StringComparison.OrdinalIgnoreCase);

            var status = await sync.StatusAsync("acme");
            Assert.Contains(status.Installations, i => i.SiteKey == "acme");
            Assert.Contains(status.Cursors, c => c.Direction == "hybrid-to-cloud");
            Assert.Contains(status.Conflicts, c => c.Reason == "ownership-conflict");
        }
        finally
        {
            await ExecAsync(admin, "DROP DATABASE IF EXISTS `" + database + "`");
        }
    }

    private sealed class Connections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var connection = new MySqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
