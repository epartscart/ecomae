using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontGuestCheckoutParityTests
{
    [Fact]
    public void Guest_contact_helpers_match_php_empty_and_invalid_rules()
    {
        Assert.True(StorefrontGuestSessionService.GuestEmailLooksValid(null));
        Assert.True(StorefrontGuestSessionService.GuestEmailLooksValid(""));
        Assert.True(StorefrontGuestSessionService.GuestEmailLooksValid("ops@local.test"));
        Assert.False(StorefrontGuestSessionService.GuestEmailLooksValid("not-an-email"));
        Assert.False(StorefrontGuestSessionService.GuestPhoneHasDigits(""));
        Assert.False(StorefrontGuestSessionService.GuestPhoneHasDigits("12345"));
        Assert.True(StorefrontGuestSessionService.GuestPhoneHasDigits("+971 50 123 4567"));
    }

    [Fact]
    public void Contact_regexp_requires_full_match_like_php()
    {
        Assert.True(StorefrontGuestSessionService.ContactMatchesRegexp("abc", null));
        Assert.True(StorefrontGuestSessionService.ContactMatchesRegexp("abc", ""));
        Assert.True(StorefrontGuestSessionService.ContactMatchesRegexp("abc", "^[a-z]+$"));
        Assert.False(StorefrontGuestSessionService.ContactMatchesRegexp("abc!", "^[a-z]+$"));
        Assert.False(StorefrontGuestSessionService.ContactMatchesRegexp("xabc", "abc"));
    }

    [Fact]
    public void Html_entities_escape_guest_contacts()
    {
        Assert.Equal("a &amp; b", StorefrontGuestSessionService.HtmlEntities("a & b"));
        Assert.Equal("&lt;script&gt;", StorefrontGuestSessionService.HtmlEntities("<script>"));
    }

    [Fact]
    public void Checkout_and_cart_module_accept_guest_session()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("IStorefrontGuestSessionService", module, StringComparison.Ordinal);
        Assert.Contains("phone_not_auth", module, StringComparison.Ordinal);
        Assert.Contains("ApplyCheckoutCookies", module, StringComparison.Ordinal);
        Assert.Contains("AppendProductsInCartCookie", module, StringComparison.Ordinal);

        var checkout = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Storefront/StorefrontCheckoutWriteService.cs"));
        Assert.Contains("`session_id` = ?", checkout, StringComparison.Ordinal);
        Assert.Contains("phone_not_auth", checkout, StringComparison.Ordinal);
        Assert.Contains("phone_required", checkout, StringComparison.Ordinal);

        var cartApp = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor"));
        Assert.Contains("Please log in or register to continue.", cartApp, StringComparison.Ordinal);
        Assert.Contains("ValidateCustomerAsync", cartApp, StringComparison.Ordinal);
        Assert.Contains("IStorefrontGuestSessionService", cartApp, StringComparison.Ordinal);
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
