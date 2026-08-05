using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LoginBridgeSecretSyncTests
{
    [Fact]
    public void BridgeNotConfiguredMessagePointsAtPhpSyncHelper()
    {
        var msg = LoginErrorHelper.FromUri("https://www.ecomae.com/bos/login?error=bridge_not_configured");
        Assert.NotNull(msg);
        Assert.Contains("cloudpanel_sync_secret_succession_from_php.sh", msg, StringComparison.Ordinal);
        Assert.Contains("same PHP admin", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SyncScriptsExistInRepo()
    {
        Assert.True(File.Exists(FindRepoFile("scripts/cloudpanel_sync_secret_succession_from_php.sh")));
        Assert.True(File.Exists(FindRepoFile("scripts/php/sync_secret_succession_to_platform_env.php")));
        var sh = File.ReadAllText(FindRepoFile("scripts/cloudpanel_sync_secret_succession_from_php.sh"));
        Assert.Contains("ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION", sh, StringComparison.Ordinal);
        Assert.Contains("never prints", sh, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
