namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/shop/workshop/*</c> and <c>content/general_pages/epc_autoworkshop_storefront_page.php</c>
/// twins. Book/track writes stay on the PHP compare archive.
/// </summary>
public static class PhpWorkshopPortal
{
    public static readonly string[] BoardColumns =
    [
        "checkin", "estimate", "approved", "in_progress", "qc", "ready",
    ];

    public static string BookWriteHref => "/php-reference/en/auto-workshop";
    public static string TrackWriteHref => "/php-reference/en/auto-workshop";
    public static string ManagerWriteHref => "/php-reference/en/garage/manager";
}
