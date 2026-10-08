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
        if (c.TryGetProperty("offer", out var offer))
        {
            var config = new Dictionary<string, string>(StringComparer.Ordinal);
            if (offer.TryGetProperty("order_without_auth", out var setting))
            {
                config["order_without_auth"] = setting.GetString()!;
            }

            html = StorefrontCheckoutLoginOffer.Render(input, T, config);
        }
        else if (c.TryGetProperty("general", out var general))
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

    [Theory]
    [InlineData("1", true)]
    [InlineData(" 1", true)]
    [InlineData("1 ", true)]
    [InlineData("1.0", true)]
    [InlineData("01", true)]
    [InlineData("+1", true)]
    [InlineData("1e0", true)]
    [InlineData("10e-1", true)]
    [InlineData(".1e1", true)]
    [InlineData("1.", true)]
    [InlineData("\t1\n", true)]
    [InlineData("1.00000000000000001", true)]
    [InlineData("+.1E+1", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("yes", false)]
    [InlineData("1abc", false)]
    [InlineData("0x1", false)]
    [InlineData(" ", false)]
    [InlineData("1 1", false)]
    [InlineData("-1", false)]
    [InlineData("1e", false)]
    [InlineData("e1", false)]
    [InlineData("--1", false)]
    public void Order_without_auth_compares_like_php_loose_equals_one(string raw, bool expected)
    {
        Assert.Equal(expected, StorefrontCheckoutLoginOffer.LooseEqualsOne(raw));
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["order_without_auth"] = raw };
        Assert.Equal(expected, StorefrontCheckoutLoginOffer.GuestButtonShown(config));
        Assert.Equal(expected, StorefrontCheckoutLoginOffer.GuestOrdersAllowed(config));
    }

    [Fact]
    public void Unset_order_without_auth_hides_the_button_but_still_allows_guest_orders()
    {
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);
        Assert.False(StorefrontCheckoutLoginOffer.GuestButtonShown(empty));
        Assert.True(StorefrontCheckoutLoginOffer.GuestOrdersAllowed(empty));
    }

    [Theory]
    [InlineData("/shop/checkout/login_offer", true)]
    [InlineData("/en/shop/checkout/login_offer", true)]
    [InlineData("/ar/shop/checkout/login_offer/", true)]
    [InlineData("/EN/shop/checkout/login_offer", false)]
    [InlineData("/eng/shop/checkout/login_offer", false)]
    [InlineData("/en/shop/checkout/how_get", false)]
    [InlineData("/en/users/login", false)]
    public void Login_offer_posts_are_handled_on_the_offer_page(string path, bool expected)
        => Assert.Equal(expected, StorefrontLoginPostMiddleware.IsLoginOfferPath(new Microsoft.AspNetCore.Http.PathString(path)));

    [Fact]
    public void Login_offer_target_continues_to_the_delivery_step()
    {
        Assert.Equal("/", StorefrontLoginPost.SafeTarget(StorefrontCheckoutLoginOffer.Target));
        Assert.Equal("/en/shop/checkout/how_get", StorefrontLoginPost.SafeTarget(StorefrontLoginPostMiddleware.LoginOfferTarget("en", true)));
        Assert.Equal("/shop/checkout/how_get", StorefrontLoginPost.SafeTarget(StorefrontLoginPostMiddleware.LoginOfferTarget("en", false)));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", false)]
    [InlineData("yes", true)]
    public async Task Guest_checkout_create_follows_config_php_order_without_auth(string value, bool refused)
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["order_without_auth"] = value };
        if (refused)
        {
            var result = await StorefrontPhpAjax.CheckoutCreateAsync(null!, 0, 0, null, null, null, null, CancellationToken.None, null, config);
            var status = Assert.IsType<StorefrontPhpAjax.ShopStatus>(result);
            Assert.Equal("4470", status.Message);
        }
        else
        {
            Assert.True(StorefrontCheckoutLoginOffer.GuestOrdersAllowed(config));
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
