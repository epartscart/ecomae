namespace EcomAE.Platform.Presentation;

/// <summary>
/// Live tenant storefront PHP canonical paths (lang-prefixed).
/// Interim while <c>/storefront/*</c> apps are not installed on tenant nginx or not dual-sample green.
/// epartscart serves full catalog/search UI under <c>/en/…</c>; bare paths often 404.
/// </summary>
public static class StorefrontPhpCanonical
{
    public const string LangPrefix = "/en";

    public const string PartSearch = LangPrefix + "/shop/part_search";
    public const string NameSearch = LangPrefix + "/shop/search";
    public const string WarehouseSearch = LangPrefix + "/shop/warehouse-search";
    public const string LaximoVin = LangPrefix + "/katalog-laximo";
    public const string VehicleCatalog = LangPrefix + "/vehicle-catalog";
    public const string ProductFamily = LangPrefix + "/product-family";
    public const string AvailableBrands = LangPrefix + "/available-brands";
    public const string PartsInStock = LangPrefix + "/parts";
    public const string Accessories = LangPrefix + "/accessories-spare-parts";
    public const string EpartsCata = LangPrefix + "/eparts-cata";
    public const string EpartsMod = LangPrefix + "/eparts-mod";
    public const string PartsApiCatalog = LangPrefix + "/partsapi-catalog";
    public const string LevamOem = LangPrefix + "/levam-oem";
    public const string UmapiCatalog = LangPrefix + "/umapi_catalog";
    public const string UcatsService = LangPrefix + "/shop/katalogi-ucats";
    public const string OriginalCatalog = LangPrefix + "/original-catalog";
    public const string DemandIntelligence = LangPrefix + "/demand-intelligence";
    public const string SellerRequest = LangPrefix + "/zapros-prodavczu";
    public const string Cart = LangPrefix + "/shop/cart";
    public const string Checkout = LangPrefix + "/shop/checkout";
    public const string Orders = LangPrefix + "/shop/orders";
    public const string Login = LangPrefix + "/users/login";
    public const string GarageLogin = LangPrefix + "/garage/login";

    /// <summary>Map thin ASP.NET <c>/storefront/*</c> stubs to working PHP pages.</summary>
    public static bool TryMapStorefrontStubToPhp(string? pathAndQuery, out string phpCanonical)
    {
        phpCanonical = "";
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return false;
        }

        var value = pathAndQuery.Trim();
        var qIndex = value.IndexOf('?', StringComparison.Ordinal);
        var path = (qIndex < 0 ? value : value[..qIndex]).TrimEnd('/');
        var query = qIndex < 0 ? "" : value[qIndex..];

        if (!path.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/storefront", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Home surface stays ASP.NET (classic-entry / → /storefront/app).
        if (path.Equals("/storefront/app", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/storefront", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Equals("/storefront/search-app", StringComparison.OrdinalIgnoreCase))
        {
            phpCanonical = SearchTargetFromQuery(query) + PreserveQueryExceptMode(query);
            return true;
        }

        phpCanonical = path.ToLowerInvariant() switch
        {
            "/storefront/cart-app" => Cart + query,
            "/storefront/checkout-app" => Checkout + query,
            "/storefront/orders-app" => Orders + query,
            "/storefront/login" => Login + query,
            "/storefront/garage-app" => GarageLogin + query,
            "/storefront/logout" => Login + query, // clear happens on ASP.NET if tree installed; else login
            "/storefront/profile-app" => Login + query,
            "/storefront/account-summary-app" => Orders + query,
            _ => "",
        };

        return phpCanonical.Length > 0;
    }

    public static string ForCatalogBrowse(string phpPath)
    {
        var path = phpPath.Trim();
        var qIndex = path.IndexOf('?', StringComparison.Ordinal);
        var bare = (qIndex < 0 ? path : path[..qIndex]).TrimEnd('/');
        var query = qIndex < 0 ? "" : path[qIndex..];
        bare = StripLang(bare);

        return bare.ToLowerInvariant() switch
        {
            "/product-family" => ProductFamily + query,
            "/available-brands" => AvailableBrands + query,
            "/parts" => PartsInStock + query,
            "/accessories-spare-parts" => Accessories + query,
            "/eparts-cata" => EpartsCata + query,
            "/eparts-mod" => EpartsMod + query,
            "/partsapi-catalog" => PartsApiCatalog + query,
            "/levam-oem" => LevamOem + query,
            "/umapi_catalog" => UmapiCatalog + query,
            "/original-catalog" => OriginalCatalog + query,
            "/demand-intelligence" => DemandIntelligence + query,
            "/zapros-prodavczu" => SellerRequest + query,
            _ when bare.Contains("katalogi-ucats", StringComparison.OrdinalIgnoreCase)
                => UcatsService + query,
            _ => ProductFamily + query,
        };
    }

    public static string ForWarehouseSearch(string? originalWithQuery = null)
        => AppendIncomingQuery(WarehouseSearch, originalWithQuery, dropMode: true);

    public static string ForPartSearch(string? originalWithQuery = null)
        => AppendIncomingQuery(PartSearch, originalWithQuery, dropMode: true);

    public static string ForVinSearch(string? originalWithQuery = null)
        => AppendIncomingQuery(LaximoVin, originalWithQuery, dropMode: true);

    public static string ForVehicleCatalog(string? originalWithQuery = null)
        => AppendIncomingQuery(VehicleCatalog, originalWithQuery, dropMode: true);

    /// <summary>PHP product-family deep link for a vehicle manufacturer tile.</summary>
    public static string ForManufacturer(string manufacturer)
        => ProductFamily + "?manufacturer=" + Uri.EscapeDataString(manufacturer);

    /// <summary>PHP umapi_catalog deep link for an aftermarket brand tile.</summary>
    public static string ForUmapiBrand(string brand)
        => UmapiCatalog + "?brand=" + Uri.EscapeDataString(brand.Trim().ToLowerInvariant());

    private static string SearchTargetFromQuery(string query)
    {
        var mode = GetQueryValue(query, "mode")?.ToLowerInvariant() ?? "";
        return mode switch
        {
            "attr" => WarehouseSearch,
            "vin" => LaximoVin,
            "car" => VehicleCatalog,
            "engine" => VehicleCatalog,
            "name" => NameSearch,
            _ => PartSearch,
        };
    }

    private static string PreserveQueryExceptMode(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return "";
        }

        var pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return pairs.Length == 0 ? "" : "?" + string.Join('&', pairs);
    }

    private static string AppendIncomingQuery(string basePath, string? original, bool dropMode)
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

        var incoming = original[(qIndex + 1)..];
        if (dropMode)
        {
            var pairs = incoming.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return pairs.Length == 0 ? basePath : basePath + "?" + string.Join('&', pairs);
        }

        return basePath + "?" + incoming;
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var k = eq < 0 ? pair : pair[..eq];
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return null;
    }

    private static string StripLang(string path)
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

        return path;
    }
}
