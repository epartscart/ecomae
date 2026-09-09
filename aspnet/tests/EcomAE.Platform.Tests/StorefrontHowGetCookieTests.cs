using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontHowGetCookieTests
{
    [Fact]
    public void Missing_cookie_stores_mode_and_office()
    {
        var json = StorefrontHowGetCookie.BuildHowGetJson(1, 12, null);
        Assert.Contains("\"mode\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"office_id\":12", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Carrier_cookie_keeps_address_and_rate()
    {
        var cookie = """{"mode":2,"carrier":"DHL","city":"Dubai","address":"Shed 4 <b>","rate":42.5,"delivery_price":42.5}""";
        var json = StorefrontHowGetCookie.BuildHowGetJson(2, 0, cookie);
        Assert.Contains("\"mode\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"carrier\":\"DHL\"", json, StringComparison.Ordinal);
        Assert.Contains("\"city\":\"Dubai\"", json, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", json, StringComparison.Ordinal);
        Assert.Contains("42.5", json, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_mode_wins_over_cookie_mode()
    {
        var json = StorefrontHowGetCookie.BuildHowGetJson(1, 9, """{"mode":99,"office_id":3}""");
        Assert.Contains("\"mode\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"office_id\":9", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_or_oversized_cookie_falls_back()
    {
        Assert.Contains("\"mode\":1", StorefrontHowGetCookie.BuildHowGetJson(1, 0, "not-json"), StringComparison.Ordinal);
        Assert.Equal(0, StorefrontHowGetCookie.ReadInt("not-json", "mode"));
        Assert.Equal(2, StorefrontHowGetCookie.ReadInt("""{"mode":"2"}""", "mode"));
        Assert.Equal(0, StorefrontHowGetCookie.ReadInt(new string('x', StorefrontHowGetCookie.MaxLength + 1), "mode"));
    }

    [Fact]
    public void Module_reads_how_get_cookie_on_create()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontHowGetCookie.ReadRaw", module, StringComparison.Ordinal);
        Assert.Contains("howGetJson", module, StringComparison.Ordinal);
        Assert.Contains("how_get_json", module, StringComparison.Ordinal);
        Assert.Contains("ready-for-confirm", module, StringComparison.Ordinal);
        Assert.DoesNotContain("Obtain/confirm/payment writes remain PHP", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_service_uses_cookie_builder()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Storefront/StorefrontCheckoutWriteService.cs"));
        Assert.Contains("StorefrontHowGetCookie.BuildHowGetJson", src, StringComparison.Ordinal);
        Assert.Contains("HowGetCookieJson", src, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
