using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Existing PHP stays on disk (readyToRemovePhp=false). New PHP development is forbidden.
/// All new product work is ASP.NET Core only. Raising these constants is the review signal.
/// </summary>
public sealed class NewPhpDevelopmentBlockedTests
{
    public const int FrozenPhpFileCount = 3091;
    public const int FrozenAspNetPhpFileCount = 19;
    public const string FrozenPhpPathSha256 = "6a9e1a66b7c5ce640e7f5552ad7ac18eafc1718971e1030744804b3812f820da";

    [Fact]
    public void TrackedPhpFiles_MustStayOnFrozenAllowlist()
    {
        var root = FindRepoRoot();
        var allowlist = ReadAllowlist(root);
        Assert.Equal(FrozenPhpFileCount, allowlist.Count);

        var tracked = ListTrackedPhp(root);
        var extra = tracked.Except(allowlist, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var missing = allowlist.Except(tracked, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(extra.Count == 0, "New PHP files are forbidden. All new development is ASP.NET Core only.\n" + string.Join("\n", extra.Take(40)));
        Assert.True(missing.Count == 0, "Allowlist drifted from tracked PHP files.\n" + string.Join("\n", missing.Take(40)));

        var hash = Sha256Hex(string.Join('\n', allowlist) + "\n");
        Assert.Equal(FrozenPhpPathSha256, hash);
        Assert.Equal(FrozenAspNetPhpFileCount, allowlist.Count(p => p.StartsWith("aspnet/", StringComparison.Ordinal)));
    }

    [Fact]
    public void GatedDeploy_RunsPhpStackLeakGuardBeforePublish()
    {
        var root = FindRepoRoot();
        var deploy = File.ReadAllText(Path.Combine(root, "scripts/cloudpanel_deploy_main_gated.sh"));
        Assert.Contains("forbid_php_stack_leak.py", deploy, StringComparison.Ordinal);
        Assert.Contains("php_stack_leak_or_new_php", deploy, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "scripts/forbid_php_stack_leak.py")));
        Assert.True(File.Exists(Path.Combine(root, "docs/governance/php-source-allowlist.txt")));
        Assert.True(File.Exists(Path.Combine(root, ".cursor/rules/no-new-php-development.mdc")));
    }

    private static IReadOnlyList<string> ReadAllowlist(string root)
    {
        var path = Path.Combine(root, "docs/governance/php-source-allowlist.txt");
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith('/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> ListTrackedPhp(string root)
    {
        var psi = new ProcessStartInfo("git", "ls-files *.php")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("git ls-files failed to start");
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        Assert.Equal(0, proc.ExitCode);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs/governance/php-source-allowlist.txt"))
                && Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
