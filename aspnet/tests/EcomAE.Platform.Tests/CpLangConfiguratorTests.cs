using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpLangConfiguratorTests
{
    [Fact]
    public void LanguagesApp_RendersConfiguratorAndEditorInsteadOfDigest()
    {
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpLanguagesApp.razor"));
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/lang/configure\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"multilang_on\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"langs_active\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"lang_default\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/lang/save-translation\"", razor, StringComparison.Ordinal);
        Assert.Contains("ICpLangConfiguratorService", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureEndpoint_IsCpGatedAndDryRunByDefault()
    {
        var module = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpLangConfigure", module, StringComparison.Ordinal);
        Assert.Contains("Admin CP capability required for language configuration.", module, StringComparison.Ordinal);
        Assert.Contains("Set confirmWrites=true to save the language configuration on ASP.NET.", module, StringComparison.Ordinal);
        Assert.Contains("ICpLangConfiguratorService, EcomAE.Platform.Cp.CpLangConfiguratorService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Search_DefaultsAreSane()
    {
        var s = CpLangStringSearch.Default;
        Assert.Equal("all", s.Scope);
        Assert.Equal(1, s.Page);
        Assert.Equal(50, s.PageSize);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }
}
