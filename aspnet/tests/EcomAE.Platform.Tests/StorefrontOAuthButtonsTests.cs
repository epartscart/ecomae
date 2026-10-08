using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The social sign-in buttons against goldens recorded from the real <c>epc_oauth_buttons_render()</c> by
/// <c>Fixtures/OAuthButtons/harness.py</c>. Each case renders its configs on one page, joined with <c>|</c>; the random
/// root id is <c>XXXXXXXX</c>.
/// </summary>
public sealed class StorefrontOAuthButtonsTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OAuthButtons");

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
        var enabled = c.GetProperty("enabled").EnumerateArray().Select(e => e.GetString()!).ToList();
        string? cookie = c.TryGetProperty("cookies", out var cookies) && cookies.TryGetProperty("epc_oauth_last_google_email", out var v) ? v.GetString() : null;

        var buttons = new StorefrontOAuthButtons();
        var html = string.Join("|", c.GetProperty("configs").EnumerateArray().Select(cfg => buttons.Render(
            enabled,
            new StorefrontOAuthButtons.Options
            {
                Context = Text(cfg, "context"),
                TenantKey = Text(cfg, "tenant_key"),
                ReturnUrl = Text(cfg, "return_url"),
                Only = cfg.TryGetProperty("only", out var only) ? only.EnumerateArray().Select(o => o.GetString()!).ToList() : null,
                Divider = cfg.TryGetProperty("divider", out var divider) ? Truthy(divider) : null,
                Heading = Text(cfg, "heading"),
                RequireTerms = cfg.TryGetProperty("require_terms", out var terms) && Truthy(terms),
                TermsUrl = Text(cfg, "terms_url"),
                PrivacyUrl = Text(cfg, "privacy_url"),
            },
            cookie,
            "XXXXXXXX")));

        Assert.Equal(golden, html);
    }

    private static bool Truthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => value.GetDouble() != 0,
        _ => false,
    };

    private static string? Text(JsonElement cfg, string key) => cfg.TryGetProperty(key, out var value) ? value.GetString() : null;
}
