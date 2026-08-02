using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyApiClientKeyParserTests
{
    [Theory]
    [InlineData("epc_catalog_abc123", "catalog")]
    [InlineData("epc_pricepro_abc123", "price_pro")]
    public void ParseAcceptsPhpApiKeyPrefixes(string raw, string product)
    {
        var parsed = LegacyApiClientKeyParser.Parse(raw);

        Assert.NotNull(parsed);
        Assert.Equal(product, parsed.Product);
        Assert.Equal(64, parsed.Sha256Hash.Length);
    }

    [Fact]
    public void ParseRejectsUnknownPrefix()
    {
        Assert.Null(LegacyApiClientKeyParser.Parse("epc_test_key"));
    }

    [Fact]
    public void ParseRejectsInvalidCharactersAfterPrefix()
    {
        Assert.Null(LegacyApiClientKeyParser.Parse("epc_pricepro_bad-key!"));
    }

    [Fact]
    public void ExtractFromAuthorizationHeaderSupportsBearerKeys()
    {
        Assert.Equal("epc_catalog_abc123", LegacyApiClientKeyParser.ExtractFromAuthorizationHeader("Bearer epc_catalog_abc123"));
    }

    [Fact]
    public void ExtractFromRequestPrefersXApiKeyHeader()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "epc_pricepro_fromheader01";
        context.Request.Headers.Authorization = "Bearer epc_catalog_ignored01";

        Assert.Equal("epc_pricepro_fromheader01", LegacyApiClientKeyParser.ExtractFromRequest(context.Request));
    }
}
