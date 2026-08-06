namespace EcomAE.Platform.Presentation;

/// <summary>
/// ASP.NET storefront app paths used when PHP serving is temporarily deactivated
/// for deep ASP.NET testing (<see cref="Configuration.PhpReferenceOptions.TemporarilyDeactivatePhpServing"/>).
/// Does not delete PHP source and does not flip cutover locks.
/// </summary>
public static class StorefrontAspNetCanonical
{
    public const string PartSearch = "/storefront/search-app";
    public const string NameSearch = "/storefront/search-app?mode=name";
    public const string WarehouseSearch = "/storefront/search-app?mode=attr";
    public const string LaximoVin = "/storefront/search-app?mode=vin";
    public const string VehicleCatalog = "/storefront/search-app?mode=car";

    // Home carries the full PHP catalog widgets — deep-link to the matching section
    // anchor instead of the bare home URL (bare /storefront/app made every catalog
    // click look like "nothing happened").
    public const string ProductFamily = "/storefront/app#epc-product-family";
    public const string AvailableBrands = "/storefront/app#epc-brands";
    public const string PartsInStock = "/storefront/app#epc-brands";
    public const string Accessories = "/storefront/app#epc-product-family";
    public const string EpartsCata = "/storefront/app#epc-umapi";
    public const string EpartsMod = "/storefront/app#epc-umapi";
    public const string PartsApiCatalog = "/storefront/app#epc-umapi";
    public const string LevamOem = "/storefront/app#epc-umapi";
    public const string UmapiCatalog = "/storefront/app#epc-umapi";
    public const string UcatsService = "/storefront/app#epc-umapi";
    public const string OriginalCatalog = "/storefront/app#epc-vehicle-catalog";
    public const string DemandIntelligence = "/storefront/app#epc-umapi";
    public const string SellerRequest = "/storefront/search-app?mode=vin";
    public const string Cart = "/storefront/cart-app";
    public const string Checkout = "/storefront/checkout-app";
    public const string Orders = "/storefront/orders-app";
    public const string Login = "/storefront/login";
    public const string GarageLogin = "/storefront/garage-app";
}
