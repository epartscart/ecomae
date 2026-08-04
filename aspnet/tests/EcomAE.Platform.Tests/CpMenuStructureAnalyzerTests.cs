using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMenuStructureAnalyzerTests
{
    [Fact]
    public void AnalyzeEmptyStructureIsPresentFalse()
    {
        var summary = CpMenuStructureAnalyzer.Analyze("");
        Assert.False(summary.StructurePresent);
        Assert.True(summary.StructureParseOk);
        Assert.Equal(0, summary.NodeCount);
    }

    [Fact]
    public void AnalyzeCountsNestedUrlAndContentLinks()
    {
        const string json = """
            [
              {
                "value": "Home",
                "link_mode": "url",
                "url": "/",
                "children": [
                  { "value": "About", "link_mode": "content", "content_id": 12 },
                  { "value": "Shop", "link_mode": "url", "url": "/shop" }
                ]
              }
            ]
            """;

        var summary = CpMenuStructureAnalyzer.Analyze(json);
        Assert.True(summary.StructurePresent);
        Assert.True(summary.StructureParseOk);
        Assert.Equal(3, summary.NodeCount);
        Assert.Equal(2, summary.MaxDepth);
        Assert.Equal(2, summary.UrlLinkCount);
        Assert.Equal(1, summary.ContentLinkCount);
        Assert.Equal(0, summary.UnknownLinkCount);
    }

    [Fact]
    public void AnalyzeInvalidJsonMarksParseFailure()
    {
        var summary = CpMenuStructureAnalyzer.Analyze("{not-json");
        Assert.True(summary.StructurePresent);
        Assert.False(summary.StructureParseOk);
        Assert.Equal(0, summary.NodeCount);
    }
}
