using System.Text.Json;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The e-mail code modal against goldens recorded from the real <c>epc_otp_modal_render()</c> by
/// <c>Fixtures/OtpModal/harness.py</c>. Each case renders its configs in order on one page, joined with <c>|</c>.
/// </summary>
public sealed class StorefrontOtpModalTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OtpModal");

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
        var configs = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "cases.json"))).RootElement.GetProperty(name);
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir, "goldens.json"))).RootElement.GetProperty(name).GetString();

        var modal = new StorefrontOtpModal();
        var html = string.Join("|", configs.EnumerateArray().Select(cfg => modal.Render(new StorefrontOtpModal.Options
        {
            ModalId = Text(cfg, "modal_id"),
            Context = Text(cfg, "context"),
            TenantKey = Text(cfg, "tenant_key"),
            SendUrl = Text(cfg, "send_url"),
            VerifyUrl = Text(cfg, "verify_url"),
            ReturnUrl = Text(cfg, "return_url"),
            LogoUrl = Text(cfg, "logo_url"),
            Label = Text(cfg, "label"),
            OnSuccess = Text(cfg, "on_success"),
            VerifyOnly = cfg.TryGetProperty("verify_only", out var v) && v.GetBoolean(),
        })));

        Assert.Equal(golden, html);
    }

    private static string? Text(JsonElement cfg, string key) => cfg.TryGetProperty(key, out var value) ? value.GetString() : null;
}
