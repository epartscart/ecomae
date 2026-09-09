using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontConfirmContactWriteTests
{
    [Fact]
    public void Routes_expose_confirm_contact()
    {
        Assert.Equal("/storefront/confirm-contact", EcomAeRoutes.StorefrontConfirmContact);
        Assert.Equal("/storefront/confirm-contact", PhpVendorPortal.ConfirmWriteHref);
        Assert.Equal("/storefront/confirm-contact-app", StorefrontAspNetCanonical.ConfirmContact);
    }

    [Fact]
    public void Type_is_allowlisted_to_email_or_phone()
    {
        Assert.Equal("email", StorefrontConfirmContactWriteService.NormalizeType("EMAIL"));
        Assert.Equal("phone", StorefrontConfirmContactWriteService.NormalizeType("phone"));
        Assert.Null(StorefrontConfirmContactWriteService.NormalizeType("fax"));
        Assert.Null(StorefrontConfirmContactWriteService.NormalizeType("email_code"));
        Assert.Equal("123456", StorefrontConfirmContactWriteService.NormalizeCode(" 123456 "));
    }

    [Fact]
    public void Page_posts_native_confirm_form()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontConfirmContactApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpVendorPortal.ConfirmWriteHref", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"u_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"type\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic twin", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_confirm_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/confirm-contact");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_contact.php", row.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_confirm_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IStorefrontConfirmContactWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontConfirmContact", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontConfirmContactWriteService.cs"));
        Assert.Contains("email_confirmed", service, StringComparison.Ordinal);
        Assert.Contains("phone_confirmed", service, StringComparison.Ordinal);
        Assert.Contains("is \"email\" or \"phone\"", service, StringComparison.Ordinal);
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
