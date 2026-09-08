using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EpcObtainModesTests
{
    [Fact]
    public void OnlyDiskHandlersAreOffered()
    {
        Assert.True(EpcObtainModes.HasCustomerInterface("get_in_office"));
        Assert.True(EpcObtainModes.HasCustomerInterface("epc_carriers"));
        Assert.False(EpcObtainModes.HasCustomerInterface("courier"));
        Assert.False(EpcObtainModes.HasCustomerInterface(""));
        Assert.Equal("get_in_officedrop", EpcObtainModes.SanitizeHandler("get_in_office!;drop"));
    }

    [Fact]
    public void ObtainModeCookieAcceptsClassicJsonNumber()
    {
        Assert.Equal(1, EpcObtainModes.ParseObtainModeCookie("1"));
        Assert.Equal(2, EpcObtainModes.ParseObtainModeCookie("2"));
        Assert.Equal(0, EpcObtainModes.ParseObtainModeCookie(""));
    }

    [Fact]
    public void HowGetCookieKeepsOfficeAndCarrier()
    {
        var office = EpcObtainModes.ParseHowGetCookie("{\"mode\":1,\"office_id\":7}");
        Assert.NotNull(office);
        Assert.Equal(1, office!.Mode);
        Assert.Equal(7, office.OfficeId);

        var encoded = Uri.EscapeDataString("{\"mode\":2,\"carrier\":\"dhl\",\"service\":\"EXPRESS\",\"city\":\"Dubai\",\"country\":\"AE\",\"weight_kg\":\"1.5\",\"rate\":57.75}");
        var carrier = EpcObtainModes.ParseHowGetCookie(encoded);
        Assert.NotNull(carrier);
        Assert.Equal("dhl", carrier!.Carrier);
        Assert.Equal("EXPRESS", carrier.Service);
        Assert.Equal(57.75m, carrier.Rate);
    }

    [Fact]
    public void DemoRateMatchesClassicPhpFormula()
    {
        Assert.Equal(57.75m, EpcObtainModes.DemoRate("dhl", 1.5m, "AE"));
        Assert.Equal(77.96m, EpcObtainModes.DemoRate("dhl", 1.5m, "US"));
        Assert.Equal("DHL Express", EpcObtainModes.CarrierName("dhl"));
        Assert.Equal("Express Worldwide", EpcObtainModes.ServiceName("dhl", "EXPRESS"));
    }
}
