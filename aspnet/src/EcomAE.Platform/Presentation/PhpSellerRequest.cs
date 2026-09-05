namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/general_pages/vin_zapros.php</c> + <c>content/requests/*</c> twins.
/// users_vin INSERT and customer messages are ASP.NET-live. Captcha, photos, and manager email stay Classic.
/// </summary>
public static class PhpSellerRequest
{
    public static readonly IReadOnlyList<(string Name, string Label, bool Required, string Placeholder)> Fields =
    [
        ("client_fio", "Full name", true, "First and last name"),
        ("client_email", "Email", true, "name@example.com"),
        ("client_phone", "Phone", true, "+971 …"),
        ("client_vin", "VIN / frame", true, "17-character VIN or frame"),
        ("client_mark", "Make", false, "Make"),
        ("client_model", "Model", false, "Model"),
        ("client_year", "Year", false, "Year"),
        ("client_engine", "Engine", false, "Engine"),
        ("client_body", "Body", false, "Body type"),
        ("client_kpp", "Transmission", false, "AT / MT / robot"),
        ("client_city", "City", false, "City"),
        ("client_drive", "Drive", false, "FWD / RWD / 4WD"),
    ];

    public static readonly IReadOnlyList<(string Code, string Label)> PrintDocs =
    [
        ("sales_receipt", "Sales receipt"),
        ("invoice_for_payment", "Invoice for payment"),
    ];

    public static string SellerWriteHref => "/storefront/vin-request/create";
    public static string MessageWriteHref => "/storefront/vin-request/send-message";
    public static string PrintWriteHref => "/php-reference/content/shop/print_docs/service/print.php";

    public static string PrintHref(int orderId, string docName)
        => PrintWriteHref
           + "?doc_name=" + Uri.EscapeDataString(docName)
           + "&order_id=" + orderId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
