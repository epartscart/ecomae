namespace EcomAE.Platform.Presentation;

/// <summary>
/// Storefront href picker: PHP <c>/en/…</c> interim pages by default, or ASP.NET
/// <c>/storefront/*</c> apps when <see cref="PreferAspNetApps"/> is set
/// (temporary PHP serving deactivation for deep ASP.NET testing).
/// </summary>
public static class StorefrontSurfaceLinks
{
    /// <summary>
    /// Set at startup from <c>EcomAE:PhpReference:PreferAspNetStorefrontApps</c> (default true)
    /// or <c>TemporarilyDeactivatePhpServing</c>. When true, product chrome uses ASP.NET
    /// <c>/storefront/*-app</c> paths — not interim PHP <c>/en/*</c>. PHP compare stays on
    /// <c>/php-reference/*</c> only. Does not delete PHP source.
    /// </summary>
    public static bool PreferAspNetApps { get; set; }

    public static string PartSearch => PreferAspNetApps ? StorefrontAspNetCanonical.PartSearch : StorefrontPhpCanonical.PartSearch;
    public static string NameSearch => PreferAspNetApps ? StorefrontAspNetCanonical.NameSearch : StorefrontPhpCanonical.NameSearch;
    public static string WarehouseSearch => PreferAspNetApps ? StorefrontAspNetCanonical.WarehouseSearch : StorefrontPhpCanonical.WarehouseSearch;
    public static string LaximoVin => PreferAspNetApps ? StorefrontAspNetCanonical.LaximoVin : StorefrontPhpCanonical.LaximoVin;
    public static string VehicleCatalog => PreferAspNetApps ? StorefrontAspNetCanonical.VehicleCatalog : StorefrontPhpCanonical.VehicleCatalog;
    public static string ProductFamily => PreferAspNetApps ? StorefrontAspNetCanonical.ProductFamily : StorefrontPhpCanonical.ProductFamily;
    public static string AvailableBrands => PreferAspNetApps ? StorefrontAspNetCanonical.AvailableBrands : StorefrontPhpCanonical.AvailableBrands;
    public static string PartsInStock => PreferAspNetApps ? StorefrontAspNetCanonical.PartsInStock : StorefrontPhpCanonical.PartsInStock;
    public static string Accessories => PreferAspNetApps ? StorefrontAspNetCanonical.Accessories : StorefrontPhpCanonical.Accessories;
    public static string OwnCatalog => PreferAspNetApps ? StorefrontAspNetCanonical.OwnCatalog : StorefrontPhpCanonical.OwnCatalog;
    public static string EpartsCata => PreferAspNetApps ? StorefrontAspNetCanonical.EpartsCata : StorefrontPhpCanonical.EpartsCata;
    public static string EpartsMod => PreferAspNetApps ? StorefrontAspNetCanonical.EpartsMod : StorefrontPhpCanonical.EpartsMod;
    public static string PartsApiCatalog => PreferAspNetApps ? StorefrontAspNetCanonical.PartsApiCatalog : StorefrontPhpCanonical.PartsApiCatalog;
    public static string LevamOem => PreferAspNetApps ? StorefrontAspNetCanonical.LevamOem : StorefrontPhpCanonical.LevamOem;
    public static string UmapiCatalog => PreferAspNetApps ? StorefrontAspNetCanonical.UmapiCatalog : StorefrontPhpCanonical.UmapiCatalog;
    public static string UcatsService => PreferAspNetApps ? StorefrontAspNetCanonical.UcatsService : StorefrontPhpCanonical.UcatsService;
    public static string OriginalCatalog => PreferAspNetApps ? StorefrontAspNetCanonical.OriginalCatalog : StorefrontPhpCanonical.OriginalCatalog;
    public static string DemandIntelligence => PreferAspNetApps ? StorefrontAspNetCanonical.DemandIntelligence : StorefrontPhpCanonical.DemandIntelligence;
    public static string SellerRequest => PreferAspNetApps ? StorefrontAspNetCanonical.SellerRequest : StorefrontPhpCanonical.SellerRequest;
    public static string Cart => PreferAspNetApps ? StorefrontAspNetCanonical.Cart : StorefrontPhpCanonical.Cart;
    public static string Checkout => PreferAspNetApps ? StorefrontAspNetCanonical.Checkout : StorefrontPhpCanonical.Checkout;
    public static string Orders => PreferAspNetApps ? StorefrontAspNetCanonical.Orders : StorefrontPhpCanonical.Orders;
    public static string Login => PreferAspNetApps ? StorefrontAspNetCanonical.Login : StorefrontPhpCanonical.Login;
    public static string Registration => PreferAspNetApps ? StorefrontAspNetCanonical.Registration : StorefrontPhpCanonical.Registration;
    public static string GarageLogin => PreferAspNetApps ? StorefrontAspNetCanonical.GarageLogin : StorefrontPhpCanonical.GarageLogin;
    public static string CheckoutHowGet => PreferAspNetApps ? StorefrontAspNetCanonical.CheckoutHowGet : StorefrontPhpCanonical.CheckoutHowGet;
    public static string CheckoutConfirm => PreferAspNetApps ? StorefrontAspNetCanonical.CheckoutConfirm : StorefrontPhpCanonical.CheckoutConfirm;
    public static string CheckoutLoginOffer => PreferAspNetApps ? StorefrontAspNetCanonical.CheckoutLoginOffer : StorefrontPhpCanonical.CheckoutLoginOffer;
    public static string Quotes => PreferAspNetApps ? StorefrontAspNetCanonical.Quotes : StorefrontPhpCanonical.Quotes;
    public static string Wishlist => PreferAspNetApps ? StorefrontAspNetCanonical.Wishlist : StorefrontPhpCanonical.Wishlist;
    public static string Compare => PreferAspNetApps ? StorefrontAspNetCanonical.Compare : StorefrontPhpCanonical.Compare;
    public static string Product => PreferAspNetApps ? StorefrontAspNetCanonical.Product : StorefrontPhpCanonical.PartSearch;
    public static string Balance => PreferAspNetApps ? StorefrontAspNetCanonical.Balance : StorefrontPhpCanonical.Balance;
    public static string BulkUpload => PreferAspNetApps ? StorefrontAspNetCanonical.BulkUpload : StorefrontPhpCanonical.BulkUpload;

