using System.Text.RegularExpressions;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpShellCssScriptLeakTests
{
    private static readonly Regex Rule = new(@"([^{}]+)\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex ForcedDisplay = new(@"display\s*:\s*(?!none)[\w-]+[^;]*!important", RegexOptions.Compiled);
    private static readonly Regex EndsInWildcard = new(@"(^|[\s>+~])\*(?<nots>(:not\([^)]*\))*)\s*$", RegexOptions.Compiled);

    [Fact]
    public void Forced_display_wildcards_do_not_reveal_inline_scripts_as_text()
    {
        var root = FindRepoRoot();
        var files = new[] { "cp/templates", "content/general_pages", "css", "aspnet/src/EcomAE.Platform/wwwroot" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                        || (f.EndsWith(".php", StringComparison.OrdinalIgnoreCase) && File.ReadAllText(f).Contains("<style", StringComparison.OrdinalIgnoreCase)))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var leaks = new List<string>();
        foreach (var file in files)
        {
            var css = Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match rule in Rule.Matches(css))
            {
                if (!ForcedDisplay.IsMatch(rule.Groups[2].Value))
                {
                    continue;
                }

                foreach (var selector in rule.Groups[1].Value.Split(','))
                {
                    var m = EndsInWildcard.Match(selector.Trim());
                    if (m.Success && !m.Groups["nots"].Value.Contains("script", StringComparison.OrdinalIgnoreCase))
                    {
                        leaks.Add($"{Path.GetRelativePath(root, file)}: {selector.Trim()}");
                    }
                }
            }
        }

        Assert.True(leaks.Count == 0, string.Join("\n", leaks));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
