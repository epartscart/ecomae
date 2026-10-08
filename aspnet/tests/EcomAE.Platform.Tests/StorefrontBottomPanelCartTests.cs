using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <c>Fixtures/BottomPanelCart/harness.php</c> echoed the cart script of PHP <c>bottom_panel.php</c> for every
/// csrf key, <c>session</c> cookie and template case; ASP.NET must print the same bytes.
/// </summary>
public sealed class StorefrontBottomPanelCartTests
{
    [Fact]
    public void Script_MatchesPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BottomPanelCart", "golden.json"))).RootElement;
        var count = 0;
        foreach (var c in golden.GetProperty("results").EnumerateArray())
        {
            var actual = StorefrontBottomPanelCart.Script(
                c.GetProperty("csrf").GetString()!,
                c.GetProperty("session_cookie").GetBoolean(),
                c.GetProperty("template").GetInt64() == StorefrontBottomPanelCart.NewFrontTemplateId);
            Assert.True(c.GetProperty("html").GetString() == actual, c.GetProperty("name").GetString() + ":\n" + actual);
            count++;
        }

        Assert.Equal(4, count);
    }

    [Fact]
    public void DesktopChrome_PrintsTheCartScriptAndAddedLabel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform")))
        {
            dir = dir.Parent;
        }

        var chrome = File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("StorefrontBottomPanelCart.Script(", chrome, StringComparison.Ordinal);
        Assert.Contains("id=\"mark_popup_added\" class=\"badge-primary badge-round panel-primary hidden\">@_addedLabel</span>", chrome, StringComparison.Ordinal);
    }
}
