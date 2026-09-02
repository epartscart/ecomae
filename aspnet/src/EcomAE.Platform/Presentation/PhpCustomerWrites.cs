namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer write hrefs for storefront twins. PHP remains authoritative.
/// </summary>
public static class PhpCustomerWrites
{
    public static string ProfileWriteHref => "/php-reference/en/users/profile";
    public static string BalanceTopUpHref => "/php-reference/content/shop/finance/ajax_create_operation.php";
    public static string GarageCarWriteHref => "/php-reference/en/garazh/avtomobil";
    public static string GarageNotepadWriteHref => "/php-reference/en/garazh/bloknot";
    public static string OrderWriteHref => "/php-reference/en/shop/orders/order";
    public static string OrderMessageHref => "/php-reference/content/shop/messager/ajax_send_message.php";
    public static string GuestOrderWriteHref => "/php-reference/en/shop/orders/order";
    public static string PaymentDemoHref => "/php-reference/content/shop/finance/payment_systems/epc_demo/go_to_pay.php";

    public static readonly IReadOnlyList<(string Code, string Label)> PayGateways =
    [
        ("epc_demo", "Card / demo"),
        ("tabby", "Tabby"),
        ("tamara", "Tamara"),
        ("stripe", "Stripe"),
    ];

    public static string PaymentHref(string gateway, string operation)
        => "/php-reference/content/shop/finance/payment_systems/"
           + Uri.EscapeDataString(gateway)
           + "/go_to_pay.php?operation="
           + Uri.EscapeDataString(operation);
}
