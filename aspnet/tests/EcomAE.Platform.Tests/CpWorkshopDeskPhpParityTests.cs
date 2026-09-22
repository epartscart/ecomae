using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// cp/content/shop/workshop (workshop_main_page.php + ajax_workshop_endpoint.php + epc_workshop.js)
/// twin: the desk page and its JSON dispatcher must keep the PHP action / DOM / asset contract.
/// </summary>
public sealed class CpWorkshopDeskPhpParityTests
{
    private static readonly string[] PhpActions =
    [
        "seed_demo", "create_job", "set_status", "assign", "add_line", "get_job", "list_jobs",
        "save_bay", "save_tech", "create_appointment", "convert_appointment", "list_appointments",
    ];

    [Fact]
    public void Dispatcher_CoversEveryPhpAjaxAction_AndKeepsCpAdminGate()
    {
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/workshop/ajax_workshop_endpoint.php"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var start = module.IndexOf("MapPost(EcomAeRoutes.CpWorkshopTerminalAjax", StringComparison.Ordinal);
        Assert.True(start > 0);
        var dispatcher = module[start..];

        foreach (var action in PhpActions)
        {
            Assert.Contains("case '" + action + "':", php, StringComparison.Ordinal);
            Assert.Contains("case \"" + action + "\":", dispatcher, StringComparison.Ordinal);
        }

        Assert.Equal("/cp/workshop/terminal-ajax", EcomAeRoutes.CpWorkshopTerminalAjax);
        Assert.Contains("session.Kind != LegacySessionKind.Admin || !session.Capabilities.Contains(\"cp\")", dispatcher, StringComparison.Ordinal);
        Assert.Contains("message = \"Access denied\"", dispatcher, StringComparison.Ordinal);
    }

    [Fact]
    public void Statuses_AndBoardColumns_MatchPhpHelper()
    {
        var php = File.ReadAllText(FindRepoFile("content/shop/workshop/epc_workshop_helpers.php"));
        foreach (var (key, label) in CpWorkshopDeskService.Statuses)
        {
            Assert.Contains("'" + key + "' => '" + label + "'", php, StringComparison.Ordinal);
        }

        Assert.Equal(["checkin", "estimate", "approved", "in_progress", "qc", "ready"], CpWorkshopDeskService.BoardColumns);
        Assert.Equal("QC / test", CpWorkshopDeskService.StatusLabel("qc"));
        Assert.True(CpWorkshopDeskService.IsStatus("delivered"));
        Assert.False(CpWorkshopDeskService.IsStatus("bogus"));
    }

    [Fact]
    public void DeskPage_KeepsPhpDomIdsTabsAndAssets()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWorkshopApp.razor"));
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/workshop/workshop_main_page.php"));
        foreach (var id in new[] { "epc-ws-root", "epc-ws-msg", "epc-ws-seed", "epc-ws-checkin-form", "epc-ws-detail", "epc-ws-detail-body" })
        {
            Assert.Contains("id=\"" + id + "\"", php, StringComparison.Ordinal);
            Assert.Contains("id=\"" + id + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var tab in new[] { "board", "jobs", "checkin", "schedule", "resources", "guide" })
        {
            Assert.Contains("(\"" + tab + "\", ", razor, StringComparison.Ordinal);
        }

        Assert.Contains("window.EPC_WORKSHOP", razor, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_workshop.css", razor, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_workshop.js", razor, StringComparison.Ordinal);
        Assert.Contains("ICpWorkshopDeskService", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP module", razor, StringComparison.Ordinal);

        var bridge = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("cp/content/shop/workshop/epc_workshop.css", bridge, StringComparison.Ordinal);
        Assert.Contains("cp/content/shop/workshop/epc_workshop.js", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void DeskService_UsesParameterizedSqlOnly()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpWorkshopDeskService.cs"));
        Assert.DoesNotContain("+ status", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{status}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{jobId}", text, StringComparison.Ordinal);
        Assert.Contains("WHERE j.status = ?", text, StringComparison.Ordinal);
        Assert.Contains("ErpDb.AddParameters(c, status)", text, StringComparison.Ordinal);
        Assert.Contains("ErpDb.AddParameters(c, jobId)", text, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
