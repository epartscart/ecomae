using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPlatformCommunicationWriteTests
{
    [Fact]
    public void Route_exposes_platform_communication_write()
    {
        Assert.Equal("/cp/platform-communication/write", EcomAeRoutes.CpPlatformCommunicationWrite);
    }

    [Fact]
    public void Normalize_site_category_status_priority_match_php()
    {
        Assert.Equal("epartscart", CpPlatformCommunicationWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal("support", CpPlatformCommunicationWriteService.NormalizeCategory(""));
        Assert.Equal("billing", CpPlatformCommunicationWriteService.NormalizeCategory("billing"));
        Assert.Equal("open", CpPlatformCommunicationWriteService.NormalizeStatus("unknown"));
        Assert.Equal("in_progress", CpPlatformCommunicationWriteService.NormalizeStatus("in_progress"));
        Assert.Equal("normal", CpPlatformCommunicationWriteService.NormalizePriority(""));
        Assert.Equal("urgent", CpPlatformCommunicationWriteService.NormalizePriority("urgent"));
        Assert.Contains("onboarding", CpPlatformCommunicationWriteService.Categories.Keys);
    }

    [Fact]
    public void Page_posts_native_save_and_delete_task()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPlatformCommunicationApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/platform-communication/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_task\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete_task\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"description\"", razor, StringComparison.Ordinal);
        Assert.Contains("Leave blank to keep current description", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("_isAdmin && _isSuper", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_task_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/platform-communication/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_super_cp_communication.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_task()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPlatformCommunicationWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpPlatformCommunicationWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_task", module, StringComparison.Ordinal);
        Assert.Contains("delete_task", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPlatformCommunicationWriteService.cs"));
        Assert.Contains("epc_scp_task_save", service, StringComparison.Ordinal);
        Assert.Contains("epc_scp_task_delete", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_platform_internal_tasks`", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `epc_platform_internal_tasks`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
