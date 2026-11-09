using EcomAE.Package.AutoParts;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LaximoOemCatalogServiceTests
{
    [Theory]
    [InlineData("catalogs", "ListCatalogs")]
    [InlineData("wizard", "GetWizard2")]
    [InlineData("wizard_next", "GetWizardNextStep2")]
    [InlineData("categories", "GetCategories")]
    [InlineData("units", "ListUnits")]
    [InlineData("unit_details", "ListDetailByUnit")]
    [InlineData("quick_groups", "ListQuickGroup")]
    [InlineData("quick_details", "ListQuickDetail")]
    [InlineData("part_search", "FindDetailsByArticle")]
    [InlineData("part_refs", "ListApplicableReplacements")]
    [InlineData("applicability", "ListDetailApplicability")]
    [InlineData("aftermarket", "FindDetailsByArticle")]
    public void BuildCommand_MapsEveryDrillAction(string action, string expected)
    {
        var command = LaximoOemCatalogService.BuildCommand(
            new LaximoCatalogRequest(action, Catalog: "TOYOTA", Ssd: "abc+def/ghi=",
                VehicleId: "9", CategoryId: "3", UnitId: "44", GroupId: "7",
                Value: "x", Query: "filter", Oem: "90915"));

        Assert.NotNull(command);
        Assert.Contains(expected + ":", command, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCommand_PassesSsdVerbatim()
    {
        var command = LaximoOemCatalogService.BuildCommand(
            new LaximoCatalogRequest("quick_groups", Catalog: "TOYOTA", Ssd: "abc+def/ghi=", VehicleId: "9"));

        // ssd must pass verbatim — guayaquillib never re-encodes the token.
        Assert.NotNull(command);
        Assert.Contains("ssd=abc+def/ghi=", command, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCommand_UnknownAction_ReturnsNull()
        => Assert.Null(LaximoOemCatalogService.BuildCommand(new LaximoCatalogRequest("nope")));

    [Fact]
    public void BuildCommand_FindVehicle_UsesVinThenFrame()
    {
        var vin = LaximoOemCatalogService.BuildCommand(
            new LaximoCatalogRequest("find_vehicle", Vin: "WVWZZZ1JZXW000001"));
        var frame = LaximoOemCatalogService.BuildCommand(
            new LaximoCatalogRequest("find_vehicle", Vin: "ABC123"));

        Assert.Contains("FindVehicleByVIN", vin, StringComparison.Ordinal);
        Assert.Contains("VIN=WVWZZZ1JZXW000001", vin, StringComparison.Ordinal);
        Assert.Contains("FindVehicleByFrameNo", frame, StringComparison.Ordinal);
        Assert.Contains("FrameNo=ABC123", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void XmlToResult_ParsesAttributeRows_ToJsContract()
    {
        const string xml = """
            <?xml version="1.0"?><Response><GetUnits>
              <row UnitId="42" Name="Fuel pump" Code="FP1" ImageUrl="http://img/1.png" />
              <row unitid="43" name="Filter" />
            </GetUnits></Response>
            """;

        var result = LaximoOemCatalogService.XmlToResult(new LaximoCatalogRequest("units"), xml);

        Assert.True(result.Ok);
        Assert.Contains("\"success\":true", result.Json, StringComparison.Ordinal);
        Assert.Contains("\"unitid\":\"42\"", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fuel pump", result.Json, StringComparison.Ordinal);
        Assert.Contains("\"imageurl\"", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"oem\":\"FP1\"", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void XmlToResult_ParsesChildElementRows()
    {
        const string xml = """
            <Response><rows><row><code>TOYOTA</code><brand>Toyota</brand><name>Toyota</name></row></rows></Response>
            """;

        var result = LaximoOemCatalogService.XmlToResult(new LaximoCatalogRequest("catalogs"), xml);

        Assert.True(result.Ok);
        Assert.Contains("TOYOTA", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractFault_ReturnsProviderMessage()
    {
        const string faultXml = """
            <?xml version='1.0' encoding='UTF-8'?><soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"><soapenv:Body><soapenv:Fault><faultcode>soapenv:Server</faultcode><faultstring>E_ACCESSDENIED:Cannot find active user</faultstring></soapenv:Fault></soapenv:Body></soapenv:Envelope>
            """;

        Assert.Equal("E_ACCESSDENIED:Cannot find active user", LaximoOemCatalogService.ExtractFault(faultXml));
        Assert.Null(LaximoOemCatalogService.ExtractFault("<Response><row a=\"1\"/></Response>"));
        Assert.Null(LaximoOemCatalogService.ExtractFault("not xml at all"));
    }

    [Fact]
    public void XmlToResult_InvalidXml_ReturnsParseError()
    {
        var result = LaximoOemCatalogService.XmlToResult(new LaximoCatalogRequest("units"), "<unclosed");

        Assert.False(result.Ok);
        Assert.Contains("\"success\":false", result.Json, StringComparison.Ordinal);
    }
}
