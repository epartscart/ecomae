using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP twin: content/shop/docpart/epc_storefront_prices_helpers.php
/// epartscart guests + pending wholesale must not see prices.
/// </summary>
public sealed class StorefrontGuestPriceVisibilityTests
{
    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    [Theory]
    [InlineData("epartscart", "www.epartscart.com", true)]
    [InlineData("epartscart", "epartscart.com", true)]
    [InlineData(null, "www.epartscart.com", true)]
    [InlineData("platform", "www.ecomae.com", false)]
    public void HideForGuests_MatchesEpartscartPhpFallback(string? siteKey, string host, bool expected)
    {
        TenantContext? tenant = siteKey is null
            ? null
            : TenantContext.ForKnownTenant(siteKey, host, TenantMode.LiveTenant, TenantSurface.Storefront, "/");
        Assert.Equal(expected, StorefrontPriceAccess.HideStorefrontPricesForGuests(tenant, host));
    }

    [Fact]
    public void RedactOffers_ZerosPriceAndMasksWarehouseFields()
    {
        var access = new StorefrontPriceAccess(new NoopSessionValidator(), new NoopConnections());
        var rows = new List<StorefrontPartOfferDigest>
        {
            new(1, "List A", "BOSCH", "OC90", "OC90", "Filter", 42.50m, 5, "Dubai WH", "1d"),
        };
        var redacted = access.RedactOffers(rows);
        Assert.Single(redacted);
        Assert.Equal(0m, redacted[0].Price);
        Assert.Equal(1, redacted[0].Exist);
        Assert.Equal(string.Empty, redacted[0].Storage);
        Assert.Equal(string.Empty, redacted[0].TimeToExe);
        Assert.Equal(string.Empty, redacted[0].PriceList);
        Assert.Equal("BOSCH", redacted[0].Manufacturer);
    }

    [Fact]
    public void SearchApp_HonorsPriceVisibilityGate()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor");
        Assert.Contains("IStorefrontPriceAccess", text, StringComparison.Ordinal);
        Assert.Contains("data-prices-visible", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-price-gate", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPriceAccess.SensitiveMask", text, StringComparison.Ordinal);
        Assert.Contains("prices_visible === false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_AppliesPriceGateOnSearchAndPoll()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs");
        Assert.Contains("IStorefrontPriceAccess", text, StringComparison.Ordinal);
        Assert.Contains("prices_visible = access.PricesVisible", text, StringComparison.Ordinal);
        Assert.Contains("RedactOffers", text, StringComparison.Ordinal);
        Assert.Contains("access_state", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeWidgets_PassPricesVisibleFlag()
    {
        var brands = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEpartFrontSections.razor");
        Assert.Contains("AvailableBrands(pricesVisible)", brands, StringComparison.Ordinal);
        var depth = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontHomeDepth.razor");
        Assert.Contains("StorefrontPriceAccess.SensitiveMask", depth, StringComparison.Ordinal);
        var html = PhpHomeWidgetHtml.Substitute(
            "<?php echo $epc_brands_prices_visible ? '1' : '0'; ?>",
            "/en",
            pricesVisible: false);
        Assert.Equal("0", html);
        html = PhpHomeWidgetHtml.Substitute(
            "<?php echo $epc_brands_prices_visible ? '1' : '0'; ?>",
            "/en",
            pricesVisible: true);
        Assert.Equal("1", html);
    }

    [Fact]
    public void PriceAccessService_IsRegisteredInProgram()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Program.cs");
        Assert.Contains("IStorefrontPriceAccess", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPriceAccess", text, StringComparison.Ordinal);
    }

    private sealed class NoopSessionValidator : Auth.ILegacySessionValidator
    {
        public ValueTask<Auth.LegacySessionContext> ValidateAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new Auth.LegacySessionContext(Auth.LegacySessionKind.Anonymous, 0, null, []));
    }

    private sealed class NoopConnections : Data.ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<System.Data.Common.DbConnection> OpenAsync(
            string? databaseName,
            string? userName,
            string? password,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(
            TenantContext? tenant,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
