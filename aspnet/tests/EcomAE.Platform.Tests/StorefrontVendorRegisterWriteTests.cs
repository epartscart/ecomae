using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontVendorRegisterWriteTests
{
    [Fact]
    public void Routes_expose_vendor_register()
    {
        Assert.Equal("/storefront/vendor/register", EcomAeRoutes.StorefrontVendorRegister);
        Assert.Equal("/storefront/vendor/register", PhpVendorPortal.RegisterWriteHref);
        Assert.Equal("/storefront/vendor-register-app", StorefrontAspNetCanonical.VendorRegister);
    }

    [Fact]
    public void Normalize_and_trn_helpers_match_php()
    {
        Assert.Equal("ops@local.test", StorefrontVendorRegisterWriteService.NormalizeEmail("  Ops@Local.TEST "));
        Assert.True(StorefrontVendorRegisterWriteService.LooksLikeEmail("ops@local.test"));
        Assert.False(StorefrontVendorRegisterWriteService.LooksLikeEmail("not-an-email"));
        Assert.Equal("100123456700003", StorefrontVendorRegisterWriteService.DigitsOnly("100-123-4567-00003"));
        Assert.True(StorefrontVendorRegisterWriteService.LooksLikeTrn("100123456700003"));
        Assert.False(StorefrontVendorRegisterWriteService.LooksLikeTrn("123"));
        Assert.Equal("1001234567", StorefrontVendorRegisterWriteService.TinFromTrn("100123456700003"));
        Assert.Equal("0235:1001234567", StorefrontVendorRegisterWriteService.PeppolFromTin("1001234567"));
        Assert.Equal("TL", StorefrontVendorRegisterWriteService.NormalizeLegalRegType("tl"));
        Assert.Equal("TL", StorefrontVendorRegisterWriteService.NormalizeLegalRegType("nope"));
        Assert.Equal("S-UAE", CpVendorApprovalWriteService.SanitizeShort(" S-UAE "));
        Assert.Equal("S-UAE Trading", CpVendorApprovalWriteService.SanitizeFull(" S-UAE   Trading "));
    }

    [Fact]
    public void Page_posts_native_vendor_register_form()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVendorRegisterApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpVendorPortal.RegisterWriteHref", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"email\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"password2\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor_short\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"trn\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("checked=\"@", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_vendor_register_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/vendor/register");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("vendor_register.php", row.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_register_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IStorefrontVendorRegisterWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontVendorRegister", module, StringComparison.Ordinal);
        Assert.Contains("vendor-app?registered=1", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontVendorRegisterWriteService.cs"));
        Assert.Contains("INSERT INTO `users`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_vendor_accounts`", service, StringComparison.Ordinal);
        Assert.Contains("ICpVendorApprovalWriteService", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("send_notify", service, StringComparison.Ordinal);
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
