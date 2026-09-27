using System.Collections.Generic;
using System.Text.Json;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPrintDocsDeskServiceTests
{
    private const string Description = """
    [
      {"name":"header_text","caption":"Header","type":"text","hint":"Shown on top"},
      {"name":"footer_text","caption":"Footer","type":"textarea"},
      {"name":"show_stamp","caption":"Stamp","type":"checkbox"},
      {"name":"logo","caption":"Logo","type":"image_file"},
      {"name":"profile","caption":"Profile","type":"user_profile_json_builder"}
    ]
    """;

    private const string Values = """
    {"header_text":"Tax Invoice","footer_text":"Thank you","show_stamp":1,"logo":"stamp.png","profile":{"phone":"1"}}
    """;

    [Fact]
    public void Parameters_pair_description_with_current_values()
    {
        var parameters = CpPrintDocsDeskService.BuildParameters(Description, Values);

        Assert.Equal(5, parameters.Count);
        Assert.Equal(["header_text", "footer_text", "show_stamp", "logo", "profile"], parameters.Select(p => p.Name));
        Assert.Equal("text", parameters[0].Type);
        Assert.Equal("Shown on top", parameters[0].Hint);
        Assert.Equal("Tax Invoice", parameters[0].Value);
        Assert.Equal("1", parameters[2].Value);
        Assert.Equal("stamp.png", parameters[3].Value);
        Assert.Equal("""{"phone":"1"}""", parameters[4].Value);
    }

    [Fact]
    public void Malformed_or_missing_json_degrades_to_empty()
    {
        Assert.Empty(CpPrintDocsDeskService.BuildParameters("not json", Values));
        Assert.Empty(CpPrintDocsDeskService.BuildParameters("", ""));
        Assert.Empty(CpPrintDocsDeskService.ReadValues("{oops"));

        var parameters = CpPrintDocsDeskService.BuildParameters(Description, "");
        Assert.Equal(5, parameters.Count);
        Assert.All(parameters, p => Assert.Equal("", p.Value));
    }

    [Fact]
    public void Merge_follows_php_type_rules()
    {
        var parameters = CpPrintDocsDeskService.BuildParameters(Description, Values);
        var submitted = new Dictionary<string, string>
        {
            ["header_text"] = "Tax Invoice <b>",
            ["footer_text"] = "Bye",
            ["logo"] = "",
            ["profile"] = """{"phone":"2"}""",
        };

        using var merged = JsonDocument.Parse(
            CpPrintDocsWriteService.MergeValues(parameters, submitted, []));
        var root = merged.RootElement;

        Assert.Equal("Tax Invoice &lt;b&gt;", root.GetProperty("header_text").GetString());
        Assert.Equal("Bye", root.GetProperty("footer_text").GetString());
        Assert.Equal(0, root.GetProperty("show_stamp").GetInt32());
        Assert.Equal("stamp.png", root.GetProperty("logo").GetString());
        Assert.Equal("2", root.GetProperty("profile").GetProperty("phone").GetString());
    }

    [Fact]
    public void Checked_checkbox_and_cleared_image_are_applied()
    {
        var parameters = CpPrintDocsDeskService.BuildParameters(Description, Values);
        var submitted = new Dictionary<string, string> { ["show_stamp"] = "1" };

        using var merged = JsonDocument.Parse(
            CpPrintDocsWriteService.MergeValues(parameters, submitted, ["logo"]));

        Assert.Equal(1, merged.RootElement.GetProperty("show_stamp").GetInt32());
        Assert.Equal("", merged.RootElement.GetProperty("logo").GetString());
    }
}
