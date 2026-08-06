using System.Text.RegularExpressions;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Tenants must not see stack names (PHP / ASP.NET) in product UI copy, link text, or meta.
/// </summary>
public sealed class TenantUiStackDisclosureTests
{
    private static readonly string ComponentsRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "EcomAE.Platform", "Components"));

    [Fact]
    public void ProductSurfaceRazor_HasNoTenantVisibleStackDisclosure()
    {
        Assert.True(Directory.Exists(ComponentsRoot), $"missing {ComponentsRoot}");

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ComponentsRoot, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Migration", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(file).StartsWith("Migration", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var visible = StripRazorCommentsAndCode(text);

            // Match tenant-readable stack words only (not C# identifiers like AspNetPrimaryHref).
            foreach (var phrase in new[]
                     {
                         "ASP.NET",
                         "PHP reference", "PHP archive", "PHP ERP", "Full PHP",
                         "PHP Command", "PHP-authoritative", "PHP is reference",
                         "PHP compare", "ASP.NET shell", "ASP.NET modules",
                         "ASP.NET BOS", "ASP.NET ERP", "ASP.NET Core",
                         "ecomae-php-chrome", "aspnet-php-assets",
                         "epc-moddir-aspnet", "epc-asp-login",
                     })
            {
                if (visible.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Rel(file)}: contains '{phrase}'");
                }
            }

            if (Regex.IsMatch(visible, @"<a[^>]+href\s*=\s*[""']/php-reference/", RegexOptions.IgnoreCase))
            {
                offenders.Add($"{Rel(file)}: visible /php-reference link");
            }
        }

        Assert.True(offenders.Count == 0, "Stack disclosure in tenant UI:\n" + string.Join("\n", offenders.Take(50)));
    }

    private string Rel(string file) => Path.GetRelativePath(ComponentsRoot, file);

    private static string StripRazorCommentsAndCode(string razor)
    {
        var noBlockComments = Regex.Replace(razor, @"@\*.*?\*@", "", RegexOptions.Singleline);
        var noCode = Regex.Replace(noBlockComments, @"@code\s*\{.*\}\s*$", "", RegexOptions.Singleline);
        return Regex.Replace(noCode, @"<!--.*?-->", "", RegexOptions.Singleline);
    }
}
