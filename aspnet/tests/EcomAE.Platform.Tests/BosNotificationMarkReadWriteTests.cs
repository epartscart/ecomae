using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosNotificationMarkReadWriteTests
{
    [Fact]
    public void Route_exposes_mark_read()
    {
        Assert.Equal("/bos/notifications/mark-read", EcomAeRoutes.BosNotificationsMarkRead);
    }

    [Fact]
    public void Parse_ids_matches_php()
    {
        Assert.False(BosNotificationWriteService.TryParseIds(null, out _));
        Assert.False(BosNotificationWriteService.TryParseIds("", out _));
        Assert.False(BosNotificationWriteService.TryParseIds("[]", out _));
        Assert.False(BosNotificationWriteService.TryParseIds("{}", out _));
        Assert.True(BosNotificationWriteService.TryParseIds("[1,2]", out var jsonIds));
        Assert.Equal(new long[] { 1, 2 }, jsonIds);
        Assert.True(BosNotificationWriteService.TryParseIds("[0]", out var zero));
        Assert.Equal(new long[] { 0 }, zero);
        Assert.True(BosNotificationWriteService.TryParseIds("1,2,3", out var csv));
        Assert.Equal(new long[] { 1, 2, 3 }, csv);
        Assert.True(BosNotificationWriteService.TryParseIds("5", out var one));
        Assert.Equal(new long[] { 5 }, one);
        Assert.Equal("__platform__", BosNotificationWriteService.ResolveTenantKey(null));
        Assert.Equal("", BosNotificationWriteService.ResolveTenantKey(""));
        Assert.Equal("acme", BosNotificationWriteService.ResolveTenantKey("acme"));
        Assert.Equal(1, BosNotificationWriteService.PhpIntval("1abc"));
        Assert.Equal(0, BosNotificationWriteService.PhpIntval("abc"));
    }

    [Fact]
    public void Page_posts_native_mark_read()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/notifications/mark-read\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"ids\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"tenant_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_mark_read_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/notifications/mark-read");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("mark_read", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_notifications", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_mark_read_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosNotificationsMarkRead", module, StringComparison.Ordinal);
        Assert.Contains("MarkReadAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IBosNotificationWriteService", program, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosNotificationWriteService.cs"));
        Assert.Contains("epc_notifications_mark_read", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_notifications`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var policy = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs"));
        Assert.Contains("\"notifications\"", policy, StringComparison.Ordinal);
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
