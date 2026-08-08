using System.Text.RegularExpressions;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guest complaint: CP autofilled confidential email/password.
/// All login surfaces must block credential autofill and offer Google sign-in.
/// </summary>
public sealed class LoginAutofillAndGoogleParityTests
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

    public static TheoryData<string> LoginSurfaces => new()
    {
        "aspnet/src/EcomAE.Platform/Components/Pages/CpLoginApp.razor",
        "aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor",
        "aspnet/src/EcomAE.Platform/Components/Pages/BosLoginApp.razor",
        "aspnet/src/EcomAE.Platform/Components/Pages/IpLoginApp.razor",
        "aspnet/src/EcomAE.Platform/Components/Pages/LifeOsApp.razor",
        "aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor",
    };

    [Theory]
    [MemberData(nameof(LoginSurfaces))]
    public void LoginSurface_BlocksCredentialAutofillAndOffersGoogle(string path)
    {
        var text = Read(path);
        Assert.Contains("PhpOAuthLoginButtons", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-anti-autofill", text, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", text, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"new-password\"", text, StringComparison.Ordinal);
        Assert.Contains("data-lpignore=\"true\"", text, StringComparison.Ordinal);
        Assert.Contains("epc_autofill_decoy", text, StringComparison.Ordinal);

        // Decoy inputs may still use username/current-password tokens to absorb autofill.
        var withoutDecoyBlocks = Regex.Replace(
            text,
            @"aria-hidden=""true""[\s\S]*?</div>",
            "",
            RegexOptions.IgnoreCase);

        Assert.False(
            Regex.IsMatch(withoutDecoyBlocks, @"name=""contact""[^>]*autocomplete=""username"""),
            $"{path}: contact field must not use autocomplete=username");
        Assert.False(
            Regex.IsMatch(withoutDecoyBlocks, @"name=""password""[^>]*autocomplete=""current-password"""),
            $"{path}: password field must not use autocomplete=current-password");
    }

    [Fact]
    public void StorefrontLoginApp_UsesSharedFormWithGoogle()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontLoginApp.razor");
        Assert.Contains("LegacyAdminLoginForm", text, StringComparison.Ordinal);
        Assert.Contains("Surface=\"LegacyLoginSurface.Storefront\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthButtons_PointAtPhpAuthoritativeStart()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Shared/PhpOAuthLoginButtons.razor");
        Assert.Contains("/api/epc_oauth_start.php", text, StringComparison.Ordinal);
        Assert.Contains("Continue with Google", text, StringComparison.Ordinal);
        Assert.Contains("epc_oauth_login_buttons.css", text, StringComparison.Ordinal);
        Assert.Contains("provider=\"google\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthCss_BridgedViaPlatformAssets()
    {
        var bridge = Read("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs");
        Assert.Contains("/platform-assets/epc_oauth_login_buttons.css", bridge, StringComparison.Ordinal);
        Assert.True(File.Exists(FindRepoFile("content/general_pages/epc_oauth_login_buttons.css")));
    }

    [Fact]
    public void LifeOsResults_DoesNotStealPasswordManagerSlots()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/LifeOsResultsApp.razor");
        Assert.DoesNotContain("autocomplete=\"username\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("autocomplete=\"current-password\"", text, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", text, StringComparison.Ordinal);
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
