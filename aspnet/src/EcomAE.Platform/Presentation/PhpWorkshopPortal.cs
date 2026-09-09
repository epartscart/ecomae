namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/shop/workshop/*</c> and <c>content/general_pages/epc_autoworkshop_storefront_page.php</c>
/// twins. Book INSERT is ASP.NET-live. Schema-ensure stays Classic.
/// </summary>
public static class PhpWorkshopPortal
{
    public static readonly string[] BoardColumns =
    [
        "checkin", "estimate", "approved", "in_progress", "qc", "ready",
    ];

    public static string BookWriteHref => "/storefront/workshop/book";
    public static string TrackWriteHref => "/storefront/auto-workshop-app";
    public static string ManagerWriteHref => "/storefront/workshop/appointment";
}
