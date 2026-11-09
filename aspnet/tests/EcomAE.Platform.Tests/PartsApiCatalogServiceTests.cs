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
    public void BuildCacheKey_SortsParams()
    {
        var key = PartsApiCatalogService.BuildCacheKey(
            "getModels",
            new Dictionary<string, string> { ["modelId"] = "2", ["makeId"] = "1" });
        Assert.Equal("getmodels|makeId=1|modelId=2", key);
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
