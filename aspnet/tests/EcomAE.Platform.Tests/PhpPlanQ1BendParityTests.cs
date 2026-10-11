using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BendParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Bend");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Bend_MatchPhpGolden()
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
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/shop/order_process/epc_order_whatsapp_share.php", PhpPlanQ1Bend.OrderWhatsappSharePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Bend.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 1, null), StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Bend.OrderWhatsappSharePath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Bend.Reset();
        SeedInjects();
        return name switch
        {
            "empty" => new object[]
            {
                Clip(PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(new Dictionary<string, object?>(), 0, new Dictionary<string, object?>())),
                Clip(PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 0, new Dictionary<string, object?>()))
            },
            "nophone" => NoPhone(),
            "phone" => Phone(),
            "lpo" => Lpo(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void SeedInjects()
    {
        PhpPlanQ1Bend.SalesDigits = () => "971567607011";
        PhpPlanQ1Bend.CustomerMessage = (_, _, _) => "CUST";
        PhpPlanQ1Bend.SalesMessage = (_, _, _) => "SALES";
        PhpPlanQ1Bend.Styles = () => "<style>wa</style>";
        PhpPlanQ1Bend.SalesDisplay = () => "Sales line";
        PhpPlanQ1Bend.Escape = value => Convert.ToString(value) ?? "";
        PhpPlanQ1Bend.Button = (href, label, extraClass, _) =>
            href == "" ? "" : "<a class=\"" + extraClass + "\" href=\"" + href + "\">" + label + "</a>";
        PhpPlanQ1Bend.ShareUrl = (digits, text) => digits == "" ? "" : "https://wa.me/" + digits + "?t=" + text;
        PhpPlanQ1Bend.Digits = phone => new string((phone ?? "").Where(char.IsDigit).ToArray());
    }

    private static object NoPhone()
    {
        PhpPlanQ1Bend.OrderItems = _ => [new Dictionary<string, object?> { ["name"] = "Pad" }];
        PhpPlanQ1Bend.LpoGroups = (_, _) => [];
        var html = PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(new Dictionary<string, object?> { ["id"] = 9 }, 9, new Dictionary<string, object?>());
        return new object[]
        {
            Clip(html),
            html.Contains("No customer phone", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("Share with sales", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("Supplier LPO", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Phone()
    {
        PhpPlanQ1Bend.OrderItems = _ => [new Dictionary<string, object?> { ["name"] = "O'Brien" }];
        PhpPlanQ1Bend.LpoGroups = (_, _) => [];
        var html = PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(
            new Dictionary<string, object?> { ["id"] = 3, ["phone_not_auth"] = "050" },
            3,
            new Dictionary<string, object?> { ["phone"] = "+971 50 111 2222" });
        return new object[]
        {
            Clip(html),
            html.Contains("Message customer", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("971501112222", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("No customer phone", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Lpo()
    {
        PhpPlanQ1Bend.OrderItems = _ => [new Dictionary<string, object?> { ["name"] = "Filter" }];
        PhpPlanQ1Bend.LpoGroups = (_, _) =>
        [
            new Dictionary<string, object?>
            {
                ["storage_name"] = "Acme WH",
                ["target_label"] = "supplier",
                ["wa_href"] = "https://wa.me/97150?t=LPO",
                ["lpo_message"] = "LPO text"
            },
            new Dictionary<string, object?>
            {
                ["storage_name"] = "O'Neil",
                ["target_label"] = "sales (forward LPO)",
                ["wa_href"] = "https://wa.me/97156?t=FWD",
                ["lpo_message"] = "fwd"
            }
        ];
        var html = PhpPlanQ1Bend.EpcOrderWhatsappShareRenderHtml(
            new Dictionary<string, object?> { ["id"] = 4, ["phone_not_auth"] = "+971-56-000" },
            4,
            new Dictionary<string, object?>());
        return new object[]
        {
            Clip(html),
            html.Contains("LPO: Acme WH → supplier", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("LPO: O'Neil", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("97156000", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object[] Clip(string html)
        => [PhpStrlen(html), PhpSubstr(html, 0, 80), PhpSubstr(html, -80, 80)];

    private static int PhpStrlen(string value) => Encoding.UTF8.GetByteCount(value);

    private static string PhpSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (start < 0)
        {
            start = bytes.Length + start;
        }

        if (start < 0)
        {
            start = 0;
        }

        if (start >= bytes.Length)
        {
            return "";
        }

        return Encoding.UTF8.GetString(bytes, start, Math.Min(length, bytes.Length - start));
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            _ => true
        };
    }
}
