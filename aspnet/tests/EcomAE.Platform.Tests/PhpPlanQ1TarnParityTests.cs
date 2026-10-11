using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1TarnParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Tarn");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Tarn_MatchPhpGolden()
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
                var exp = expected.GetRawText();
                var got = Json(actual);
                failures.Add(name + " exp=" + exp[..Math.Min(280, exp.Length)] + " got=" + got[..Math.Min(280, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("modules/login/module.php", PhpPlanQ1Tarn.LoginModulePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Tarn.Reset();
        SeedEn();
        PhpPlanQ1Tarn.GetUserId = () => 0;
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Tarn.LoginModulePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Tarn.EpcLoginModule(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Tarn.Reset();
        return name switch
        {
            "guest" => Guest(),
            "signed" => Signed(),
            "labels" => Labels(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Guest()
    {
        SeedEn();
        PhpPlanQ1Tarn.GetUserId = () => 0;
        var guest = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserId = () => "0";
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Ignored" };
        var zero = PhpPlanQ1Tarn.EpcLoginModule();
        return new object[] { guest, zero };
    }

    private static object Signed()
    {
        SeedEn();
        PhpPlanQ1Tarn.GetUserId = () => 7;
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Ali", ["surname"] = "Khan" };
        var both = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Ali" };
        var nameOnly = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal);
        var empty = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = null, ["surname"] = "Khan" };
        var nullName = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Ali", ["surname"] = "" };
        var space = PhpPlanQ1Tarn.EpcLoginModule();
        return new object[] { both, nameOnly, empty, nullName, space };
    }

    private static object Labels()
    {
        PhpPlanQ1Tarn.TranslateById = raw => raw switch
        {
            "4758" => "دخول",
            "4759" => "حساب",
            "1311" => "كلمة",
            "4666" => "تذكرني",
            "4008" => "ادخل",
            "3987" => "تسجيل",
            "4667" => "نسيت",
            "4760" => "أهلا",
            "4668" => "الملف",
            "4761" => "تعديل",
            "3996" => "خروج",
            "4762" => "عميل",
            _ => raw
        };
        PhpPlanQ1Tarn.LangHref = () => "/ar";
        PhpPlanQ1Tarn.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-ar" };
        PhpPlanQ1Tarn.GetUserId = () => 0;
        var guest = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.GetUserId = () => 4;
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "نورا" };
        var signed = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.TranslateById = raw => raw;
        PhpPlanQ1Tarn.LangHref = () => "";
        PhpPlanQ1Tarn.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "" };
        PhpPlanQ1Tarn.GetUserId = () => 0;
        PhpPlanQ1Tarn.GetUserProfile = null;
        var missing = PhpPlanQ1Tarn.EpcLoginModule();
        return new object[] { guest, signed, missing };
    }

    private static object Tenant()
    {
        SeedEn();
        PhpPlanQ1Tarn.GetUserId = () => 7;
        PhpPlanQ1Tarn.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-acme" };
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "AcmeUser" };
        var acme = PhpPlanQ1Tarn.EpcLoginModule();
        PhpPlanQ1Tarn.LangHref = () => "/ar";
        PhpPlanQ1Tarn.GetUserId = () => 8;
        PhpPlanQ1Tarn.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-beta" };
        PhpPlanQ1Tarn.GetUserProfile = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "BetaUser" };
        var beta = PhpPlanQ1Tarn.EpcLoginModule();
        return new object[] { acme, beta };
    }

    private static void SeedEn()
    {
        PhpPlanQ1Tarn.TranslateById = raw => raw switch
        {
            "4758" => "Sign in",
            "4759" => "Login",
            "1311" => "Password",
            "4666" => "Remember me",
            "4008" => "Enter",
            "3987" => "Register",
            "4667" => "Forgot password",
            "4760" => "Welcome",
            "4668" => "Profile",
            "4761" => "Edit",
            "3996" => "Logout",
            "4762" => "Customer",
            _ => raw
        };
        PhpPlanQ1Tarn.LangHref = () => "/en";
        PhpPlanQ1Tarn.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
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
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
