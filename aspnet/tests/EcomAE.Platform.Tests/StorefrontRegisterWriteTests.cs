using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontRegisterWriteTests
{
    [Fact]
    public void Register_page_route_stays_and_the_native_write_route_is_gone()
    {
        Assert.Equal("/storefront/register-app", EcomAeRoutes.StorefrontRegisterApp);
        Assert.Null(typeof(EcomAeRoutes).GetField("StorefrontRegister"));
        Assert.Null(typeof(PhpCustomerWrites).GetProperty("RegisterHref"));
        Assert.Null(typeof(EcomAeRoutes).Assembly.GetType("EcomAE.Platform.Storefront.StorefrontRegisterWriteService"));
        Assert.DoesNotContain(SurfacePayloadContractCatalog.Functions, item => item.AspNetRouteOrCapability == "/storefront/register");
    }

    [Fact]
    public void Page_renders_the_php_form_posting_to_the_register_engine()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor"));
        Assert.Contains("StorefrontRegFormLoader.RenderAsync", razor, StringComparison.Ordinal);
        Assert.Contains("createIfMissing: true", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpCustomerWrites.RegisterHref", razor, StringComparison.Ordinal);

        var html = StorefrontRegForm.Render(
            new StorefrontRegForm.Input { LangHref = "/en", CsrfGuardKey = "ck", Variants = [new("1", "Retail")] },
            id => "T" + id);
        Assert.Contains("<form action=\"/en" + StorefrontPhpAjax.UsersRegisterPath + "\" id=\"regform\"", html, StringComparison.Ordinal);
        Assert.Contains("method=\"post\" enctype=\"multipart/form-data\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"csrf_guard_key\" value=\"ck\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"reg_contact_type\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"password_repeat\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"epc_email_otp_verified\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmWrites", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_no_longer_wire_the_native_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.DoesNotContain("IStorefrontRegisterWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.DoesNotContain("EcomAeRoutes.StorefrontRegister,", module, StringComparison.Ordinal);
        Assert.DoesNotContain("StorefrontRegisterBody", module, StringComparison.Ordinal);
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
