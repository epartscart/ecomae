using EcomAE.Platform.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ProbeBypassMiddlewareTests
{
    [Theory]
    [InlineData("/health", "Healthy")]
    [InlineData("/ready", "ready")]
    public async Task Probe_Paths_ShortCircuit_WithoutCallingNext(string path, string needle)
    {
        var called = false;
        var mw = new ProbeBypassMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx, new StubEnv());
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.False(called);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.Contains(needle, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Other_Paths_PassThrough()
    {
        var called = false;
        var mw = new ProbeBypassMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/cp/app";
        await mw.InvokeAsync(ctx, new StubEnv());
        Assert.True(called);
    }

    private sealed class StubEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
