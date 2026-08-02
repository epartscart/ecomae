using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpDecommissionEvidenceTests
{
    [Fact]
    public void HasSurfaceDigestSmokeRequiresAuthenticatedDigest200()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var smoke = Path.Combine(root, "staging-smoke");
            Directory.CreateDirectory(smoke);

            File.WriteAllText(
                Path.Combine(smoke, "surface-digests-aspnet.json"),
                """{"ok":true,"routes":[{"route":"/migration/zero-php-completion","status":200},{"route":"/cp/dashboard-summary","status":401}]}""");
            Assert.False(PhpDecommissionEvidence.HasSurfaceDigestSmoke(root));

            File.WriteAllText(
                Path.Combine(smoke, "surface-digests-aspnet.json"),
                """{"ok":true,"authenticatedDigest200Count":1,"routes":[{"route":"/migration/zero-php-completion","status":200},{"route":"/cp/dashboard-summary","status":200}]}""");
            Assert.True(PhpDecommissionEvidence.HasSurfaceDigestSmoke(root));

            File.WriteAllText(
                Path.Combine(smoke, "surface-digests-aspnet.json"),
                """{"ok":false,"routes":[{"route":"/cp/dashboard-summary","status":200}]}""");
            Assert.False(PhpDecommissionEvidence.HasSurfaceDigestSmoke(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void HasAuthenticatedPriceLookupSmokeRejectsMissingApiKeyBodies()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae-price-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var smoke = Path.Combine(root, "staging-smoke");
            Directory.CreateDirectory(smoke);
            File.WriteAllText(
                Path.Combine(smoke, "price-lookup-aspnet.json"),
                """{"ok":false,"error":{"code":"missing_api_key","message":"x"}}""");
            Assert.False(PhpDecommissionEvidence.HasAuthenticatedPriceLookupSmoke(root));

            File.WriteAllText(
                Path.Combine(smoke, "price-lookup-aspnet.json"),
                """{"brand":"TOYOTA","article":"90919","offers":[]}""");
            Assert.True(PhpDecommissionEvidence.HasAuthenticatedPriceLookupSmoke(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
