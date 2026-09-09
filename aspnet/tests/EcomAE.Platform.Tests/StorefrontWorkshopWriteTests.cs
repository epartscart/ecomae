using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontWorkshopWriteTests
{
    [Fact]
    public void Routes_expose_workshop_book()
    {
        Assert.Equal("/storefront/workshop/book", EcomAeRoutes.StorefrontWorkshopBook);
        Assert.Equal("/storefront/workshop/appointment", EcomAeRoutes.StorefrontWorkshopAppointment);
        Assert.Equal("/storefront/workshop/book", PhpWorkshopPortal.BookWriteHref);
        Assert.Equal("/storefront/auto-workshop-app", PhpWorkshopPortal.TrackWriteHref);
        Assert.Equal("/storefront/workshop/appointment", PhpWorkshopPortal.ManagerWriteHref);
    }

    [Fact]
    public void Phone_tail_matches_php_last_seven_digits()
    {
        Assert.True(StorefrontWorkshopWriteService.PhoneTailMatches("+971 50 123 4567", "0501234567"));
        Assert.True(StorefrontWorkshopWriteService.PhoneTailMatches("0501234567", ""));
        Assert.False(StorefrontWorkshopWriteService.PhoneTailMatches("0501234567", "0509999999"));
        Assert.Equal("Fatima", StorefrontWorkshopWriteService.Normalize("  Fatima "));
    }

    [Fact]
    public void Page_posts_native_book_and_get_track()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAutoWorkshopApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpWorkshopPortal.BookWriteHref", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"complaint\"", razor, StringComparison.Ordinal);
        Assert.Contains("method=\"get\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpWorkshopPortal.TrackWriteHref", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic twin", razor, StringComparison.Ordinal);

        var manager = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageManagerApp.razor"));
        Assert.Contains("PhpWorkshopPortal.ManagerWriteHref", manager, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", manager, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_workshop_writes_live_gated()
    {
        var book = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/workshop/book");
        Assert.Equal("write-live-gated", book.Status);
        Assert.Contains("ajax_workshop_public.php", book.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", book.Notes, StringComparison.Ordinal);
        var appt = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/workshop/appointment");
        Assert.Equal("write-live-gated", appt.Status);
    }

    [Fact]
    public void Program_and_module_workshop_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IStorefrontWorkshopWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontWorkshopBook", module, StringComparison.Ordinal);
        Assert.Contains("StorefrontWorkshopAppointment", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontWorkshopWriteService.cs"));
        Assert.Contains("ICpWorkshopWriteService", service, StringComparison.Ordinal);
        Assert.Contains("CreateJobAsync", service, StringComparison.Ordinal);
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
