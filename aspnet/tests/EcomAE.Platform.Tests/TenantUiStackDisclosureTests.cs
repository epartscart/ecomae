using System.Text.RegularExpressions;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Production ERP / CP / BOS / storefront markup must not mention PHP, classic, or classical,
/// and must not expose /php-reference links. Existing sources stay on disk; new UI copy cannot
/// bring those words back.
/// </summary>
public sealed class TenantUiStackDisclosureTests
{
    private static readonly string ComponentsRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "EcomAE.Platform", "Components"));

    private static readonly Regex ForbiddenWord = new(
        @"\b(PHP|classic|classical|ASP\.NET)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] VisibleAttrs =
    [
        "title", "subtitle", "placeholder", "aria-label", "alt", "label",
        "aria-description", "aria-roledescription"
    ];

    [Fact]
    public void ProductSurfaceRazor_HasNoTenantVisibleStackDisclosure()
    {
        Assert.True(Directory.Exists(ComponentsRoot), $"missing {ComponentsRoot}");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ComponentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (IsOperatorConsole(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var visible = VisibleCopy(text);

            foreach (Match match in ForbiddenWord.Matches(visible))
            {
                offenders.Add($"{Rel(file)}: contains '{match.Value}'");
            }

            if (Regex.IsMatch(text, @"PhpReferenceOnlyHref\s*\(", RegexOptions.IgnoreCase)
                && !file.EndsWith("ErpModuleApp.razor", StringComparison.Ordinal)
                && !file.EndsWith("PhpHybridWorkspaceFrame.razor", StringComparison.Ordinal))
            {
                offenders.Add($"{Rel(file)}: product page still links PhpReferenceOnlyHref");
            }

            if (Regex.IsMatch(text, @"<a[^>]+href\s*=\s*[""']/php-reference/", RegexOptions.IgnoreCase))
            {
                offenders.Add($"{Rel(file)}: visible /php-reference link");
            }
        }

        Assert.True(offenders.Count == 0, "Stack disclosure in tenant UI:\n" + string.Join("\n", offenders.Distinct().Take(80)));
    }

    [Fact]
    public void RenderedPresentationCopy_HasNoTenantVisibleStackDisclosure()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            "aspnet/src/EcomAE.Platform/Presentation/CpPhpModuleCopy.cs",
            "aspnet/src/EcomAE.Platform/Presentation/ErpPhpTabChromeRows.cs",
            "aspnet/src/EcomAE.Platform/Presentation/ErpPhpModuleChromeCatalog.cs",
            "aspnet/src/EcomAE.Platform/Presentation/ErpJewelleryModuleChrome.cs",
            "aspnet/src/EcomAE.Platform/Presentation/LoginErrorHelper.cs",
            "aspnet/src/EcomAE.Platform/Presentation/OperatorGuideChapters.cs",
            "aspnet/src/EcomAE.Platform/Presentation/ErpGuideBooks.cs",
        };
        var offenders = new List<string>();
        foreach (var rel in files)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            text = Regex.Replace(text, @"//.*?$", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"///.*?$", "", RegexOptions.Multiline);
            foreach (Match match in ForbiddenWord.Matches(text))
            {
                offenders.Add($"{rel}: contains '{match.Value}'");
            }
        }

        Assert.True(offenders.Count == 0, "Stack disclosure in rendered presentation copy:\n" + string.Join("\n", offenders.Distinct().Take(40)));
    }

    private static bool IsOperatorConsole(string file)
    {
        var name = Path.GetFileName(file);
        return file.Contains($"{Path.DirectorySeparatorChar}Migration", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("Migration", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("ZeroPhp", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(ComponentsRoot);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs/governance/php-source-allowlist.txt")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }

    private string Rel(string file) => Path.GetRelativePath(ComponentsRoot, file);

    private static string VisibleCopy(string razor)
    {
        var text = Regex.Replace(razor, @"@\*.*?\*@", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<!--.*?-->", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"<script\b[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style\b[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"@code\s*\{.*\}\s*$", "", RegexOptions.Singleline);
        var attrs = new List<string>();
        foreach (var name in VisibleAttrs)
        {
            foreach (Match m in Regex.Matches(text, name + @"\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase))
            {
                attrs.Add(m.Groups[1].Value);
            }
        }

        text = Regex.Replace(text, @"<[^>]+>", " ");
        return string.Join(' ', attrs) + " " + text;
    }
}
