using System;
using System.IO;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocumentWorkspaceCustomGroupTests
{
    private static string Razor()
    {
        var relative = Path.Combine("aspnet", "src", "EcomAE.Platform", "Components", "Shared", "Desktop", "ErpDocumentWorkspace.razor");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }

    [Fact]
    public void Workspace_renders_custom_field_groups_as_sections()
    {
        var razor = Razor();
        Assert.Contains("CustomGroups", razor, StringComparison.Ordinal);
        Assert.Contains("<details id=\"@Anchor(group)\"", razor, StringComparison.Ordinal);
        Assert.Contains("string.Equals(f.Group, group, StringComparison.Ordinal)", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_tab_links_stay_on_current_page()
    {
        var razor = Razor();
        Assert.Contains("href=\"@TabHref(tab)\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"#@Anchor(tab)\"", razor, StringComparison.Ordinal);
        Assert.Contains("Navigation.ToBaseRelativePath(Navigation.Uri)", razor, StringComparison.Ordinal);
        Assert.Contains("\"Lines\" => \"lines\"", razor, StringComparison.Ordinal);
    }
}
