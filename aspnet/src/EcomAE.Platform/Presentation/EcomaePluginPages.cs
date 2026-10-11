namespace EcomAE.Platform.Presentation;

/// <summary>
/// Storefront/CP plugins still owned by the existing chrome.
/// Live behaviour stays on the existing importer or Blazor desk.
/// </summary>
public static class EcomaePluginPages
{
    public static readonly string[] Paths =
    [
        "plugins/error_pages/error_pages.php",
        "plugins/metadata_handler/metadata_handler.php",
        "plugins/phone_tablet/phone_tablet.php",
        "plugins/shop/cart/cart_handler.php",
    ];
}
