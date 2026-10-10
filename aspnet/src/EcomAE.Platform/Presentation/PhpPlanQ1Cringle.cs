using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-cringle alternative bread crumbs. PHP identifier kept for the inventory:
/// <c>get_alternative_bread_crumbs</c>. Path:
/// <c>modules/bread_crumbs/helper.php</c>.
/// GET never mints a session cookie. Live HTTP caption fetch stays injected.
/// </summary>
public static class PhpPlanQ1Cringle
{
    public const string BreadCrumbsHelperPath = "modules/bread_crumbs/helper.php";

    public static Func<string, string>? QueryGet { get; set; }
    public static Func<string, string>? ConfigGet { get; set; }
    public static Func<string, string>? FetchUrl { get; set; }

    public static void Reset()
    {
        QueryGet = _ => "";
        ConfigGet = _ => "";
        FetchUrl = _ => "";
    }

    public static object? GetAlternativeBreadCrumbs(MySqlConnection db, string url, string breadCrumb)
    {
        using var stmt = db.CreateCommand();
        stmt.CommandText = "SELECT * FROM `bread_crumbs_rules` WHERE `url` = @url AND `bread_crumb` = @bread_crumb";
        stmt.Parameters.AddWithValue("@url", url);
        stmt.Parameters.AddWithValue("@bread_crumb", breadCrumb);
        using var reader = stmt.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        var captionRaw = reader["bread_crumb_caption"] is DBNull ? "" : Convert.ToString(reader["bread_crumb_caption"]) ?? "";
        var hrefRaw = reader["bread_crumb_href_args"] is DBNull ? "" : Convert.ToString(reader["bread_crumb_href_args"]) ?? "";
        var crumb = reader["bread_crumb"] is DBNull ? "" : Convert.ToString(reader["bread_crumb"]) ?? "";
        reader.Close();

        var alternative = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (captionRaw != "")
        {
            using var captionJson = JsonDocument.Parse(captionRaw);
            var caption = captionJson.RootElement;
            var type = caption.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            switch (type)
            {
                case "get":
                    alternative["step_caption"] = QueryGet != null ? QueryGet(Str(caption, "value")) : "";
                    break;
                case "text":
                    alternative["step_caption"] = Str(caption, "value");
                    break;
                case "url":
                    var fetch = Str(caption, "value");
                    if (caption.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
                    {
                        var i = 0;
                        foreach (var arg in args.EnumerateArray())
                        {
                            var argType = arg.TryGetProperty("type", out var at) ? at.GetString() ?? "" : "";
                            var argValue = arg.TryGetProperty("value", out var av) ? av.GetString() ?? "" : "";
                            var replacement = argType switch
                            {
                                "config" => ConfigGet != null ? ConfigGet(argValue) : "",
                                "get" => QueryGet != null ? QueryGet(argValue) : "",
                                "text" => argValue,
                                _ => ""
                            };
                            fetch = fetch.Replace("%" + i, replacement, StringComparison.Ordinal);
                            i++;
                        }
                    }

                    alternative["step_caption"] = FetchUrl != null ? FetchUrl(fetch) : "";
                    break;
            }
        }

        var href = crumb;
        if (hrefRaw != "")
        {
            using var hrefJson = JsonDocument.Parse(hrefRaw);
            var i = 0;
            foreach (var arg in hrefJson.RootElement.EnumerateArray())
            {
                var ampersand = i > 0 ? "&" : "?";
                var name = arg.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                href += ampersand + name + "=";
                var argType = arg.TryGetProperty("type", out var at) ? at.GetString() ?? "" : "";
                href += argType switch
                {
                    "get" => QueryGet != null ? QueryGet(arg.TryGetProperty("value", out var gv) ? gv.GetString() ?? "" : "") : "",
                    "text" => arg.TryGetProperty("value", out var tv) ? tv.GetString() ?? "" : "",
                    _ => ""
                };
                i++;
            }
        }

        alternative["step_url"] = href;
        return alternative;
    }

    private static string Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";
}
