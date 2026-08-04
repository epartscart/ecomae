using System.Text.RegularExpressions;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Safe basename allowlist for CP debug console temp logs (metadata-only; no LFI).
/// Mirrors PHP <c>Debug</c> class naming: <c>dmY_Hi.php</c> (e.g. 04082026_1805.php).
/// </summary>
public static partial class CpDebugConsoleAllowlist
{
    [GeneratedRegex(@"^\d{8}_\d{4}\.php$", RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex DebugTempFilePattern();

    public static bool IsAllowedBasename(string? basename)
    {
        return !string.IsNullOrWhiteSpace(basename) && DebugTempFilePattern().IsMatch(basename);
    }

    public static IReadOnlyList<string> ResolveTmpRootCandidates()
    {
        return
        [
            Path.Combine(Directory.GetCurrentDirectory(), "modules", "debug", "tmp"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "modules", "debug", "tmp"),
            "/workspace/modules/debug/tmp",
            "/var/www/modules/debug/tmp",
        ];
    }

    public static string? FindTmpRoot()
    {
        foreach (var candidate in ResolveTmpRootCandidates())
        {
            try
            {
                var full = Path.GetFullPath(candidate);
                if (Directory.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // ignore invalid paths
            }
        }

        return null;
    }
}
