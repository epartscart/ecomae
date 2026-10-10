namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-leech CP SSL checker. Path kept for the inventory:
/// <c>cp/modules/check_ssl/check_ssl.php</c>.
/// GET never mints a session cookie. Leftover top-alert parents stay injected.
/// PHP concatenates the probe digit with the redirect digit (<c>1</c>/<c>2</c>).
/// </summary>
public static class PhpPlanQ1Leech
{
    public const string CheckSslPath = "cp/modules/check_ssl/check_ssl.php";

    public static Func<string, bool>? ProbeOk { get; set; }
    public static Func<bool>? RedirectConfigured { get; set; }
    public static Func<int, string>? Translate { get; set; }
    public static Action<Dictionary<string, object?>>? RenderItem { get; set; }

    public static void Reset()
    {
        ProbeOk = _ => false;
        RedirectConfigured = () => false;
        Translate = id => "t" + id;
        RenderItem = _ => { };
    }

    public static Dictionary<string, object?> EpcCheckSslEvaluate(string domainPath)
    {
        var host = ParseHost(domainPath);
        var sslDigit = ProbeOk != null && ProbeOk(host) ? "1" : "2";
        var redirectDigit = RedirectConfigured != null && RedirectConfigured() ? "1" : "2";
        var state = sslDigit + redirectDigit;
        var text = "";
        var style = "";
        var sign = "";
        switch (state)
        {
            case "11":
                text = T(5585);
                break;
            case "12":
                text = T(5586);
                style = "background-color:#f3fa37;color:#000;";
                sign = "<i class=\"pe-7s-info\"></i>";
                break;
            case "21":
                text = T(5587);
                style = "background-color:#f3fa37;color:#000;";
                sign = "<i class=\"pe-7s-info\"></i>";
                break;
            case "22":
                text = T(5588);
                style = "background-color:#F00;color:#FFF;";
                sign = "<i class=\"pe-7s-attention\"></i>";
                break;
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["state"] = state,
            ["text"] = text,
            ["style"] = style,
            ["sign"] = sign,
            ["host"] = host
        };
        RenderItem?.Invoke(row);
        return row;
    }

    private static string T(int id) => Translate != null ? Translate(id) : "t" + id;

    private static string ParseHost(string domainPath)
    {
        if (!Uri.TryCreate(domainPath, UriKind.Absolute, out var uri))
        {
            return "";
        }

        return uri.Host;
    }
}
