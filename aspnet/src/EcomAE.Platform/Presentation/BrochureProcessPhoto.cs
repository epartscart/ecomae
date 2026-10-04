using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/general_pages/epc_brochure_process_photo.php</c>.
/// Topic illustration SVG. The brochure snapshot requests this URL for every process card.
/// </summary>
public static class BrochureProcessPhoto
{
    public const string PhpPath = "/content/general_pages/epc_brochure_process_photo.php";
    public const string AssetPath = "/platform-assets/brochure-process.svg";

    public static IResult Serve(HttpContext context)
    {
        var svg = Build(
            context.Request.Query["topic"],
            context.Request.Query["t"],
            context.Request.Query["a"],
            context.Request.Query["seed"]);
        context.Response.Headers.CacheControl = "public, max-age=86400";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Text(svg, "image/svg+xml; charset=utf-8");
    }

    public static string Build(string? topic, string? title, string? area, string? seedKey)
    {
        var cleanTitle = MbLimit((title ?? string.Empty).Trim(), 52);
        if (cleanTitle.Length == 0)
        {
            cleanTitle = "Process";
        }

        var cleanArea = MbLimit((area ?? string.Empty).Trim(), 40);
        var cleanTopic = new string((topic ?? "platform").Trim().ToLowerInvariant().Where(static ch => ch is >= 'a' and <= 'z').ToArray());
        if (cleanTopic.Length == 0)
        {
            cleanTopic = "platform";
        }

        if (cleanTopic == "shipping")
        {
            cleanTopic = "logistics";
        }
        else if (cleanTopic == "suppliers")
        {
            cleanTopic = "procurement";
        }
        else if (cleanTopic == "operations")
        {
            cleanTopic = "default";
        }

        var palette = PaletteOf(cleanTopic);
        var hash = PhpCrc32((string.IsNullOrEmpty(seedKey) ? cleanTitle : seedKey.Trim()) + "|" + cleanTopic + "|" + cleanArea);
        var seed = (int)(hash % 97);
        var scene = Scene(cleanTopic, palette, seed);
        var caption = cleanArea.Length == 0 ? palette.Label : cleanArea;
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"960\" height=\"540\" viewBox=\"0 0 960 540\" role=\"img\" aria-label=\"").Append(Xml(cleanTitle)).Append("\">");
        sb.Append("<defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
        sb.Append("<stop offset=\"0%\" stop-color=\"").Append(palette.Bg).Append("\"/>");
        sb.Append("<stop offset=\"100%\" stop-color=\"").Append(palette.Panel).Append("\"/>");
        sb.Append("</linearGradient></defs>");
        sb.Append("<rect width=\"960\" height=\"540\" fill=\"url(#g)\"/>");
        sb.Append("<circle cx=\"").Append(100 + (seed % 60)).Append("\" cy=\"").Append(80 + (seed % 40)).Append("\" r=\"180\" fill=\"").Append(palette.Accent).Append("\" opacity=\".12\"/>");
        sb.Append("<circle cx=\"").Append(820 - (seed % 50)).Append("\" cy=\"420\" r=\"200\" fill=\"").Append(palette.Accent2).Append("\" opacity=\".1\"/>");
        sb.Append(scene);
        sb.Append("<rect x=\"0\" y=\"400\" width=\"960\" height=\"140\" fill=\"#000\" opacity=\".55\"/>");
        sb.Append("<text x=\"40\" y=\"440\" fill=\"").Append(palette.Accent2).Append("\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\" font-size=\"14\" font-weight=\"700\" letter-spacing=\"2\">").Append(Xml(palette.Label.ToUpperInvariant())).Append("</text>");
        sb.Append("<text x=\"40\" y=\"478\" fill=\"#ffffff\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\" font-size=\"28\" font-weight=\"800\">").Append(Xml(cleanTitle)).Append("</text>");
        sb.Append("<text x=\"40\" y=\"508\" fill=\"#ffffff\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\" font-size=\"13\" opacity=\".65\">").Append(Xml(caption)).Append(" · #").Append((hash % 10000).ToString(CultureInfo.InvariantCulture)).Append("</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>PHP <c>crc32</c> on the UTF-8 bytes, as an unsigned 32-bit value.</summary>
    public static uint PhpCrc32(string value)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                var mask = (uint)-(int)(crc & 1);
                crc = (crc >> 1) ^ (0xEDB88320u & mask);
            }
        }

