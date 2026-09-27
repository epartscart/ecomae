using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinanceRouteTests
{
    [Fact]
    public void FinanceAliasUsesTheImplementedErpDashboard()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));

        Assert.Contains("@page \"/erp/finance-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ERP Finance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("migration placeholder", text, StringComparison.OrdinalIgnoreCase);
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

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
