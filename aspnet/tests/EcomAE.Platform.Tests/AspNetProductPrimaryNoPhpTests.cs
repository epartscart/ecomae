using EcomAE.Platform.Configuration;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Product must be ASP.NET-based; PHP is reference-only until ReadyToRemovePhp.
/// </summary>
public sealed class AspNetProductPrimaryNoPhpTests
{
    [Fact]
    public void PhpReferenceOptions_DefaultsPreferAspNetStorefrontApps()
    {
        var opts = new PhpReferenceOptions();
        Assert.True(opts.PreferAspNetStorefrontApps);
        Assert.True(opts.KeepPhpProjectAvailable);
        Assert.False(opts.TemporarilyDeactivatePhpServing);
    }

    [Fact]
    public void Appsettings_DeclaresPreferAspNetStorefrontApps()
    {
        var json = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/appsettings.json"));
        Assert.Contains("\"PreferAspNetStorefrontApps\": true", json, StringComparison.Ordinal);
        Assert.Contains("KeepPhpProjectAvailable", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_WiresPreferAspNetFromStorefrontFlagNotOnlyTempDeactivate()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("PreferAspNetStorefrontApps", text, StringComparison.Ordinal);
        Assert.Contains("KeepPhpProjectAvailable must stay true", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "StorefrontSurfaceLinks.PreferAspNetApps = phpRef.TemporarilyDeactivatePhpServing;",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StubMiddleware_SkipsWithAspNetPrimaryHeader()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Middleware/StorefrontStubToPhpRedirectMiddleware.cs"));
        Assert.Contains("skipped-aspnet-primary", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductsOfBunch_UsesAspNetWarehouseForProtocol3()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("aspnet-warehouse", text, StringComparison.Ordinal);
        Assert.Contains("officeId == 0 && storageId == 0", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_StatusIsProductPrimaryPhpReferenceOnly()
    {
        var reporter = new PhpReferenceModeReporter(
            Options.Create(new PhpReferenceOptions()),
            Options.Create(new MigrationRouteCutoverOptions
            {
                RequirePhpFallback = true,
                StorefrontAspNetEnabled = true,
                AdminAspNetEnabled = true
            }));
        var report = reporter.BuildReport();
        Assert.Equal("aspnet-product-primary-php-reference-only", report.Status);
        Assert.True(report.PreferAspNetStorefrontApps);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.KeepPhpProjectAvailable);
        Assert.Contains(report.HardLocks, l => l.Contains("PreferAspNetStorefrontApps=true", StringComparison.Ordinal));
        Assert.Contains(report.HardLocks, l => l.Contains("never invent", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ErpModuleApp.razor")]
    [InlineData("StorefrontCheckoutApp.razor")]
    [InlineData("StorefrontVinApp.razor")]
    [InlineData("StorefrontBulkUploadApp.razor")]
    [InlineData("StorefrontRegisterApp.razor")]
    public void ProductApps_PhpIframesAreOptInOnly(string fileName)
    {
        var text = File.ReadAllText(Find($"aspnet/src/EcomAE.Platform/Components/Pages/{fileName}"));
        if (!text.Contains("<iframe", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("PhpHybridWorkspaceFrame", StringComparison.Ordinal))
        {
            return;
        }

        // PHP hybrid / iframe hosts must gate on an explicit opt-in (php= / classic=).
        Assert.True(
            text.Contains("_showPhpCompare", StringComparison.Ordinal)
            || text.Contains("_useClassic", StringComparison.Ordinal)
            || text.Contains("_hybridPhp", StringComparison.Ordinal)
            || text.Contains("Query[\"php\"]", StringComparison.Ordinal),
            $"{fileName} embeds PHP without an opt-in gate");
    }

    private static string Find(string relative)
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
