namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer write hrefs for storefront twins. Cart / checkout / quote / garage / reviews / wishlist / compare / profile UPSERT are ASP.NET-live.
/// </summary>
public static class PhpCustomerWrites
{
    public static string ProfileWriteHref => "/storefront/profile/save";
    public static string ProfilePasswordHref => "/storefront/profile/change-password";
    public static string GarageCheckCarHref => "/storefront/garage/check-car";
    public static string BalanceTopUpHref => "/storefront/payment/create-operation";
    public static string GarageCarWriteHref => "/storefront/garage/save";
    public static string GarageNotepadWriteHref => "/storefront/garage/notepad-add";
    public static string OrderWriteHref => "/php-reference/en/shop/orders/order";
    public static string OrderMessageHref => "/storefront/orders/send-message";
    public static string GuestOrderWriteHref => "/php-reference/en/shop/orders/order";
    public static string PaymentDemoHref => "/storefront/payment/go-to-pay";
    public static string CartAddHref => "/storefront/cart/add";
    public static string QuoteAddHref => "/storefront/quotes/add-item";
    public static string QuotesAcceptHref => "/storefront/quotes/accept";
    public static string QuotesAddManualHref => "/storefront/quotes/add-manual";
    public static string CheckoutHowGetWriteHref => "/storefront/checkout-app?step=how_get";
    public static string CheckoutConfirmWriteHref => "/storefront/checkout/create";
    public static string ReturnsMessageHref => "/storefront/returns/send-message";
    public static string ReturnsCreateHref => "/storefront/returns/create";
    public static string QuotesWriteHref => "/storefront/quotes/submit";
    public static string WishlistWriteHref => "/storefront/wishlist/add";
    public static string WishlistRemoveHref => "/storefront/wishlist/remove";
    public static string CompareWriteHref => "/storefront/compare/add";
    public static string CompareRemoveHref => "/storefront/compare/remove";
    public static string EvaluationWriteHref => "/storefront/evaluations/add";

    public static readonly IReadOnlyList<(string Code, string Label)> ObtainModes =
    [
        ("1", "Collect from warehouse"),
        ("2", "Courier delivery"),
    ];

    public static readonly IReadOnlyList<(string Code, string Label)> PayGateways =
    [
        ("epc_demo", "Card / demo"),
        ("tabby", "Tabby"),
        ("tamara", "Tamara"),
        ("stripe", "Stripe"),
    ];

    public static string PaymentHref(string gateway, string operation)
        => "/storefront/payment/go-to-pay?pay_system="
           + Uri.EscapeDataString(gateway)
           + "&operation="
           + Uri.EscapeDataString(operation);
}
