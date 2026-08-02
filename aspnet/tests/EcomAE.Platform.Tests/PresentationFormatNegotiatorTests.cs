using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PresentationFormatNegotiatorTests
{
    [Fact]
    public void DefaultsToJsonWhenAcceptMissing()
    {
        var context = new DefaultHttpContext();
        Assert.Equal(PresentationFormat.Json, PresentationFormatNegotiator.Resolve(context.Request));
    }

    [Fact]
    public void HonorsExplicitFormatQuery()
    {
        var html = new DefaultHttpContext();
        html.Request.QueryString = new QueryString("?format=html");
        Assert.Equal(PresentationFormat.Html, PresentationFormatNegotiator.Resolve(html.Request));

        var json = new DefaultHttpContext();
        json.Request.QueryString = new QueryString("?format=json");
        json.Request.Headers.Accept = "text/html";
        Assert.Equal(PresentationFormat.Json, PresentationFormatNegotiator.Resolve(json.Request));
    }

    [Fact]
    public void ChoosesHtmlForBrowserAcceptWithoutJson()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";
        Assert.Equal(PresentationFormat.Html, PresentationFormatNegotiator.Resolve(context.Request));
    }

    [Fact]
    public void PrefersJsonWhenApplicationJsonIsAccepted()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = "application/json, text/html;q=0.9";
        Assert.Equal(PresentationFormat.Json, PresentationFormatNegotiator.Resolve(context.Request));
    }
}
