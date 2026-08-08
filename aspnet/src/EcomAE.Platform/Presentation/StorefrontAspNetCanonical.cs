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
    public const string LaximoVin = "/storefront/vin-app";
    public const string VehicleCatalog = "/storefront/vehicle-catalog-app";
    public const string Quotes = "/storefront/quotes-app";
    public const string Wishlist = "/storefront/wishlist-app";
    public const string Compare = "/storefront/compare-app";
    public const string Product = "/storefront/product-app";
    public const string BulkUpload = "/storefront/bulk-upload-app";
    public const string Balance = "/storefront/account-summary-app";

    // Home carries the full PHP catalog widgets — deep-link to the matching section
    // anchor instead of the bare home URL (bare /storefront/app made every catalog
    // click look like "nothing happened").
    public const string ProductFamily = "/storefront/app#epc-product-family";
    public const string AvailableBrands = "/storefront/app#epc-brands";
    public const string PartsInStock = "/storefront/app#epc-brands";
    /// <summary>Accessories marketplace (PHP twin: <c>/en/accessories-spare-parts</c>).</summary>
    public const string Accessories = "/storefront/accessories-app";
    /// <summary>Tenant own catalogue (PHP Catalog of products mega menu / category browse).</summary>
    public const string OwnCatalog = "/storefront/own-catalog-app";
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
    /// <summary>Customer registration (PHP twin: <c>/en/users/registration</c>).</summary>
    public const string Registration = "/storefront/register-app";
    public const string GarageLogin = "/storefront/garage-app";
    /// <summary>Checkout delivery/pickup step (PHP <c>/en/shop/checkout/how_get</c>).</summary>
    public const string CheckoutHowGet = "/storefront/checkout-app?step=how_get";
    /// <summary>Checkout confirm step (PHP <c>/en/shop/checkout/confirm</c>).</summary>
    public const string CheckoutConfirm = "/storefront/checkout-app?step=confirm";
    /// <summary>Guest checkout offer (PHP <c>/en/shop/checkout/login_offer</c>).</summary>
    public const string CheckoutLoginOffer = "/storefront/checkout-app?step=login_offer";
    /// <summary>Order detail (PHP <c>/en/shop/orders/order</c>).</summary>
    public const string OrderDetail = "/storefront/orders-app";
}
