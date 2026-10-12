using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ClientErpPathTests
{
    [Theory]
    [InlineData("/cp/client-erp", "", "", true, false, false)]
    [InlineData("/cp/client-erp/", "", "", true, false, false)]
    [InlineData("/cp/client-erp/asap", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/asap/", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/ASAP!/", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/asap/erp", "asap", "erp", false, false, true)]
    [InlineData("/cp/client-erp/asap/erp/app", "asap", "erp/app", false, false, true)]
    public void Parse_MirrorsPhpRouter(string path, string key, string rest, bool bare, bool login, bool erp)
    {
        Assert.True(ClientErpPath.TryParse(path, out var parsed));
        Assert.Equal(key, parsed.SiteKey);
        Assert.Equal(rest, parsed.Rest);
        Assert.Equal(bare, parsed.IsBare);
        Assert.Equal(login, parsed.IsLoginRoot);
        Assert.Equal(erp, parsed.IsErpShell);
    }

    [Theory]
    [InlineData("/cp")]
    [InlineData("/cp/login")]
    [InlineData("/erp")]
    [InlineData("/erp/login")]
    public void Parse_RejectsSuperSurfaces(string path)
    {
        Assert.False(ClientErpPath.TryParse(path, out _));
        Assert.False(AdminSurfaceAuthGateMiddleware.IsClientErpLoginPath(path));
    }

    [Fact]
    public void AuthGate_AllowsPickerAndCompanyLogin_NotErpShell()
    {
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp"));
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap"));
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/"));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/erp"));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/erp/app"));
    }

    [Fact]
    public void PrefixErpPath_KeepsChromeOnClientPrefix()
    {
        Assert.Equal("/cp/client-erp/asap/", ClientErpPath.PrefixErpPath("asap", "/erp/login"));
        Assert.Equal("/cp/client-erp/asap/erp", ClientErpPath.PrefixErpPath("asap", "/erp"));
        Assert.Equal("/cp/client-erp/asap/erp/app", ClientErpPath.PrefixErpPath("asap", "/erp/app"));
    }

    [Fact]
    public void ForErpOnly_IsCompanyMark_NotSuperCp()
    {
        var brand = LoginHostBrand.ForErpOnly("asap", "ASAP Autoparts", "auto_parts");
        Assert.Equal(LoginHostBrand.Kind.CompanyMark, brand.LogoKind);
        Assert.Equal("asap", brand.SiteKey);
        Assert.Equal("ASAP Autoparts", brand.Label);
        Assert.DoesNotContain("Operator Access", LoginIndustryContent.For(brand, "erp").FormTitle, StringComparison.Ordinal);
        Assert.Contains("Sign in to ERP", LoginIndustryContent.For(brand, "erp").FormTitle, StringComparison.Ordinal);
    }
}
