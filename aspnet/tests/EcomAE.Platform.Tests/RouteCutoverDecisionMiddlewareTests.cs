using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class RouteCutoverDecisionMiddlewareTests
{
    [Fact]
    public void MiddlewareDefinesStableDiagnosticHeaders()
    {
        Assert.Equal("X-EcomAE-Target-Runtime", RouteCutoverDecisionMiddleware.TargetRuntimeHeader);
        Assert.Equal("X-EcomAE-PHP-Fallback", RouteCutoverDecisionMiddleware.PhpFallbackHeader);
    }
}