    public static string ForProduct(int productId)
        => PreferAspNetApps
            ? StorefrontAspNetCanonical.Product + "?id=" + productId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : StorefrontPhpCanonical.PartSearch;

    public static string ForQuote(int quoteId)
        => PreferAspNetApps
            ? StorefrontAspNetCanonical.Quotes + "?id=" + quoteId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : StorefrontPhpCanonical.Quotes + "?id=" + quoteId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string ForVin(string? identString)
    {
        var basePath = LaximoVin;
        if (string.IsNullOrWhiteSpace(identString))
        {
            return basePath;
        }

        var sep = basePath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return basePath + sep + "identString=" + Uri.EscapeDataString(identString.Trim());
    }

    public static string ForCatalogBrowse(string phpPath)
    {
        if (!PreferAspNetApps)
        {
            return StorefrontPhpCanonical.ForCatalogBrowse(phpPath);
        }

        // PreferAspNet must not collapse every catalog page onto Product Family —
        // Accessories is a dedicated marketplace (PHP /en/accessories-spare-parts).
        var path = (phpPath ?? string.Empty).Trim();
        var qIndex = path.IndexOf('?', StringComparison.Ordinal);
        var bare = (qIndex < 0 ? path : path[..qIndex]).TrimEnd('/');
        var query = qIndex < 0 ? "" : path[qIndex..];
        bare = StripLangPrefix(bare);

        return bare.ToLowerInvariant() switch
        {
            "/accessories-spare-parts" or "/accessories" => StorefrontAspNetCanonical.Accessories + query,
            "/available-brands" => StorefrontAspNetCanonical.AvailableBrands,
            "/parts" => StorefrontAspNetCanonical.PartsInStock,
            "/eparts-cata" => StorefrontAspNetCanonical.EpartsCata,
            "/eparts-mod" => StorefrontAspNetCanonical.EpartsMod,
            "/partsapi-catalog" => StorefrontAspNetCanonical.PartsApiCatalog,
            "/levam-oem" => StorefrontAspNetCanonical.LevamOem,
            "/umapi_catalog" => StorefrontAspNetCanonical.UmapiCatalog,
            "/original-catalog" => StorefrontAspNetCanonical.OriginalCatalog,
            "/demand-intelligence" => StorefrontAspNetCanonical.DemandIntelligence,
            "/zapros-prodavczu" => StorefrontAspNetCanonical.SellerRequest,
            "/product-family" => StorefrontAspNetCanonical.ProductFamily,
            _ when bare.Contains("katalogi-ucats", StringComparison.OrdinalIgnoreCase)
                => StorefrontAspNetCanonical.UcatsService,
            _ when bare.Contains("bulk-upload", StringComparison.OrdinalIgnoreCase)
                => StorefrontAspNetCanonical.BulkUpload,
            _ => StorefrontAspNetCanonical.ProductFamily,
        };
    }

