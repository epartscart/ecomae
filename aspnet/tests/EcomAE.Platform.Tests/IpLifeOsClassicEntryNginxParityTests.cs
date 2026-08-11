using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// /ip/login and /lifeos must exact-route to ASP.NET on Super-CP classic-entry.
/// Live miss left them on PHP index.php → TemporarilyDeactivatePhpServing 503 splash.
/// Tenants must 404 IP/LifeOS (same isolation as BOS).
/// </summary>
public sealed class IpLifeOsClassicEntryNginxParityTests
{
    [Theory]
    [InlineData("location = /ip/login")]
    [InlineData("location = /IP/login")]
    [InlineData("location = /ip")]
    [InlineData("location = /lifeos")]
    [InlineData("location = /lifeos/login")]
    [InlineData("location ^~ /ip/")]
    [InlineData("location ^~ /lifeos/")]
    [InlineData("classic-entry-ip-login")]
    [InlineData("classic-entry-lifeos-login")]
    public void WwwClassicEntry_ProxiesIpAndLifeOsToAspNet(string needle)
    {
        var text = Read("deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf");
        Assert.Contains(needle, text, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://127.0.0.1:5100", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("location = /ip/login")]
    [InlineData("location = /lifeos/login")]
    [InlineData("location ^~ /ip/")]
    [InlineData("location ^~ /lifeos/")]
    public void TenantClassicEntry_DeniesIpAndLifeOs(string locationLine)
    {
        var text = Read("deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf");
        Assert.Contains(locationLine, text, StringComparison.Ordinal);
        var idx = text.IndexOf(locationLine, StringComparison.Ordinal);
        Assert.True(idx >= 0);
        var window = text.Substring(idx, Math.Min(280, text.Length - idx));
        Assert.Contains("return 404", window, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicEntryEvidence_ListsIpLifeOsLoginBridges()
    {
        var text = Read("docs/migration/evidence/presentation/classic-entry-aspnet-primary.json");
        Assert.Contains("\"/ip/login\"", text, StringComparison.Ordinal);
        Assert.Contains("\"/lifeos/login\"", text, StringComparison.Ordinal);
        Assert.Contains("\"/ip\"", text, StringComparison.Ordinal);
        Assert.Contains("\"/lifeos\"", text, StringComparison.Ordinal);
        Assert.Contains("\"cutoverAllowed\": false", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("location ^~ /en/")]
    [InlineData("location ^~ /me/")]
    [InlineData("location ^~ /ru/")]
    [InlineData("location ^~ /parts/")]
    [InlineData("classic-entry-en-lang-tree")]
    public void TenantClassicEntry_ProxiesEntireLangTreeToAspNet(string needle)
    {
        var text = Read("deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf");
        Assert.Contains(needle, text, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://127.0.0.1:5100", text, StringComparison.Ordinal);
        Assert.Contains("Product browser must NEVER render PHP nero", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassicEntryEvidence_ListsEpartscartLangTreeAspNet()
    {
        var text = Read("docs/migration/evidence/presentation/classic-entry-aspnet-primary.json");
        Assert.Contains("\"/en/\"", text, StringComparison.Ordinal);
        Assert.Contains("storefrontLangTreeAspNet", text, StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
