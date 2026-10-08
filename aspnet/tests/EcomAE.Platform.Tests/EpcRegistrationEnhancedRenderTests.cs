using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The render half of the enhanced registration form against goldens recorded from the real PHP render functions by
/// <c>Fixtures/RegistrationEnhancedRender/harness.py</c> (stubbed auth core, login context and provider buttons).
/// </summary>
public sealed class EpcRegistrationEnhancedRenderTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RegistrationEnhancedRender");

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

        var html = c.GetProperty("fn").GetString() switch
        {
            "social" => Social(c),
            "country" => Country(c),
            "tabs" => EpcRegistrationEnhancedRender.AccountTabs(),
            "uae" => EpcRegistrationEnhancedRender.UaePanel(),
            var fn => throw new InvalidOperationException(fn),
        };

        Assert.Equal(golden, html);
    }

    private static string Social(JsonElement c)
    {
        var ui = c.GetProperty("ui");
        return EpcRegistrationEnhancedRender.SocialBlock(
            !c.TryGetProperty("auth_available", out var auth) || auth.GetBoolean(),
            Text(c.GetProperty("params"), "lang_href"),
            Text(ui, "tenant_key"),
            Text(c, "trade_name"),
            Text(ui, "login_label"),
            Text(c, "buttons") ?? string.Empty,
            Text(ui, "send_code_url"),
            Text(ui, "verify_code_url"));
    }

    private static string Country(JsonElement c)
    {
        var args = c.GetProperty("args").EnumerateArray().Select(a => a.GetString()!).ToArray();
        return args.Length switch
        {
            0 => EpcRegistrationEnhancedRender.CountrySelect(),
            1 => EpcRegistrationEnhancedRender.CountrySelect(args[0]),
            2 => EpcRegistrationEnhancedRender.CountrySelect(args[0], args[1]),
            _ => EpcRegistrationEnhancedRender.CountrySelect(args[0], args[1], args[2]),
        };
    }

    private static string? Text(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