    private static string StripLangPrefix(string path)
    {
        foreach (var lang in new[] { "/en", "/me", "/ru" })
        {
            if (path.Equals(lang, StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            if (path.StartsWith(lang + "/", StringComparison.OrdinalIgnoreCase))
            {
                return path[lang.Length..];
            }
        }

        return path.Length == 0 ? "/" : path;
    }

    public static string ForWarehouseSearch(string? originalWithQuery = null)
        => PreferAspNetApps
            ? AppendQuery(StorefrontAspNetCanonical.WarehouseSearch, originalWithQuery)
            : StorefrontPhpCanonical.ForWarehouseSearch(originalWithQuery);

    public static string ForPartSearch(string? originalWithQuery = null)
        => PreferAspNetApps
            ? AppendQuery(StorefrontAspNetCanonical.PartSearch, NormalizePartSearchQuery(originalWithQuery))
            : StorefrontPhpCanonical.ForPartSearch(originalWithQuery);

    /// <summary>Map PHP <c>brend</c> → ASP.NET <c>brand</c> in incoming query strings.</summary>
    private static string? NormalizePartSearchQuery(string? originalWithQuery)
    {
        if (string.IsNullOrWhiteSpace(originalWithQuery))
        {
            return originalWithQuery;
        }

        var qIndex = originalWithQuery.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0 || qIndex >= originalWithQuery.Length - 1)
        {
            return originalWithQuery;
        }

        var path = originalWithQuery[..(qIndex + 1)];
        var query = originalWithQuery[(qIndex + 1)..];
        if (!query.Contains("brend=", StringComparison.OrdinalIgnoreCase)
            || query.Contains("brand=", StringComparison.OrdinalIgnoreCase))
        {
            return originalWithQuery;
        }

        // Prefer brand= for ASP.NET search-app; keep other pairs intact.
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < pairs.Length; i++)
        {
            if (pairs[i].StartsWith("brend=", StringComparison.OrdinalIgnoreCase))
            {
                pairs[i] = "brand=" + pairs[i]["brend=".Length..];
            }
        }

        return path + string.Join('&', pairs);
    }

    public static string ForVinSearch(string? originalWithQuery = null)
        => PreferAspNetApps
            ? AppendQuery(StorefrontAspNetCanonical.LaximoVin, originalWithQuery)
            : StorefrontPhpCanonical.ForVinSearch(originalWithQuery);

    public static string ForVehicleCatalog(string? originalWithQuery = null)
        => PreferAspNetApps
            ? AppendQuery(StorefrontAspNetCanonical.VehicleCatalog, originalWithQuery)
            : StorefrontPhpCanonical.ForVehicleCatalog(originalWithQuery);

    public static string ForManufacturer(string manufacturer)
        => PreferAspNetApps
            ? StorefrontAspNetCanonical.ProductFamily + "?manufacturer=" + Uri.EscapeDataString(manufacturer)
            : StorefrontPhpCanonical.ForManufacturer(manufacturer);

    public static string ForOwnCatalogCategory(string? categoryUrl = null, int categoryId = 0)
    {
        var basePath = OwnCatalog;
        if (categoryId > 0)
        {
            return basePath + "?category_id=" + categoryId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var route = (categoryUrl ?? string.Empty).Trim().TrimStart('/');
        if (route.Length == 0)
        {
            return basePath;
        }

        return basePath + "?url=" + Uri.EscapeDataString(route);
    }

    public static string ForUmapiBrand(string brand)
        => PreferAspNetApps
            ? StorefrontAspNetCanonical.UmapiCatalog + "?brand=" + Uri.EscapeDataString(brand.Trim().ToLowerInvariant())
            : StorefrontPhpCanonical.ForUmapiBrand(brand);

    private static string AppendQuery(string basePath, string? original)
    {
        if (string.IsNullOrWhiteSpace(original))
        {
            return basePath;
        }

        var qIndex = original.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0 || qIndex >= original.Length - 1)
        {
            return basePath;
        }

        var hasQuery = basePath.Contains('?', StringComparison.Ordinal);
        return basePath + (hasQuery ? "&" : "?") + original[(qIndex + 1)..];
    }
}
