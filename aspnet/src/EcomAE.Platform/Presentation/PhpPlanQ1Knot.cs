using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-knot storefront metadata handler. Path kept for the inventory:
/// <c>plugins/metadata_handler/metadata_handler.php</c>.
/// GET never mints a session cookie. Leftover page-url / translate / HTTP parents stay injected.
/// </summary>
public static class PhpPlanQ1Knot
{
    public const string MetadataHandlerPath = "plugins/metadata_handler/metadata_handler.php";

    public static Func<string>? PageUrl { get; set; }
    public static Func<string, string>? FetchHttp { get; set; }
    public static Func<int, string>? Translate { get; set; }

    public static void Reset()
    {
        PageUrl = () => "";
        FetchHttp = _ => "";
        Translate = id => "t" + id + " %0";
    }

    public static Dictionary<string, string> Apply(
        MySqlConnection db,
        int contentId,
        string title,
        string description,
        string keywords,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> config)
    {
        var lastUrl = "";
        using (var stmt = db.CreateCommand())
        {
            stmt.CommandText = "SELECT * FROM `metadata_handler_rules` WHERE `content_id` = @content_id";
            stmt.Parameters.AddWithValue("@content_id", contentId);
            using var reader = stmt.ExecuteReader();
            if (reader.Read())
            {
                var titleRule = Col(reader, "title_rule");
                var descriptionRule = Col(reader, "description_rule");
                reader.Close();
                if (titleRule != "")
                {
                    using var titleJson = JsonDocument.Parse(titleRule);
                    var type = Str(titleJson.RootElement, "type");
                    switch (type)
                    {
                        case "url":
                            var url = Str(titleJson.RootElement, "value");
                            url = SubstituteArgs(url, titleJson.RootElement, query, config);
                            lastUrl = url;
                            title = FetchHttp != null ? FetchHttp(url) : "";
                            break;
                        case "complex":
                            var id = titleJson.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                                ? v.GetInt32()
                                : int.TryParse(Str(titleJson.RootElement, "value"), out var parsed) ? parsed : 0;
                            title = Translate != null ? Translate(id) : "t" + id + " %0";
                            title = SubstituteArgs(title, titleJson.RootElement, query, config, getOnly: true);
                            break;
                    }
                }

                if (descriptionRule != "")
                {
                    using var descJson = JsonDocument.Parse(descriptionRule);
                    if (Str(descJson.RootElement, "type") == "like_title")
                    {
                        description = title.Replace("\"", "\\\"", StringComparison.Ordinal);
                    }
                }
            }
        }

        var pageUrl = PageUrl != null ? PageUrl() : "";
        using (var stmt = db.CreateCommand())
        {
            stmt.CommandText = "SELECT * FROM `text_for_url` WHERE `url` = @url";
            stmt.Parameters.AddWithValue("@url", pageUrl);
            using var reader = stmt.ExecuteReader();
            if (reader.Read())
            {
                var urlTitle = Col(reader, "title_tag");
                var urlDesc = Col(reader, "description_tag");
                var urlKw = Col(reader, "keywords_tag");
                if (urlTitle != "")
                {
                    title = urlTitle;
                }

                if (urlDesc != "")
                {
                    description = urlDesc;
                }

                if (urlKw != "")
                {
                    keywords = urlKw;
                }
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["description"] = description,
            ["keywords"] = keywords,
            ["lastUrl"] = lastUrl
        };
    }

    private static string SubstituteArgs(
        string template,
        JsonElement rule,
        IReadOnlyDictionary<string, string> query,
        IReadOnlyDictionary<string, string> config,
        bool getOnly = false)
    {
        if (!rule.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
        {
            return template;
        }

        var i = 0;
        foreach (var arg in args.EnumerateArray())
        {
            var argType = Str(arg, "type");
            var argValue = Str(arg, "value");
            string replacement;
            if (getOnly)
            {
                replacement = argType == "get" && query.TryGetValue(argValue, out var q) ? q : "";
            }
            else
            {
                replacement = argType switch
                {
                    "config" => config.TryGetValue(argValue, out var c) ? c : "",
                    "get" => query.TryGetValue(argValue, out var q) ? q : "",
                    "text" => argValue,
                    _ => ""
                };
            }

            if (!getOnly || argType == "get")
            {
                template = template.Replace("%" + i, replacement, StringComparison.Ordinal);
            }

            i++;
        }

        return template;
    }

    private static string Col(MySqlDataReader reader, string name)
        => reader[name] is DBNull ? "" : Convert.ToString(reader[name]) ?? "";

    private static string Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p)
            ? p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.ToString()
            : "";
}
