namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>prices_upload_guide.php</c> / <c>prices_manager.php</c> upload channels
/// for ePartsCart CP. Writes stay on PHP; this catalogue drives the ASP.NET console.
/// </summary>
public static class CpPricesUploadWaysCatalog
{
    public sealed record Way(
        string Key,
        string Label,
        string Mode,
        string Href,
        string Icon,
        string Tone,
        string Hint);

    public static string LoadModeLabel(int loadMode) => loadMode switch
    {
        2 => "FTP",
        3 => "E-mail",
        4 => "URL",
        _ => "PC file",
    };

    public static IReadOnlyList<Way> All { get; } =
    [
        new("pc", "PC file", "1", "/cp/prices-upload-app?way=pc", "fa-desktop", "pc",
            "Upload CSV / Excel / ZIP on a list (pyprices from PC)."),
        new("ftp", "FTP", "2", "/cp/prices-upload-app?way=ftp", "fa-server", "ftp",
            "Pull the supplier file from FTP (host / folder / substring)."),
        new("email", "E-mail", "3", "/cp/prices-upload-app?way=email", "fa-envelope", "email",
            "IMAP: one attachment → one list (sender / subject / filename)."),
        new("url", "URL / link", "4", "/cp/prices-upload-app?way=url", "fa-link", "url",
            "Download the price file from a configured URL."),
        new("cron", "Scheduled cron", "auto", "/cp/prices-upload-app?way=cron", "fa-clock-o", "cron",
            "Auto FTP / e-mail / URL jobs from the price-list manager."),
        new("wizard", "Classic wizard", "wizard", "/cp/shop/prices/upload", "fa-magic", "wizard",
            "PHP ajax_1→7 pipeline for one configured list."),
        new("multivendor", "Multi-vendor", "mv", "/cp/shop/prices/multivendor", "fa-sitemap", "mv",
            "One Excel/CSV → warehouse + price list per vendor."),
        new("vendor", "Vendor portal", "vendor", "/vendor/upload", "fa-truck", "vendor",
            "Approved vendors upload from the storefront (not CP)."),
        new("review", "Price review", "review", "/cp/shop/prices/review", "fa-check-square-o", "review",
            "Adjust rows after import. Writes stay PHP."),
        new("edit", "Manual grid edit", "edit", "/cp/prices-edit-app", "fa-pencil", "edit",
            "Edit warehouse rows after upload. Not a file ingest."),
        new("api", "Deploy / Treelax API", "api", "/cp/prices-upload-app?way=api", "fa-plug", "api",
            "Automation POST (tech_key + file). No CP file picker."),
    ];

    public static string EditListHref(long priceId)
        => $"/cp/shop/prices/price?price_id={priceId}";

    public static string WizardHref(long priceId)
        => $"/cp/shop/prices/upload?price_id={priceId}";

    public static string ReviewHref(long priceId)
        => $"/cp/shop/prices/review?price_id={priceId}";

    public static string PhpUploadFileAction
        => "/php-reference/cp/content/shop/prices_upload/for_pyprices/upload_file.php";
}
