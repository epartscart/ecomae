using System.Reflection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpConsolidationEntitySelectorParityTests
{
    [Fact]
    public void Consolidations_UsesPersistedEntityCodesForFigureAndIntercompanySelectors()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));

        Assert.Contains("name=\"entity_code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"from_entity\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"to_entity\"", text, StringComparison.Ordinal);
        Assert.Contains("_rows.Count == 0", text, StringComparison.Ordinal);
        Assert.Contains("IsEntitySelected(entity.Code)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Consolidations_PreservesPhpIntercompanyTransactionTypes()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));

        Assert.Contains("<option value=\"sale\">sale</option>", text, StringComparison.Ordinal);
        Assert.Contains("<option value=\"purchase\">purchase</option>", text, StringComparison.Ordinal);
        Assert.Contains("<option value=\"service\">service</option>", text, StringComparison.Ordinal);
        Assert.Contains("<option value=\"loan\">loan</option>", text, StringComparison.Ordinal);
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

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
