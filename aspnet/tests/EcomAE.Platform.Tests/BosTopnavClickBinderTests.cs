using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Locks PHP-parity click-to-open BOS topnav (not CSS :hover).
/// Live /bos menus were dead until a document-level binder matched bos/epc_bos_shell.js.
/// </summary>
public class BosTopnavClickBinderTests
{
    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void BosDesktopChrome_UsesHiddenPanelsAndClickToggleAttrs()
    {
        var chrome = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor");
        Assert.Contains("data-bos-topnav-toggle", chrome);
        Assert.Contains("hidden data-bos-topnav-panel", chrome);
        Assert.Contains("aria-expanded=\"false\"", chrome);
        Assert.Contains(".bos-topnav__panel[hidden]", chrome);
        Assert.DoesNotContain(".bos-topnav__item:hover .bos-topnav__panel", chrome);
        Assert.Contains("<PhpBosTopnavBinder", chrome);
    }

    [Fact]
    public void BosTopnavBinder_DelegatesDocumentClicksOnce()
    {
        var binder = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpBosTopnavBinder.razor");
        Assert.Contains("__epcBosTopnavBound", binder);
        Assert.Contains("[data-bos-topnav-toggle]", binder);
        Assert.Contains(".bos-topnav__item", binder);
        Assert.Contains("is-open", binder);
        Assert.Contains("addEventListener", binder);
    }

    [Fact]
    public void BosSurfaceHead_InstallsBinderOnFirstFullLoad()
    {
        var head = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpSurfaceHead.razor");
        Assert.Contains("Surface.Equals(\"bos\"", head);
        Assert.Contains("<PhpBosTopnavBinder", head);
    }
}
