using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-ford BOC product-brochure launcher.
/// PHP identifiers kept for the inventory: <c>epc_cp_page_frame_open</c>, <c>epc_cp_page_frame_close</c>.
/// Path: <c>cp/content/control/portal/epc_boc_product_brochure.php</c>.
/// GET never mints a session cookie. Leftover unique portal and Super-CP host check stay injected
/// and are not named here.
/// </summary>
public static class PhpPlanQ1Ford
{
    public const string BocProductBrochurePath = "cp/content/control/portal/epc_boc_product_brochure.php";
    public const string Denied =
        "<div class=\"alert alert-warning\">Product brochure (platform) is available on Super CP only. Tenant operators: use <a href=\"/brochure\" target=\"_blank\" rel=\"noopener\">/brochure</a> or <a href=\"/brochure-cp\" target=\"_blank\" rel=\"noopener\">/brochure-cp</a>.</div>";

    public static Func<bool>? IsSuperCp { get; set; }
    public static object? BackendDir { get; set; }
    public static bool FrameFileExists { get; set; }
    public static bool HasFrameOpen { get; set; }
    public static bool HasFrameClose { get; set; }
    public static string LastCpBrochure { get; private set; } = "";
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsSuperCp = null;
        BackendDir = null;
        FrameFileExists = false;
        HasFrameOpen = false;
        HasFrameClose = false;
        LastCpBrochure = "";
        LastOutput = "";
    }

    public static void EpcCpPageFrameOpen(Dictionary<string, object?> opts)
        => LastOutput += "FRAME_OPEN:" + Str(opts.GetValueOrDefault("class"));

    public static void EpcCpPageFrameClose()
        => LastOutput += "FRAME_CLOSE";

    public static string EpcBocProductBrochure()
    {
        if (IsSuperCp?.Invoke() != true)
        {
            LastCpBrochure = "";
            LastOutput = Denied;
            return LastOutput;
        }

        var backend = Str(BackendDir ?? "cp").Trim('/');
        LastCpBrochure = "/" + backend + "/control/cp_brochure";
        LastOutput = "";
        if (FrameFileExists && HasFrameOpen)
        {
            EpcCpPageFrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal) { ["class"] = "epc-boc-brochure" });
        }

        LastOutput += Deck(LastCpBrochure);
        if (HasFrameClose)
        {
            EpcCpPageFrameClose();
        }

        return LastOutput;
    }

    public static string Deck(string cpBrochure)
    {
        var href = H(cpBrochure);
        return
            "<div class=\"epc-portal-settings\">\n" +
            "\t<div class=\"hero\">\n" +
            "\t\t<h2><i class=\"fa fa-file-text-o\"></i> Product brochure</h2>\n" +
            "\t\t<p style=\"margin:0;opacity:.92\">Share the graphical marketing brochure with prospects, or open the full live CP function deck for demos and training.</p>\n" +
            "\t</div>\n" +
            "\t<div class=\"hpanel\">\n" +
            "\t\t<div class=\"panel-heading\"><h4>Open brochure</h4></div>\n" +
            "\t\t<div class=\"panel-body\">\n" +
            "\t\t\t<p>\n" +
            "\t\t\t\t<a class=\"btn btn-primary\" href=\"/brochure\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-file-text-o\"></i> Product brochure</a>\n" +
            "\t\t\t\t<a class=\"btn btn-default\" href=\"" + href + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-book\"></i> Full CP brochure</a>\n" +
            "\t\t\t\t<a class=\"btn btn-default\" href=\"/brochure/cp\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-external-link\"></i> Public CP brochure URL</a>\n" +
            "\t\t\t</p>\n" +
            "\t\t\t<p class=\"text-muted\" style=\"margin:0\">Public URLs: <code>/brochure</code> · <code>/brochure/cp</code> · In-CP: <code>" + href + "</code></p>\n" +
            "\t\t</div>\n" +
            "\t</div>\n" +
            "</div>\n";
    }

    private static string H(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
