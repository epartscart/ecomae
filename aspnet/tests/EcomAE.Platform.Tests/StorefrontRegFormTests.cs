using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The registration page against goldens recorded from the real <c>content/users/regform.php</c> by
/// <c>Fixtures/RegForm/harness.py</c> (fake <c>$db_link</c>, stubbed <c>DP_User</c>, translations and auth core; the
/// real registration render half, auth layout, user agreement and e-mail code modal).
/// </summary>
public sealed class StorefrontRegFormTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RegForm");

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.EnumerateObject())
        {
            data.Add(testCase.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_php(string name)
    {
        var c = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.GetProperty(name);
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).GetString();

        var missing = c.TryGetProperty("missing", out var m) ? m.EnumerateArray().Select(x => x.GetString()!).ToHashSet() : [];
        var strings = c.GetProperty("strings");
        string T(string key) => missing.Contains(key) ? string.Empty : strings.TryGetProperty(key, out var s) ? s.GetString()! : "T" + key;

        var langHref = c.GetProperty("multilang_params").GetProperty("lang_href").GetString()!;
        var enhanced = !c.TryGetProperty("enhanced", out var e) || e.GetBoolean();
        var ui = c.GetProperty("ui");
        var variants = c.GetProperty("variants").EnumerateArray().ToList();
        var comm = c.GetProperty("communications");
        string? siteKey = null;
        var logo = string.Empty;
        var hasProfile = c.TryGetProperty("site_profile", out var profile);
        if (hasProfile)
        {
            siteKey = Text(profile, "site_key");
            logo = Text(profile, "logo_url") ?? string.Empty;
        }

        var input = new StorefrontRegForm.Input
        {
            LoggedIn = c.GetProperty("user_id").GetInt32() != 0,
            AdditionalFields = c.GetProperty("fields").EnumerateArray()
                .Where(f => f.GetProperty("main_flag").GetString() == "0")
                .Select(f => new StorefrontRegForm.AdditionalField(
                    f.GetProperty("main_flag").GetString()!,
                    f.GetProperty("name").GetString()!,
                    T(f.GetProperty("caption").GetString()!),
                    f.GetProperty("show_for").GetString()!,
                    f.GetProperty("required_for").GetString()!,
                    f.GetProperty("maxlen").GetString()!,
                    f.GetProperty("regexp").GetString()!,
                    f.GetProperty("widget_type").GetString()!,
                    f.GetProperty("widget_options").GetString()!,
                    T(f.GetProperty("example").GetString()!)))
                .ToList(),
            Variants = variants.Select(v => new StorefrontRegForm.Variant(
                    v.GetProperty("id").GetString()!,
                    variants.Count == 1 ? v.GetProperty("caption").GetString()! : T(v.GetProperty("caption").GetString()!)))
                .ToList(),
            Communications = new StorefrontPhpAjax.Communications(
                comm.GetProperty("all").GetBoolean(),
                comm.GetProperty("sms").GetBoolean()),
            LangHref = langHref,
            CsrfGuardKey = c.GetProperty("session").ValueKind == JsonValueKind.Object ? c.GetProperty("session").GetProperty("csrf_guard_key").GetString()! : string.Empty,
            SocialBlock = enhanced
                ? EpcRegistrationEnhancedRender.SocialBlock(
                    true,
                    langHref,
                    Text(ui, "tenant_key"),
                    Text(c, "trade_name"),
                    Text(ui, "login_label"),
                    Text(c, "buttons") ?? string.Empty,
                    Text(ui, "send_code_url"),
                    Text(ui, "verify_code_url"))
                : string.Empty,
            Enhanced = enhanced,
            UsersAgreement = StorefrontAuthPartials.UsersAgreementModule(langHref, id => T(id.ToString())),
            OtpModal = new StorefrontOtpModal().Render(StorefrontRegForm.OtpModalOptions(StorefrontRegForm.OtpTenantKey(siteKey), logo)),
            MinPasswordLen = c.GetProperty("min_password_len").GetString()!,
            DomainPath = c.GetProperty("domain_path").GetString()!,
        };

        Assert.Equal(golden, StorefrontRegForm.Render(input, id => T(id.ToString())));
    }

    private static string? Text(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
