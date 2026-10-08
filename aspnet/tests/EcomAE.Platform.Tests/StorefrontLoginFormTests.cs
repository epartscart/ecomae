using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The storefront login page against goldens recorded from the real <c>content/users/loginform.php</c> by
/// <c>Fixtures/LoginForm/harness.py</c> (stubbed <c>DP_User</c>, translations, login context and provider buttons; the
/// real <c>login_form_general.php</c>, password and e-mail code tabs, auth layout, auth links and e-mail code modal).
/// </summary>
public sealed class StorefrontLoginFormTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LoginForm");

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

        var hasStrings = c.TryGetProperty("strings", out var strings);
        string T(int id) => hasStrings && strings.TryGetProperty(id.ToString(System.Globalization.CultureInfo.InvariantCulture), out var s) ? s.GetString()! : "T" + id;

        var ui = c.GetProperty("ui");
        var buttons = c.GetProperty("buttons");
        var input = new StorefrontLoginForm.Input
        {
            UserId = c.GetProperty("user_id").GetInt64(),
            LangHref = c.GetProperty("multilang_params").GetProperty("lang_href").GetString()!,
            CsrfGuardKey = c.GetProperty("session").GetProperty("csrf_guard_key").GetString()!,
            Sms = c.GetProperty("communications").GetProperty("sms").GetBoolean(),
            SocialButtons = buttons.ValueKind == JsonValueKind.Null ? null : buttons.GetString(),
            TemplateId = c.GetProperty("template_id").GetInt64(),
            TenantKey = Text(ui, "tenant_key") ?? string.Empty,
            LoginLabel = Text(ui, "login_label") ?? "Shop",
            SendUrl = Text(ui, "send_code_url") ?? StorefrontOtpModal.DefaultSendUrl,
            VerifyUrl = Text(ui, "verify_code_url") ?? StorefrontOtpModal.VerifyCodeUrl,
            LogoUrl = c.TryGetProperty("site_profile", out var profile) ? Text(profile, "logo_url") ?? string.Empty : string.Empty,
        };

        string html;
        if (c.TryGetProperty("general", out var general))
        {
            var postfix = Text(general, "postfix");
            var target = Text(general, "target");
            var times = general.TryGetProperty("times", out var n) ? n.GetInt32() : 1;
            var parts = new System.Text.StringBuilder();
            for (var count = 1; count <= times; count++)
            {
                postfix = StorefrontLoginForm.Postfix(postfix, count);
                parts.Append(StorefrontLoginForm.General(input, T, postfix, target)).Append('|');
            }

            html = parts.ToString();
        }
        else
        {
            html = StorefrontLoginForm.Render(input, T);
        }

        if (html != golden)
        {
            var i = 0;
            while (i < html.Length && i < golden!.Length && html[i] == golden[i])
            {
                i++;
            }

            Assert.Fail(name + " differs at " + i + ": got " + JsonSerializer.Serialize(html.Substring(Math.Max(0, i - 60), Math.Min(140, html.Length - Math.Max(0, i - 60))))
                + " want " + JsonSerializer.Serialize(golden!.Substring(Math.Max(0, i - 60), Math.Min(140, golden.Length - Math.Max(0, i - 60)))));
        }
    }

    [Fact]
    public void Signup_url_and_ids_follow_php()
    {
        Assert.Equal("/en/users/registration", StorefrontLoginForm.SignupUrl("/en/"));
        Assert.Equal("/ar/users/registration", StorefrontLoginForm.SignupUrl("/ar"));
        Assert.Contains(4008, StorefrontLoginForm.StringIds);
    }

    private static string? Text(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
