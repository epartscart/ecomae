using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTemplatesPluginsModulesTwinTests
{
    [Fact]
    public void Edit_mode_cookie_and_page_follow_php()
    {
        var ctx = new DefaultHttpContext();
        Assert.Equal(1, CpTemplatesPluginsService.ReadIsFrontend(ctx.Request));
        Assert.Equal(0, CpTemplatesPluginsService.ReadPage(ctx.Request));

        ctx.Request.Headers.Cookie = "edit_mode=backend";
        ctx.Request.QueryString = new QueryString("?s_page=3");
        Assert.Equal(0, CpTemplatesPluginsService.ReadIsFrontend(ctx.Request));
        Assert.Equal(3, CpTemplatesPluginsService.ReadPage(ctx.Request));

        Assert.Equal(20, CpTemplatesPluginsService.PageLimit);
        Assert.Equal(0, CpTemplatesPluginsService.Pages(0));
        Assert.Equal(1, CpTemplatesPluginsService.Pages(20));
        Assert.Equal(2, CpTemplatesPluginsService.Pages(21));
    }

    [Fact]
    public void Module_json_helpers_follow_php_shapes()
    {
        Assert.Equal([3, 7, 9], CpTemplatesPluginsService.ModuleIds("[3,\"7\",9]").Order());
        Assert.Empty(CpTemplatesPluginsService.ModuleIds(""));
        Assert.Empty(CpTemplatesPluginsService.ModuleIds("{bad"));

        var positions = CpTemplatesPluginsService.ModulePositions(
            "[{\"type\":\"module\",\"name\":\"left\",\"caption\":\"Left column\"},{\"type\":\"content\",\"name\":\"main\"},{\"type\":\"module\",\"name\":\"footer\"}]");
        Assert.Equal(2, positions.Count);
        Assert.Equal(new CpWidgetOption("left", "Left column"), positions[0]);
        Assert.Equal(new CpWidgetOption("footer", "footer"), positions[1]);
    }

    [Fact]
    public void Plugin_path_resolution_stays_inside_docroot()
    {
        var root = Path.Combine(Path.GetTempPath(), "epc-docroot-" + Guid.NewGuid().ToString("N"));
        Assert.Null(CpPluginsWriteService.ResolvePluginPath(root, "../etc/passwd"));
        Assert.Null(CpPluginsWriteService.ResolvePluginPath("", "cp/plugins/x"));
        var ok = CpPluginsWriteService.ResolvePluginPath(root, "/<backend_dir>/plugins/demo");
        Assert.NotNull(ok);
        Assert.StartsWith(Path.GetFullPath(root), ok, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("cp", "plugins", "demo"), ok, StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_exist_for_templates_plugins_edit_mode()
    {
        Assert.Equal("/cp/templates-manager/save", EcomAeRoutes.ControlPanelTemplatesSave);
        Assert.Equal("/cp/templates-manager/delete", EcomAeRoutes.ControlPanelTemplatesDelete);
        Assert.Equal("/cp/plugins-manager/save", EcomAeRoutes.ControlPanelPluginsSave);
        Assert.Equal("/cp/plugins-manager/delete", EcomAeRoutes.ControlPanelPluginsDelete);
        Assert.Equal("/cp/control/edit-mode", EcomAeRoutes.ControlPanelControlEditMode);
        Assert.Equal("/cp/modules/write", EcomAeRoutes.CpModulesWrite);
    }

    [Theory]
    [InlineData("CpTemplatesManagerApp.razor", "/cp/templates-manager/save", "/cp/templates-manager/delete", "/cp/templates-manager/set-current", "templates_action_type", "Templates.TemplatesAsync(")]
    [InlineData("CpPluginsManagerApp.razor", "/cp/plugins-manager/save", "/cp/plugins-manager/delete", "/cp/plugins-manager/activate", "plugins_action_type", "Plugins.PluginsAsync(")]
    [InlineData("CpModulesApp.razor", "/cp/modules/write", "modules_list", "prototype_id", "modules_action_type", "Modules.ModulesAsync(")]
    public void Razor_pages_are_php_shaped_twins_not_digest_shells(string file, params string[] required)
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages", file));
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ISurfaceDashboardSummaryReporter", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Digest", razor, StringComparison.Ordinal);
        Assert.Contains("@inject ICpTemplatesPluginsService", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\" value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/control/edit-mode\"", razor, StringComparison.Ordinal);
        Assert.Contains("CpTemplatesPluginsService.ReadIsFrontend(ctx.Request)", razor, StringComparison.Ordinal);
        Assert.Contains("CpTemplatesPluginsService.ReadPage(ctx.Request)", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"pagination\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"panel_a\"", razor, StringComparison.Ordinal);
        Assert.Contains("<CpDataStructureWidget Field=\"f\" />", razor, StringComparison.Ordinal);
        foreach (var r in required)
        {
            Assert.Contains(r, razor, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Modules_editor_binds_pages_groups_and_data_like_php()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpModulesApp.razor"));
        foreach (var name in new[] { "module_save_action", "caption", "caption_lang_str_id", "content_type", "content_lang_str_id", "position", "order", "activated", "show_caption", "for_all", "data_value", "content_array", "groups_allowed", "prototype_name_lang_str_id", "is_frontend" })
        {
            Assert.Contains("name=\"" + name + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("Modules.ModuleEditorAsync(_editId, _prototypeId, _isFrontend", razor, StringComparison.Ordinal);
        Assert.Contains("Modules.ModulePrototypesAsync(_isFrontend", razor, StringComparison.Ordinal);
        Assert.Contains("e.ContentType == \"text\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Widget_component_covers_php_get_widget_types()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Shared/CpDataStructureWidget.razor"));
        foreach (var type in new[] { "\"hidden\"", "\"checkbox\"", "\"select\"", "\"radio\"", "\"textarea\"", "\"number\"", "\"color\"", "\"password\"", "\"image\"", "\"image_file\"" })
        {
            Assert.Contains("case " + type + ":", razor, StringComparison.Ordinal);
        }

        Assert.Contains("name=\"dv_lang_@Field.Name\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Services_use_parameterized_sql_and_are_registered()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTemplatesPluginsService", program, StringComparison.Ordinal);

        foreach (var f in new[] { "CpTemplatesPluginsService.cs", "CpTemplatesWriteService.cs", "CpPluginsWriteService.cs", "CpModuleWriteService.cs" })
        {
            var src = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp", f));
            Assert.DoesNotContain("' + ", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"SELECT", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"UPDATE", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"DELETE", src, StringComparison.Ordinal);
        }

        var plugins = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpPluginsWriteService.cs"));
        Assert.Contains("control_lock", plugins, StringComparison.Ordinal);
        Assert.Contains("TwoFactorChannelReadyAsync", plugins, StringComparison.Ordinal);
        var templates = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpTemplatesWriteService.cs"));
        Assert.Contains("`current`", templates, StringComparison.Ordinal);
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

        throw new InvalidOperationException("repo root not found");
    }
}
