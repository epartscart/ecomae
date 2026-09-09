using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontRegisterWriteTests
{
    [Fact]
    public void Routes_expose_register()
    {
        Assert.Equal("/storefront/register", EcomAeRoutes.StorefrontRegister);
        Assert.Equal("/storefront/register-app", EcomAeRoutes.StorefrontRegisterApp);
        Assert.Equal("/storefront/register", PhpCustomerWrites.RegisterHref);
    }

    [Fact]
    public void Normalize_contact_type_and_shape()
    {
        Assert.Equal("email", StorefrontRegisterWriteService.NormalizeContactType("EMAIL"));
        Assert.Equal("phone", StorefrontRegisterWriteService.NormalizeContactType("phone"));
        Assert.Null(StorefrontRegisterWriteService.NormalizeContactType("fax"));
        Assert.Equal("a@b.com", StorefrontRegisterWriteService.NormalizeContact("  a@b.com  "));
        Assert.True(StorefrontRegisterWriteService.LooksLikeEmail("buyer@example.com"));
        Assert.False(StorefrontRegisterWriteService.LooksLikeEmail("not-an-email"));
        Assert.True(StorefrontRegisterWriteService.LooksLikePhone("+971567607011"));
        Assert.False(StorefrontRegisterWriteService.LooksLikePhone("12"));
    }

    [Fact]
    public void Page_posts_native_register_form()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpCustomerWrites.RegisterHref", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact_type\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"users_agreement\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("enctype=\"multipart/form-data\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_register_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/register");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("register.php", row.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_register_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IStorefrontRegisterWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontRegister", module, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontRegisterWriteService.cs")), StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontRegisterWriteService.cs"));
        Assert.Contains("INSERT INTO `users`", service, StringComparison.Ordinal);
        Assert.Contains("users_groups_bind", service, StringComparison.Ordinal);
        Assert.DoesNotContain("send_notify", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repo root.");
    }
}
