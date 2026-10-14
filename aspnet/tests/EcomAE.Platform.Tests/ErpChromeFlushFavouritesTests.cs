using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Logo and process menu sit flush left so the header reclaims width,
/// and Favourites pin any module from the top bar without opening the dedicated favourites app.
/// </summary>
public sealed class ErpChromeFlushFavouritesTests
{
    [Fact]
    public void Chrome_FlushesLogoAndMenuLeft()
    {
        var chrome = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");
        var styles = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpChromeStyles.razor");
        var portal = Read("content/shop/finance/epc_erp_portal.css");
        var professional = Read("content/shop/finance/epc_erp_professional.css");

        Assert.Contains("width:100%; max-width:none; margin:0", chrome, StringComparison.Ordinal);
        Assert.Contains("justify-content:flex-start", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("1480px", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("width:min(1480px", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("margin:0 auto", chrome, StringComparison.Ordinal);

        Assert.Contains("max-width: none !important", styles, StringComparison.Ordinal);
        Assert.Contains("margin: 0 !important", styles, StringComparison.Ordinal);
        Assert.Contains("padding-left: 4px !important", styles, StringComparison.Ordinal);
        Assert.Contains("flex-direction: row !important", styles, StringComparison.Ordinal);

        Assert.Contains(".epc-erp-topbar__inner {\n\tmax-width: none;", portal.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-start;", portal, StringComparison.Ordinal);
        Assert.DoesNotContain(".epc-erp-topbar__inner {\n\tmax-width: 1400px;", portal.Replace("\r\n", "\n"), StringComparison.Ordinal);

        Assert.Contains("padding: 0 10px 0 4px;", professional, StringComparison.Ordinal);
        Assert.Contains("max-width: none;", professional, StringComparison.Ordinal);
        Assert.Contains("z-index:500", chrome, StringComparison.Ordinal);
        Assert.Contains("z-index: 500 !important", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("z-index: 80 !important", styles, StringComparison.Ordinal);
        Assert.Contains("z-index: 220", professional, StringComparison.Ordinal);
    }

    [Fact]
    public void Chrome_PinsModulesFromTopFavourites()
    {
        var chrome = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");

        Assert.Contains("IErpShortcutReadService", chrome, StringComparison.Ordinal);
        Assert.Contains("Favourites", chrome, StringComparison.Ordinal);
        Assert.Contains("data-epc-fav-toggle", chrome, StringComparison.Ordinal);
        Assert.Contains("data-epc-fav-panel", chrome, StringComparison.Ordinal);
        Assert.Contains("data-epc-fav-filter", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-erp-fav__chip", chrome, StringComparison.Ordinal);
        Assert.Contains("Add this page", chrome, StringComparison.Ordinal);
        Assert.Contains("Add to favourites", chrome, StringComparison.Ordinal);
        Assert.Contains("Remove from favourites", chrome, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/shortcut-add\"", chrome, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/shortcut-delete\"", chrome, StringComparison.Ordinal);
        Assert.Contains("LoadFavouritesAsync", chrome, StringComparison.Ordinal);
        Assert.Contains("IsFavoriteHref", chrome, StringComparison.Ordinal);
        Assert.Contains("FavoriteReturnHref", chrome, StringComparison.Ordinal);
        Assert.Contains("__epcErpFavBound", chrome, StringComparison.Ordinal);
        Assert.Contains("> Favourites", chrome, StringComparison.Ordinal);
        Assert.Contains("nothing is added as a tab", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain(">PHP<", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain(">Classic<", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain(">ASP.NET<", chrome, StringComparison.Ordinal);
    }

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

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
