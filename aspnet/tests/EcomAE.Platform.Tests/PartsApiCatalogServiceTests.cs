using EcomAE.Package.AutoParts;
using Xunit;

namespace EcomAE.Platform.Tests;

public class PartsApiCatalogServiceTests
{
    [Theory]
    [InlineData("getMakes")]
    [InlineData("GETMAKES")]
    [InlineData("tecdocCrosses")]
    [InlineData("VINdecode")]
    [InlineData("getPartsbyVIN")]
    [InlineData("getCrossesTitle")]
    [InlineData("PartSuggest")]
    public void AllowedMethods_AcceptsCatalogReads(string method)
    {
        Assert.Contains(method, PartsApiCatalogService.AllowedMethods);
    }

    [Theory]
    [InlineData("dropTable")]
    [InlineData("setPrice")]
    [InlineData("")]
    public void AllowedMethods_RejectsUnknown(string method)
    {
        Assert.DoesNotContain(method, PartsApiCatalogService.AllowedMethods);
    }

    [Fact]
    public void BuildCacheKey_Sha1OfMethodAndCanonicalParams()
    {
        // Matches the existing docpart.epc_partsapi_cache producer:
        // sha1("getMakes|{\"carType\":\"CV\",\"lang\":\"en\"}")
        var key = PartsApiCatalogService.BuildCacheKey(
            "getMakes",
            new Dictionary<string, string> { ["carType"] = "CV", ["lang"] = "en" });
        Assert.Equal("bc045394446824ed7e95d4aeaecfb20c64a4f891", key);
    }

    [Fact]
    public void WrapBody_WrapsArrayPayloads()
    {
        var wrapped = PartsApiCatalogService.WrapBody("getMakes", "[{\"makeId\":1}]");
        Assert.Contains("\"ok\":true", wrapped);
        Assert.Contains("\"rows\":1", wrapped);
        Assert.Contains("\"action\":\"getmakes\"", wrapped);
    }

    [Fact]
    public void WrapBody_PassesThroughAlreadyWrapped()
    {
        const string body = "{\"ok\":true,\"rows\":2,\"data\":[1,2]}";
        Assert.Equal(body, PartsApiCatalogService.WrapBody("getMakes", body));
    }

    [Fact]
    public void ExtractProviderError_DetectsErrorCode()
    {
        var error = PartsApiCatalogService.ExtractProviderError(
            "{\"error_code\":5002,\"message\":\"Error when using the authorization key.\",\"status\":401}");
        Assert.Equal("Error when using the authorization key.", error);
    }

    [Fact]
    public void ExtractProviderError_DetectsCode9000()
    {
        var error = PartsApiCatalogService.ExtractProviderError(
            "{\"code\":9000,\"message\":\"Incoming query parameters are missing.\",\"status\":401}");
        Assert.Equal("Incoming query parameters are missing.", error);
    }

    [Fact]
    public void ExtractProviderError_PassesDataPayloads()
    {
        Assert.Null(PartsApiCatalogService.ExtractProviderError(
            "{\"status\":200,\"data\":[{\"MFA_ID\":\"1\",\"MFA_BRAND\":\"TOYOTA\"}]}"));
        Assert.Null(PartsApiCatalogService.ExtractProviderError("[{\"a\":1}]"));
    }

    [Fact]
    public void ExtractProviderError_FlagsNonJson()
    {
        Assert.NotNull(PartsApiCatalogService.ExtractProviderError("<html>oops</html>"));
    }
}
