using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class RouteCutoverDecisionMiddlewareTests
{
    [Fact]
    public void MiddlewareDefinesStableDiagnosticHeaders()
    {
        Assert.Equal("X-EcomAE-Platform", RouteCutoverDecisionMiddleware.TargetRuntimeHeader);
        Assert.Equal("X-EcomAE-Compat", RouteCutoverDecisionMiddleware.PhpFallbackHeader);
    }
}
