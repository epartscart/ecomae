using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards live ASP.NET cart add + customer-session preference (PHP ajax_add_to_basket / DP_User).
/// </summary>
public sealed class StorefrontCartAddLoginParityTests
{
    [Fact]
    public void ParityJs_PostsLiveCartAddAndParsesAuthErrors()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("/storefront/cart/add", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites: true", text, StringComparison.Ordinal);
        Assert.Contains("cartErrorMessage", text, StringComparison.Ordinal);
        Assert.Contains("articleShow", text, StringComparison.Ordinal);
        Assert.Contains("error.message", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Cart write is ASP.NET dry-run only", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_UsesCartAddCacheBustedParityScript()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("epc_warehouse_search_parity.js?v=20260812-cartadd", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_WiresLiveCartAddAndCustomerSession()
    {
        Assert.Equal("/storefront/cart/add", EcomAeRoutes.StorefrontCartAdd);
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("IStorefrontCartAddService", text, StringComparison.Ordinal);
        Assert.Contains("ValidateCustomerAsync", text, StringComparison.Ordinal);
        Assert.Contains("Please log in or register to continue.", text, StringComparison.Ordinal);
        Assert.Contains("body.ConfirmWrites", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CartApp_UsesValidateCustomerAsyncAndPhpLoginMessage()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor"));
        Assert.Contains("ValidateCustomerAsync", text, StringComparison.Ordinal);
        Assert.Contains("Please log in or register to continue.", text, StringComparison.Ordinal);
        Assert.Contains("Add to cart is live on ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PriceAccess_UsesValidateCustomerAsync()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Storefront/StorefrontPriceAccess.cs"));
        Assert.Contains("ValidateCustomerAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CartAddService_InsertsShopCartsType2()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Storefront/StorefrontCartAddService.cs"));
        Assert.Contains("INSERT INTO `shop_carts`", text, StringComparison.Ordinal);
        Assert.Contains("Please log in or register to continue.", text, StringComparison.Ordinal);
        Assert.Contains("session_id", text, StringComparison.Ordinal);
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