        return ~crc;
    }

    private readonly record struct Palette(string Bg, string Panel, string Accent, string Accent2, string Label);

    private static Palette PaletteOf(string topic) => topic switch
    {
        "autoparts" => new("#1c1917", "#292524", "#f97316", "#fdba74", "Auto parts"),
        "inventory" => new("#0f172a", "#1e293b", "#0ea5e9", "#7dd3fc", "Inventory"),
        "money" => new("#14532d", "#166534", "#22c55e", "#bbf7d0", "Money"),
        "orders" => new("#7c2d12", "#9a3412", "#f97316", "#fed7aa", "Orders"),
        "logistics" => new("#164e63", "#155e75", "#06b6d4", "#a5f3fc", "Logistics"),
        "customers" => new("#4c1d95", "#5b21b6", "#a78bfa", "#ddd6fe", "Customers"),
        "ai" => new("#312e81", "#3730a3", "#818cf8", "#c7d2fe", "AI"),
        "marketing" => new("#9f1239", "#be123c", "#fb7185", "#fecdd3", "Marketing"),
        "documents" => new("#334155", "#475569", "#94a3b8", "#e2e8f0", "Documents"),
        "settings" => new("#18181b", "#27272a", "#a1a1aa", "#e4e4e7", "Settings"),
        "erp" => new("#1e3a5f", "#1e40af", "#60a5fa", "#bfdbfe", "ERP"),
        "procurement" => new("#3f3f46", "#52525b", "#f59e0b", "#fde68a", "Procurement"),
        "content" => new("#134e4a", "#115e59", "#2dd4bf", "#99f6e4", "Content"),
        "default" => new("#1e293b", "#334155", "#94a3b8", "#e2e8f0", "Operations"),
        _ => new("#0c4a6e", "#075985", "#38bdf8", "#bae6fd", "Platform"),
    };

    private static string Scene(string topic, Palette p, int seed)
    {
        var sb = new StringBuilder();
        switch (topic)
        {
            case "money":
                sb.Append("<rect x=\"120\" y=\"150\" width=\"280\" height=\"140\" rx=\"10\" fill=\"").Append(p.Accent).Append("\" opacity=\".9\" transform=\"rotate(-8 260 220)\"/>");
                sb.Append("<rect x=\"150\" y=\"170\" width=\"220\" height=\"20\" rx=\"4\" fill=\"#fff\" opacity=\".35\"/>");
                sb.Append("<circle cx=\"200\" cy=\"230\" r=\"28\" fill=\"#fff\" opacity=\".25\"/>");
                sb.Append("<rect x=\"420\" y=\"180\" width=\"280\" height=\"140\" rx=\"10\" fill=\"").Append(p.Accent2).Append("\" opacity=\".85\" transform=\"rotate(6 560 250)\"/>");
                sb.Append("<rect x=\"450\" y=\"200\" width=\"220\" height=\"18\" rx=\"4\" fill=\"#14532d\" opacity=\".35\"/>");
                sb.Append("<circle cx=\"680\" cy=\"340\" r=\"36\" fill=\"").Append(p.Accent).Append("\" opacity=\".8\"/>");
                sb.Append("<circle cx=\"740\" cy=\"360\" r=\"28\" fill=\"").Append(p.Accent2).Append("\" opacity=\".75\"/>");
                sb.Append("<circle cx=\"620\" cy=\"370\" r=\"22\" fill=\"#fff\" opacity=\".3\"/>");
                break;
            case "autoparts":
                sb.Append("<circle cx=\"280\" cy=\"250\" r=\"70\" fill=\"none\" stroke=\"").Append(p.Accent).Append("\" stroke-width=\"18\"/>");
                sb.Append("<circle cx=\"280\" cy=\"250\" r=\"28\" fill=\"").Append(p.Accent2).Append("\"/>");
                for (var i = 0; i < 8; i++)
                {
                    var ang = (i * 45 + seed) * Math.PI / 180;
                    var x = 280 + Math.Cos(ang) * 78;
                    var y = 250 + Math.Sin(ang) * 78;
                    sb.Append("<rect x=\"").Append(N(x - 10)).Append("\" y=\"").Append(N(y - 16)).Append("\" width=\"20\" height=\"32\" rx=\"4\" fill=\"").Append(p.Accent).Append("\" transform=\"rotate(").Append(i * 45).Append(' ').Append(N(x)).Append(' ').Append(N(y)).Append(")\"/>");
                }

                sb.Append("<rect x=\"520\" y=\"200\" width=\"220\" height=\"40\" rx=\"8\" fill=\"").Append(p.Accent2).Append("\" transform=\"rotate(25 630 220)\"/>");
                sb.Append("<rect x=\"680\" y=\"160\" width=\"36\" height=\"120\" rx=\"8\" fill=\"").Append(p.Accent).Append("\"/>");
                sb.Append("<ellipse cx=\"650\" cy=\"360\" rx=\"120\" ry=\"36\" fill=\"#fff\" opacity=\".12\"/>");
                sb.Append("<path d=\"M540 340 h180 l40 30 h60 v20 h-300 z\" fill=\"").Append(p.Accent2).Append("\" opacity=\".55\"/>");
                break;
            case "inventory":
                for (var r = 0; r < 3; r++)
                {
                    var y = 150 + r * 90;
                    sb.Append("<rect x=\"140\" y=\"").Append(y).Append("\" width=\"680\" height=\"12\" fill=\"").Append(p.Accent2).Append("\" opacity=\".5\"/>");
                    for (var c = 0; c < 5; c++)
                    {
                        var x = 170 + c * 120 + ((seed + r + c) % 3) * 4;
                        var h = 40 + ((seed + c * 7) % 28);
                        var fill = (c + r) % 2 == 1 ? p.Accent : p.Accent2;
                        sb.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y - h).Append("\" width=\"70\" height=\"").Append(h).Append("\" rx=\"4\" fill=\"").Append(fill).Append("\" opacity=\".85\"/>");
                    }
                }

                break;
            case "orders":
                sb.Append("<rect x=\"220\" y=\"280\" width=\"160\" height=\"120\" rx=\"8\" fill=\"").Append(p.Accent).Append("\"/>");
                sb.Append("<rect x=\"220\" y=\"280\" width=\"160\" height=\"24\" fill=\"#fff\" opacity=\".2\"/>");
                sb.Append("<path d=\"M220 304 L300 250 L380 304\" fill=\"").Append(p.Accent2).Append("\"/>");
                sb.Append("<rect x=\"420\" y=\"240\" width=\"150\" height=\"160\" rx=\"8\" fill=\"").Append(p.Accent2).Append("\" opacity=\".9\"/>");
                sb.Append("<rect x=\"420\" y=\"240\" width=\"150\" height=\"28\" fill=\"#fff\" opacity=\".2\"/>");
                sb.Append("<rect x=\"600\" y=\"260\" width=\"140\" height=\"140\" rx=\"8\" fill=\"").Append(p.Accent).Append("\" opacity=\".8\"/>");
                sb.Append("<line x1=\"600\" y1=\"330\" x2=\"740\" y2=\"330\" stroke=\"#fff\" stroke-width=\"3\" opacity=\".35\"/>");
                sb.Append("<line x1=\"670\" y1=\"260\" x2=\"670\" y2=\"400\" stroke=\"#fff\" stroke-width=\"3\" opacity=\".35\"/>");
                break;
            case "logistics":
                sb.Append("<rect x=\"180\" y=\"250\" width=\"280\" height=\"110\" rx=\"12\" fill=\"").Append(p.Accent).Append("\"/>");
                sb.Append("<rect x=\"460\" y=\"280\" width=\"160\" height=\"80\" rx=\"10\" fill=\"").Append(p.Accent2).Append("\"/>");
                sb.Append("<polygon points=\"620,280 700,280 740,330 740,360 620,360\" fill=\"").Append(p.Accent).Append("\"/>");
                sb.Append("<rect x=\"640\" y=\"295\" width=\"50\" height=\"30\" rx=\"4\" fill=\"#fff\" opacity=\".35\"/>");
                sb.Append("<circle cx=\"260\" cy=\"370\" r=\"28\" fill=\"#0f172a\"/><circle cx=\"260\" cy=\"370\" r=\"14\" fill=\"#94a3b8\"/>");
                sb.Append("<circle cx=\"520\" cy=\"370\" r=\"28\" fill=\"#0f172a\"/><circle cx=\"520\" cy=\"370\" r=\"14\" fill=\"#94a3b8\"/>");
                sb.Append("<circle cx=\"680\" cy=\"370\" r=\"28\" fill=\"#0f172a\"/><circle cx=\"680\" cy=\"370\" r=\"14\" fill=\"#94a3b8\"/>");
                break;
            case "customers":
                int[] xs = [280, 420, 560, 700];
                for (var i = 0; i < xs.Length; i++)
                {
                    var y = 220 + ((seed + i * 11) % 40);
                    var head = i % 2 == 1 ? p.Accent : p.Accent2;
                    var body = i % 2 == 1 ? p.Accent2 : p.Accent;
                    sb.Append("<circle cx=\"").Append(xs[i]).Append("\" cy=\"").Append(y).Append("\" r=\"36\" fill=\"").Append(head).Append("\" opacity=\".9\"/>");
                    sb.Append("<circle cx=\"").Append(xs[i]).Append("\" cy=\"").Append(y + 70).Append("\" r=\"48\" fill=\"").Append(body).Append("\" opacity=\".45\"/>");
                }

                break;
            case "ai":
                for (var i = 0; i < 12; i++)
                {
                    var x = 180 + (i % 4) * 160;
                    var y = 170 + i / 4 * 90;
                    sb.Append("<circle cx=\"").Append(x).Append("\" cy=\"").Append(y).Append("\" r=\"16\" fill=\"").Append(p.Accent).Append("\"/>");
                    if (i % 4 < 3)
                    {
                        sb.Append("<line x1=\"").Append(x).Append("\" y1=\"").Append(y).Append("\" x2=\"").Append(x + 160).Append("\" y2=\"").Append(y).Append("\" stroke=\"").Append(p.Accent2).Append("\" stroke-width=\"3\" opacity=\".5\"/>");
                    }

                    if (i < 8)
                    {
                        sb.Append("<line x1=\"").Append(x).Append("\" y1=\"").Append(y).Append("\" x2=\"").Append(x).Append("\" y2=\"").Append(y + 90).Append("\" stroke=\"").Append(p.Accent2).Append("\" stroke-width=\"3\" opacity=\".35\"/>");
                    }
                }

                break;
            case "documents":
                for (var i = 0; i < 4; i++)
                {
                    var x = 220 + i * 130;
                    var y = 160 + (i % 2) * 20;
                    sb.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y).Append("\" width=\"110\" height=\"150\" rx=\"6\" fill=\"#fff\" opacity=\"").Append(N(0.75 - i * 0.08)).Append("\"/>");
                    sb.Append("<rect x=\"").Append(x + 14).Append("\" y=\"").Append(y + 24).Append("\" width=\"80\" height=\"8\" rx=\"2\" fill=\"").Append(p.Accent).Append("\" opacity=\".7\"/>");
                    sb.Append("<rect x=\"").Append(x + 14).Append("\" y=\"").Append(y + 44).Append("\" width=\"70\" height=\"6\" rx=\"2\" fill=\"").Append(p.Panel).Append("\" opacity=\".5\"/>");
                    sb.Append("<rect x=\"").Append(x + 14).Append("\" y=\"").Append(y + 60).Append("\" width=\"75\" height=\"6\" rx=\"2\" fill=\"").Append(p.Panel).Append("\" opacity=\".4\"/>");
                }

                break;
            case "marketing":
                sb.Append("<rect x=\"200\" y=\"180\" width=\"360\" height=\"200\" rx=\"16\" fill=\"").Append(p.Accent).Append("\" opacity=\".85\"/>");
                sb.Append("<circle cx=\"280\" cy=\"250\" r=\"40\" fill=\"#fff\" opacity=\".25\"/>");
                sb.Append("<rect x=\"340\" y=\"220\" width=\"180\" height=\"14\" rx=\"4\" fill=\"#fff\" opacity=\".5\"/>");
                sb.Append("<rect x=\"340\" y=\"250\" width=\"140\" height=\"10\" rx=\"3\" fill=\"#fff\" opacity=\".35\"/>");
                sb.Append("<rect x=\"600\" y=\"200\" width=\"160\" height=\"160\" rx=\"20\" fill=\"").Append(p.Accent2).Append("\" opacity=\".8\"/>");
                sb.Append("<circle cx=\"680\" cy=\"260\" r=\"28\" fill=\"#fff\" opacity=\".35\"/>");
                sb.Append("<rect x=\"640\" y=\"310\" width=\"80\" height=\"10\" rx=\"3\" fill=\"#fff\" opacity=\".45\"/>");
                break;
            case "procurement":
                sb.Append("<rect x=\"200\" y=\"200\" width=\"200\" height=\"160\" rx=\"10\" fill=\"").Append(p.Accent).Append("\"/>");
                sb.Append("<rect x=\"230\" y=\"230\" width=\"140\" height=\"12\" rx=\"3\" fill=\"#fff\" opacity=\".4\"/>");
                sb.Append("<rect x=\"230\" y=\"260\" width=\"120\" height=\"10\" rx=\"3\" fill=\"#fff\" opacity=\".3\"/>");
                sb.Append("<rect x=\"480\" y=\"180\" width=\"240\" height=\"200\" rx=\"12\" fill=\"").Append(p.Accent2).Append("\" opacity=\".85\"/>");
                sb.Append("<path d=\"M520 240 h160 M520 280 h140 M520 320 h150\" stroke=\"#fff\" stroke-width=\"8\" opacity=\".35\" stroke-linecap=\"round\"/>");
                break;
            default:
                sb.Append("<rect x=\"160\" y=\"160\" width=\"640\" height=\"240\" rx=\"16\" fill=\"").Append(p.Panel).Append("\" opacity=\".9\"/>");
                sb.Append("<rect x=\"190\" y=\"190\" width=\"180\" height=\"180\" rx=\"12\" fill=\"").Append(p.Accent).Append("\" opacity=\".45\"/>");
                sb.Append("<rect x=\"400\" y=\"190\" width=\"360\" height=\"40\" rx=\"8\" fill=\"").Append(p.Accent2).Append("\" opacity=\".55\"/>");
                sb.Append("<rect x=\"400\" y=\"250\" width=\"280\" height=\"20\" rx=\"6\" fill=\"#fff\" opacity=\".2\"/>");
                sb.Append("<rect x=\"400\" y=\"290\" width=\"320\" height=\"20\" rx=\"6\" fill=\"#fff\" opacity=\".15\"/>");
                sb.Append("<rect x=\"400\" y=\"330\" width=\"200\" height=\"20\" rx=\"6\" fill=\"#fff\" opacity=\".12\"/>");
                break;
        }

        return sb.ToString();
    }

    private static string N(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0000001)
        {
            return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string MbLimit(string value, int max)
    {
        var count = 0;
        var index = 0;
        while (index < value.Length && count < max)
        {
            index += char.IsSurrogatePair(value, index) ? 2 : 1;
            count++;
        }

        return index >= value.Length ? value : value[..index];
    }

    private static string Xml(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);
}
