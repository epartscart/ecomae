using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards PHP google_translate_top.php / epc_cp_translate.php twins across ASP.NET chrome.
/// </summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class GoogleTranslateChromeParityTests
{
    [Fact]
    public void StorefrontAssetsAndChromeWireGoogleTranslateTop()
    {
        Assert.True(File.Exists(Find("content/general_pages/google_translate_top.php")));
        Assert.True(File.Exists(Find("content/general_pages/epc_google_translate_storefront.css")));
        Assert.True(File.Exists(Find("content/general_pages/epc_google_translate_storefront.js")));

        var js = File.ReadAllText(Find("content/general_pages/epc_google_translate_storefront.js"));
        Assert.Contains("ipapi.co/json", js, StringComparison.Ordinal);
        Assert.Contains("ipwho.is", js, StringComparison.Ordinal);
        Assert.Contains("epcCfCountryHint", js, StringComparison.Ordinal);
        Assert.Contains("data-cf-country", js, StringComparison.Ordinal);
        Assert.Contains("epcLanguageForCountry", js, StringComparison.Ordinal);
        Assert.Contains("epcCmsLangNavigate", js, StringComparison.Ordinal);
        Assert.Contains("googtrans", js, StringComparison.Ordinal);
        Assert.Contains("epc_lang", js, StringComparison.Ordinal);
        // Manual English must stick; EN markets must not fall through to browser Afrikaans.
        Assert.Contains("US: \"en\"", js, StringComparison.Ordinal);
        Assert.Contains("ZA: \"af\"", js, StringComparison.Ordinal);
        Assert.Contains("epcIsAspNetAppPath", js, StringComparison.Ordinal);
        Assert.Contains("Language set manually:", js, StringComparison.Ordinal);
        Assert.Contains("epcWriteManualLanguage", js, StringComparison.Ordinal);
        // After cancel→English, googtrans must clear before CMS navigate or later picks stick.
        Assert.Contains("epcClearTranslateCookie()", js, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.removeItem(epcTranslateAutoAppliedKey)", js, StringComparison.Ordinal);

        var chrome = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("<PhpGoogleTranslateTop", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-lang-toggle", chrome, StringComparison.Ordinal);

        var component = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/PhpGoogleTranslateTop.razor"));
        Assert.Contains("epc_native_translate_select", component, StringComparison.Ordinal);
        Assert.Contains("google_translate_element", component, StringComparison.Ordinal);
        Assert.Contains("data-cf-country", component, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_google_translate_storefront.js", component, StringComparison.Ordinal);
        Assert.Contains("v=20260811b", component, StringComparison.Ordinal);
        // English first so the control does not look "stuck" on Afrikaans.
        var enIdx = component.IndexOf("<option value=\"en\"", StringComparison.Ordinal);
        var afIdx = component.IndexOf("<option value=\"af\"", StringComparison.Ordinal);
        Assert.True(enIdx >= 0 && afIdx > enIdx);
    }

    [Fact]
    public void CpErpBosMarketingLifeOsWireCompactTranslate()
    {
        Assert.True(File.Exists(Find("content/general_pages/epc_cp_translate.php")));
        Assert.True(File.Exists(Find("content/general_pages/epc_google_translate_cp.css")));
        Assert.True(File.Exists(Find("content/general_pages/epc_google_translate_cp.js")));

        var js = File.ReadAllText(Find("content/general_pages/epc_google_translate_cp.js"));
        Assert.Contains("ipapi.co/json", js, StringComparison.Ordinal);
        Assert.Contains("epcCpTenantDefaultLang", js, StringComparison.Ordinal);
        Assert.Contains("google_translate_element_cp", js, StringComparison.Ordinal);

        Assert.Contains(
            "<PhpCpTranslate Context=\"cp\"",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor")),
            StringComparison.Ordinal);
        Assert.Contains(
            "<PhpCpTranslate Context=\"erp\"",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor")),
            StringComparison.Ordinal);
        Assert.Contains(
            "<PhpCpTranslate Context=\"bos\"",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor")),
            StringComparison.Ordinal);
        Assert.Contains(
            "<PhpCpTranslate Context=\"marketing\"",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingChrome.razor")),
            StringComparison.Ordinal);
        Assert.Contains(
            "<PhpCpTranslate Context=\"marketing\"",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/LifeOs/LifeOsSiteChrome.razor")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAssetsBridgeExposesTranslateFiles()
    {
        var bridge = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_google_translate_storefront.css", bridge, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_google_translate_storefront.js", bridge, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_google_translate_cp.css", bridge, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_google_translate_cp.js", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpReferenceKeepsCountryIpProtocol()
    {
        var php = File.ReadAllText(Find("content/general_pages/google_translate_top.php"));
        Assert.Contains("epc_google_translate_storefront.js", php, StringComparison.Ordinal);
        Assert.Contains("data-cf-country", php, StringComparison.Ordinal);
        Assert.Contains("HTTP_CF_IPCOUNTRY", php, StringComparison.Ordinal);
        Assert.Contains("<option value=\"en\" selected>English</option>", php, StringComparison.Ordinal);
        Assert.DoesNotContain("function epcAutoTranslateByCountry", php, StringComparison.Ordinal);

        var js = File.ReadAllText(Find("content/general_pages/epc_google_translate_storefront.js"));
        Assert.Contains("AE: \"ar\"", js, StringComparison.Ordinal);
        Assert.Contains("ipapi.co/json", js, StringComparison.Ordinal);

        var cp = File.ReadAllText(Find("content/general_pages/epc_cp_translate.php"));
        Assert.Contains("epc_cp_translate_render", cp, StringComparison.Ordinal);
        Assert.Contains("cp_default_lang", cp, StringComparison.Ordinal);
        Assert.Contains("ipapi.co/json", cp, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
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
