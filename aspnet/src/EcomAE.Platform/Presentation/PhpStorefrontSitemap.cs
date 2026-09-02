namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer HTML sitemap twin for PHP <c>content/general_pages/sitemap.php</c>.
/// Links stay on ASP.NET storefront apps; industry extras are host-scoped.
/// </summary>
public static class PhpStorefrontSitemap
{
    public sealed record Entry(string Href, string Label, string Group);

    public static IReadOnlyList<Entry> ForIndustry(string? industryCode)
    {
        var industry = string.IsNullOrWhiteSpace(industryCode) ? "auto_parts" : industryCode.Trim().ToLowerInvariant();
        var rows = new List<Entry>
        {
            new("/", "Home", "Store"),
            new(StorefrontAspNetCanonical.Login, "Sign in", "Account"),
            new(StorefrontAspNetCanonical.Registration, "Register", "Account"),
            new(StorefrontAspNetCanonical.ForgotPassword, "Forgot password", "Account"),
            new(StorefrontAspNetCanonical.Balance, "Account / balance", "Account"),
            new(StorefrontAspNetCanonical.PricesDownload, "Price list", "Account"),
            new(StorefrontAspNetCanonical.Orders, "My orders", "Account"),
            new(StorefrontAspNetCanonical.GuestOrder, "Guest order", "Account"),
            new(StorefrontAspNetCanonical.CustomerReturns, "Returns", "Account"),
            new(StorefrontAspNetCanonical.CustomerRequests, "My requests", "Account"),
            new(StorefrontAspNetCanonical.Cart, "Cart", "Shop"),
            new(StorefrontAspNetCanonical.Checkout, "Checkout", "Shop"),
            new(StorefrontAspNetCanonical.Payment, "Pay", "Shop"),
            new(StorefrontAspNetCanonical.CustomerPrint, "Print documents", "Shop"),
            new(StorefrontAspNetCanonical.Quotes, "Quotes", "Shop"),
            new(StorefrontAspNetCanonical.Wishlist, "Bookmarks", "Shop"),
            new(StorefrontAspNetCanonical.Compare, "Compare", "Shop"),
            new(StorefrontAspNetCanonical.OwnCatalog, "Own catalog", "Catalog"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=kontakty", "Contact", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=o-dostavke", "Delivery", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=ob-oplate", "Payment", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=o-vozvrate", "Returns policy", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=o-kompanii", "About", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=chastye-voprosy", "Questions", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=kak-zakazat", "How to order", "Help"),
            new(StorefrontAspNetCanonical.IndustryCms + "?slug=garantii", "Warranty", "Help"),
            new(StorefrontAspNetCanonical.Offices, "Pickup points", "Help"),
            new(StorefrontAspNetCanonical.News, "News", "Help"),
            new(StorefrontAspNetCanonical.Newsletter, "Newsletter", "Help"),
            new(StorefrontAspNetCanonical.Brochure, "Brochure", "Help"),
        };

        if (industry is "auto_parts")
        {
            rows.AddRange(
            [
                new(StorefrontAspNetCanonical.PartSearch, "Part search", "Catalog"),
                new(StorefrontAspNetCanonical.VehicleCatalog, "Vehicle catalog", "Catalog"),
                new(StorefrontAspNetCanonical.LaximoVin, "VIN / Frame", "Catalog"),
                new(StorefrontAspNetCanonical.UcatsService, "Service catalogs", "Catalog"),
                new(StorefrontAspNetCanonical.Accessories, "Accessories", "Catalog"),
                new(StorefrontAspNetCanonical.SellerRequest, "Request to seller", "Help"),
                new(StorefrontAspNetCanonical.AiPartsExpert, "Parts expert", "Help"),
                new(StorefrontAspNetCanonical.SpecialSearch, "Special searches", "Catalog"),
                new(StorefrontAspNetCanonical.AutoWorkshop, "Auto workshop", "Service"),
                new(StorefrontAspNetCanonical.GarageLogin, "Garage", "Service"),
            ]);
            foreach (var search in PhpSpecialSearches.All)
            {
                rows.Add(new("/" + search.Alias, search.Title, "Special search"));
            }
        }
        else
        {
            foreach (var cat in PhpIndustryStorefrontCatalog.Roots(industry))
            {
                rows.Add(new("/" + cat.Url, cat.Name, "Catalog"));
            }
        }

        if (industry is "tax_advisory" or "consultancy")
        {
            rows.Add(new("/erp", "Client ERP", "Service"));
        }

        return rows;
    }
}
