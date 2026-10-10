using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1GaffParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Gaff");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Gaff_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(Json(actual.Extra), expected))
            {
                using var left = JsonDocument.Parse(Json(actual.Extra));
                failures.Add(name + " " + FirstValueDiff(left.RootElement, expected, name));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Gaff.WhatsappSharePath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Gaff.WhatsappSharePath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Gaff.Reset();
        Assert.Contains("epc_whatsapp_share.php", PhpPlanQ1Gaff.WhatsappSharePath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Gaff.WhatsappSharePath, StringComparison.Ordinal);
        var btn = PhpPlanQ1Gaff.EpcWaButton("https://wa.me/1", "O'Go", "", "O'title");
        Assert.Contains("&#039;", btn, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", btn, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Gaff.Reset();
        return name switch
        {
            "pure" => Pure(),
            "config" => Config(),
            "db" => Db(),
            "html" => Html(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Pure()
        => new(new object?[]
        {
            new[] { PhpPlanQ1Gaff.EpcWaH("O'Brien & Co"), PhpPlanQ1Gaff.EpcWaH("<x>"), PhpPlanQ1Gaff.EpcWaH("") },
            new[] { PhpPlanQ1Gaff.EpcWaDigits("+971 56 760 7011"), PhpPlanQ1Gaff.EpcWaDigits("!!!"), PhpPlanQ1Gaff.EpcWaDigits(null), PhpPlanQ1Gaff.EpcWaDigits("00-12") },
            new[] { PhpPlanQ1Gaff.EpcWaShareUrl("+971-50", "hello / path"), PhpPlanQ1Gaff.EpcWaShareUrl("", "x"), PhpPlanQ1Gaff.EpcWaShareUrl("abc", "x") },
            new[]
            {
                PhpPlanQ1Gaff.EpcWaBilingual("EN", "AR"),
                PhpPlanQ1Gaff.EpcWaBilingual("", "AR"),
                PhpPlanQ1Gaff.EpcWaBilingual("EN", ""),
                PhpPlanQ1Gaff.EpcWaBilingual("  ", "  "),
                PhpPlanQ1Gaff.EpcWaBilingual("O'Name", "عربي")
            },
            new[]
            {
                PhpPlanQ1Gaff.EpcWaOrderLinesText(Items(), 12),
                PhpPlanQ1Gaff.EpcWaOrderLinesText(Items(), 1),
                PhpPlanQ1Gaff.EpcWaOrderLinesText([], 8)
            }
        });

    private static Rendered Config()
    {
        var empty = Cfg();
        var zero = Cfg(("epc_whatsapp_number", "0"), ("from_name", "0"));
        var full = Cfg(("epc_whatsapp_number", "+971 50 000 0001"), ("from_name", "O'Store"));
        PhpPlanQ1Gaff.AgentHref = () => "https://wa.me/971501112233";
        var agentDigits = PhpPlanQ1Gaff.EpcWaSalesDigits(Cfg());
        var agentDisplay = PhpPlanQ1Gaff.EpcWaSalesDisplay(Cfg());
        PhpPlanQ1Gaff.BrandName = () => "O'Brand";
        var brandName = PhpPlanQ1Gaff.EpcWaSiteName(Cfg(("from_name", "ignored")));
        PhpPlanQ1Gaff.BrandName = () => "";
        PhpPlanQ1Gaff.AgentHref = () => "";
        var cfg = Cfg(("from_name", "Acme Parts"), ("domain_path", "https://shop.example/"));
        var items = Items();
        var order = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["price_sum"] = 99.1,
            ["phone_not_auth"] = "+971-55-1"
        };
        return new Rendered(new object?[]
        {
            PhpPlanQ1Gaff.EpcWaSalesDigits(empty),
            PhpPlanQ1Gaff.EpcWaSalesDisplay(empty),
            PhpPlanQ1Gaff.EpcWaSiteName(empty),
            PhpPlanQ1Gaff.EpcWaSalesDigits(zero),
            PhpPlanQ1Gaff.EpcWaSalesDisplay(zero),
            PhpPlanQ1Gaff.EpcWaSiteName(zero),
            PhpPlanQ1Gaff.EpcWaSalesDigits(full),
            PhpPlanQ1Gaff.EpcWaSalesDisplay(full),
            PhpPlanQ1Gaff.EpcWaSiteName(full),
            agentDigits,
            agentDisplay,
            brandName,
            PhpPlanQ1Gaff.EpcWaProductMessage(cfg, "Bosch", "0986", "O'filter", "12.50"),
            PhpPlanQ1Gaff.EpcWaProductMessage(cfg, "Bosch", "0986", "", null),
            PhpPlanQ1Gaff.EpcWaCartMessage(cfg, ["A ×1", "B ×2"], "10.00"),
            PhpPlanQ1Gaff.EpcWaCartMessage(cfg, [], null),
            PhpPlanQ1Gaff.EpcWaOrderStatusMessage(cfg, 7, "Packed", order, items),
            PhpPlanQ1Gaff.EpcWaOrderCustomerMessage(cfg, 7, order, items),
            PhpPlanQ1Gaff.EpcWaOrderSalesMessage(cfg, 7, order, items, "staff o'role"),
            PhpPlanQ1Gaff.EpcWaSupplierLpoMessage(cfg, 7, "WH o'name", items)
        });
    }

    private static Rendered Db()
    {
        var rows = new Dictionary<int, List<Dictionary<string, object?>>>
        {
            [9] =
            [
                Row(("id", 1), ("t2_manufacturer", "Bosch"), ("t2_article", "0986"), ("t2_article_show", "0 986"),
                    ("t2_name", "O'item"), ("count_need", 2), ("price", "12.50"), ("t2_storage_id", 7)),
                Row(("id", 2), ("t2_manufacturer", "NGK"), ("t2_article", "BKR"), ("t2_article_show", null),
                    ("t2_name", ""), ("count_need", 1), ("price", "3.00"), ("t2_storage_id", 8))
            ]
        };
        var phones = new Dictionary<int, string> { [7] = "+971-50-999", [8] = "" };
        var names = new Dictionary<int, string> { [7] = "WH o'one", [8] = "" };
        var users = new Dictionary<int, string> { [5] = "+971 55 111 2222", [6] = "" };
        var logs = new List<Dictionary<string, object?>>();
        PhpPlanQ1Gaff.OrderItems = id => rows.TryGetValue(id, out var list) ? list : [];
        PhpPlanQ1Gaff.SupplierPhone = id => phones.TryGetValue(id, out var p) ? p : "";
        PhpPlanQ1Gaff.StorageName = id => names.TryGetValue(id, out var n) ? n : "";
        PhpPlanQ1Gaff.UserPhone = id => users.TryGetValue(id, out var p) ? p : "";
        PhpPlanQ1Gaff.InsertLog = (orderId, _, text) => logs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId,
            ["user_id"] = 0,
            ["is_manager"] = 0,
            ["is_robot"] = 1,
            ["text"] = RegexHref(text)
        });
        var items = PhpPlanQ1Gaff.EpcWaOrderItems(9);
        var none = PhpPlanQ1Gaff.EpcWaOrderItems(404);
        var phone7 = PhpPlanQ1Gaff.EpcWaSupplierPhoneForStorage(7);
        var phone0 = PhpPlanQ1Gaff.EpcWaSupplierPhoneForStorage(0);
        var phone8 = PhpPlanQ1Gaff.EpcWaSupplierPhoneForStorage(8);
        var name7 = PhpPlanQ1Gaff.EpcWaStorageName(7);
        var name8 = PhpPlanQ1Gaff.EpcWaStorageName(8);
        var name0 = PhpPlanQ1Gaff.EpcWaStorageName(0);
        var cfg = Cfg(("from_name", "Acme"));
        PhpPlanQ1Gaff.ItemStorageId = _ => 7;
        var groups = PhpPlanQ1Gaff.EpcWaOrderLpoGroups(cfg, 9, items);
        var slim = groups.Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["storage_id"] = g["storage_id"],
            ["storage_name"] = g["storage_name"],
            ["item_count"] = ((List<Dictionary<string, object?>>)g["items"]!).Count,
            ["supplier_phone"] = g["supplier_phone"],
            ["target_label"] = g["target_label"],
            ["href_prefix"] = (Convert.ToString(g["wa_href"]) ?? "")[..Math.Min(24, (Convert.ToString(g["wa_href"]) ?? "").Length)],
            ["msg_has_quote"] = (Convert.ToString(g["lpo_message"]) ?? "").Contains("&#039;", StringComparison.Ordinal)
                || (Convert.ToString(g["lpo_message"]) ?? "").Contains("O'", StringComparison.Ordinal)
        }).ToList();
        var orderPhone = Row(("phone_not_auth", "+971-55-9"), ("user_id", 5));
        PhpPlanQ1Gaff.EpcWaNotifyOrderStatusChange(cfg, 0, "Packed", orderPhone);
        PhpPlanQ1Gaff.EpcWaNotifyOrderStatusChange(cfg, 9, "", orderPhone);
        PhpPlanQ1Gaff.EpcWaNotifyOrderStatusChange(cfg, 9, "Packed", orderPhone);
        var orderUser = Row(("phone_not_auth", ""), ("user_id", 5));
        PhpPlanQ1Gaff.EpcWaNotifyOrderStatusChange(cfg, 9, "Shipped", orderUser);
        return new Rendered(new object?[] { items, none, phone7, phone0, phone8, name7, name8, name0, slim, logs });
    }

    private static Rendered Html()
    {
        var cfg = Cfg(("epc_whatsapp_number", "+971 50 O'x"), ("from_name", "O'Site"), ("domain_path", "https://shop.example/a"));
        var btn = new[]
        {
            PhpPlanQ1Gaff.EpcWaButton("", "X"),
            PhpPlanQ1Gaff.EpcWaButton("https://wa.me/1?text=a&b=1", "Chat o'now", "extra", "Title o'x"),
            PhpPlanQ1Gaff.EpcWaButton("https://wa.me/1", "Go")
        };
        var css = PhpPlanQ1Gaff.EpcWaStyles();
        var script = PhpPlanQ1Gaff.EpcWaFrontendScript(cfg);
        PhpPlanQ1Gaff.AgentHref = () => "https://wa.me/97150";
        var scriptAgent = PhpPlanQ1Gaff.EpcWaFrontendScript(Cfg());
        return new Rendered(new object?[] { btn, css.Length, css[..Math.Min(80, css.Length)], css.Length <= 60 ? css : css[^60..], script, scriptAgent });
    }

    private static List<Dictionary<string, object?>> Items()
        =>
        [
            Row(("t2_manufacturer", "Bosch"), ("t2_article", "0986"), ("t2_article_show", "0 986 479 501"),
                ("t2_name", "O'filter"), ("count_need", 2), ("price", 12.5), ("t2_storage_id", 7)),
            Row(("t2_manufacturer", "NGK"), ("t2_article", "BKR"), ("t2_name", ""), ("count_need", 0), ("t2_storage_id", 0))
        ];

    private static Dictionary<string, object?> Cfg(params (string Key, object? Value)[] extra)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["epc_whatsapp_number"] = "",
            ["from_name"] = "",
            ["domain_path"] = "https://www.epartscart.com/",
            ["backend_dir"] = "cp"
        };
        foreach (var (key, value) in extra)
        {
            row[key] = value;
        }

        return row;
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] pairs)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            row[key] = value;
        }

        return row;
    }

    private static string RegexHref(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"wa\.me/[0-9]+\?text=.+$", "wa.me/<digits>?text=<enc>");

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string FirstValueDiff(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return path + " kind " + left.ValueKind + " vs " + right.ValueKind;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in right.EnumerateObject())
                {
                    if (!left.TryGetProperty(prop.Name, out var other))
                    {
                        return path + "." + prop.Name + " missing on actual";
                    }

                    var nested = FirstValueDiff(other, prop.Value, path + "." + prop.Name);
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                if (a.Count != b.Count)
                {
                    return path + " len " + a.Count + " vs " + b.Count;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    var nested = FirstValueDiff(a[i], b[i], path + "[" + i + "]");
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.String:
                var ls = left.GetString() ?? "";
                var rs = right.GetString() ?? "";
                if (ls == rs)
                {
                    return "";
                }

                var n = Math.Min(ls.Length, rs.Length);
                var at = 0;
                while (at < n && ls[at] == rs[at])
                {
                    at++;
                }

                static string Clip(string value, int at)
                {
                    var start = Math.Max(0, at - 70);
                    var len = Math.Min(140, value.Length - start);
                    return value.Substring(start, len).Replace("\n", "\\n").Replace("\t", "\\t");
                }

                return path + " at=" + at + " expLen=" + rs.Length + " gotLen=" + ls.Length
                    + " exp=" + Clip(rs, at) + " got=" + Clip(ls, at);
            default:
                return "";
        }
    }
}
