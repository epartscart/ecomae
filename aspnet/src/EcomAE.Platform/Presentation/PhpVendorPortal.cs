namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/shop/vendor/*</c> twins — storefront vendor portal (no CP).
/// Writes stay on the PHP compare archive.
/// </summary>
public static class PhpVendorPortal
{
    public static readonly string[] Emirates =
    [
        "Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah",
    ];

    public static readonly IReadOnlyList<(string Code, string Label)> LegalRegTypes =
    [
        ("TL", "Trade licence (TL)"),
        ("EID", "Emirates ID (EID)"),
        ("PAS", "Passport (PAS)"),
        ("CD", "Company document (CD)"),
    ];

    public static string AuthorityFor(string emirate) => emirate switch
    {
        "Dubai" => "Dubai Economy and Tourism",
        "Abu Dhabi" => "Abu Dhabi Department of Economic Development",
        "Sharjah" => "Sharjah Economic Development Department",
        "Ajman" => "Ajman Department of Economic Development",
        "Umm Al Quwain" => "Umm Al Quwain Department of Economic Development",
        "Ras Al Khaimah" => "Ras Al Khaimah Department of Economic Development",
        "Fujairah" => "Fujairah Municipality / Economic Development",
        _ => string.Empty,
    };

    public static string RegisterWriteHref => "/php-reference/en/vendor/register";
    public static string UploadWriteHref => "/php-reference/en/vendor/upload";
    public static string ForgotWriteHref => "/php-reference/en/users/forgot_password";
    public static string ConfirmWriteHref => "/php-reference/en/users/confirm_contact";
    public static string ReturnsWriteHref => "/storefront/returns/create";
    public static string BulkUploadWriteHref => "/php-reference/en/shop/bulk-upload";
    public static string NewsletterWriteHref => "/storefront/newsletter/subscribe";
    public static string ContactWriteHref => "/php-reference/en/kontakty";
}
