using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards new-customer → order journey twins for epartscart (PHP reference parity).</summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class EpartscartCustomerJourneyParityTests
{
    [Fact]
    public void RegistrationAndLoginAliasesExist()
    {
        Assert.Equal("/storefront/register-app", StorefrontAspNetCanonical.Registration);
        Assert.Equal("/en/users/registration", StorefrontPhpCanonical.Registration);
        Assert.Equal("/storefront/login", StorefrontAspNetCanonical.Login);

        var reg = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor"));
        Assert.Contains("@page \"/storefront/register-app\"", reg, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/users/registration\"", reg, StringComparison.Ordinal);
        Assert.Contains("/php-reference/en/users/register", reg, StringComparison.Ordinal);

        var login = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontLoginApp.razor"));
        Assert.Contains("@page \"/en/users/login\"", login, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Registration", login, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeRegisterUsesSurfaceLinksNotDeadEnPath()
    {
        var chrome = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("StorefrontSurfaceLinks.Registration", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/en/users/registration\"", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutAndOrdersKeepPhpCanonicalAliases()
    {
        var checkout = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCheckoutApp.razor"));
        Assert.Contains("@page \"/en/shop/checkout/how_get\"", checkout, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/checkout/confirm\"", checkout, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.CheckoutHowGet", checkout, StringComparison.Ordinal);

        var orders = File.ReadAllText(Find(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"));
        Assert.Contains("@page \"/en/shop/orders\"", orders, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/shop/orders/order\"", orders, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantSqlPrefersShopDatabaseForWwwAlias()
    {
        var sql = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Data/PortalTenantSql.cs"));
        Assert.Contains("IFNULL(TRIM(`db_name`), '') <> ''", sql, StringComparison.Ordinal);
        Assert.Contains("`erp_only_shared` ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`erp_only_shared` DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void JourneyRecoverScriptExists()
    {
        Assert.True(File.Exists(Find("scripts/cloudpanel_EPARTSCART_CUSTOMER_JOURNEY_RECOVER.sh")));
        Assert.True(File.Exists(Find("docs/migration/evidence/storefront/epartscart-customer-journey-parity.json")));
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
