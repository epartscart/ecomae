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

    /// <summary>Dedicated Product Family page (PHP twin: <c>/en/product-family</c>).</summary>
    public const string ProductFamily = "/storefront/product-family-app";
    /// <summary>Dedicated available-brands page (PHP twin: <c>/en/available-brands</c>).</summary>
    public const string AvailableBrands = "/storefront/available-brands-app";
    /// <summary>Brand-in-stock listing — same page as available brands (PHP <c>/en/parts</c>).</summary>
    public const string PartsInStock = "/storefront/available-brands-app";
    /// <summary>Accessories marketplace (PHP twin: <c>/en/accessories-spare-parts</c>).</summary>
    public const string Accessories = "/storefront/accessories-app";
    /// <summary>Tenant own catalogue (PHP Catalog of products mega menu / category browse).</summary>
    public const string OwnCatalog = "/storefront/own-catalog-app";
    public const string EpartsCata = "/storefront/eparts-cata-app";
    public const string EpartsMod = "/storefront/eparts-mod-app";
    public const string PartsApiCatalog = "/storefront/eparts-cata-app";
    public const string LevamOem = "/storefront/original-catalog-app";
    public const string UmapiCatalog = "/storefront/umapi-catalog-app";
    public const string UcatsService = "/storefront/ucats-app";
    public const string OriginalCatalog = "/storefront/original-catalog-app";
    public const string DemandIntelligence = "/storefront/demand-intelligence-app";
    /// <summary>Industry package category (PHP seed slugs: /gaming, /women, /gold, /services/tax).</summary>
    public const string IndustryCatalog = "/storefront/industry-catalog-app";
    /// <summary>CMS twins /kontakty /o-dostavke /ob-oplate /o-vozvrate.</summary>
    public const string IndustryCms = "/storefront/cms-page-app";
    public const string IndustrySearch = "/storefront/industry-search-app";
    public const string IndustryProduct = "/storefront/industry-product-app";
    public const string VendorPortal = "/storefront/vendor-app";
    public const string VendorRegister = "/storefront/vendor-register-app";
    public const string VendorUpload = "/storefront/vendor-upload-app";
    public const string ForgotPassword = "/storefront/forgot-password-app";
    public const string ConfirmContact = "/storefront/confirm-contact-app";
    public const string CustomerReturns = "/storefront/returns-app";
    /// <summary>Seller VIN / part request (PHP twin: <c>/en/zapros-prodavczu</c>).</summary>
    public const string SellerRequest = "/storefront/seller-request-app";
    /// <summary>Customer VIN-request inbox (PHP twin: <c>/en/requests</c>). Not CP system-requests.</summary>
    public const string CustomerRequests = "/storefront/customer-requests-app";
    /// <summary>Customer order print (PHP <c>content/shop/print_docs/service/print.php</c>).</summary>
    public const string CustomerPrint = "/storefront/print-app";
    public const string News = "/storefront/news-app";
    public const string GuestOrder = "/storefront/guest-order-app";
    public const string Payment = "/storefront/payment-app";
    public const string Sitemap = "/storefront/sitemap-app";
    public const string Brochure = "/storefront/brochure-app";
    public const string Cart = "/storefront/cart-app";
    public const string Checkout = "/storefront/checkout-app";
    public const string Orders = "/storefront/orders-app";
    public const string Login = "/storefront/login";
    /// <summary>Customer registration (PHP twin: <c>/en/users/registration</c>).</summary>
    public const string Registration = "/storefront/register-app";
    public const string GarageLogin = "/storefront/garage-app";
    public const string AutoWorkshop = "/storefront/auto-workshop-app";
    public const string GarageManager = "/storefront/garage-manager-app";
    public const string Newsletter = "/storefront/newsletter-app";
    /// <summary>Checkout delivery/pickup step (PHP <c>/en/shop/checkout/how_get</c>).</summary>
    public const string CheckoutHowGet = "/storefront/checkout-app?step=how_get";
    /// <summary>Checkout confirm step (PHP <c>/en/shop/checkout/confirm</c>).</summary>
    public const string CheckoutConfirm = "/storefront/checkout-app?step=confirm";
    /// <summary>Guest checkout offer (PHP <c>/en/shop/checkout/login_offer</c>).</summary>
    public const string CheckoutLoginOffer = "/storefront/checkout-app?step=login_offer";
    /// <summary>Order detail (PHP <c>/en/shop/orders/order</c>).</summary>
    public const string OrderDetail = "/storefront/orders-app";
}
