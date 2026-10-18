using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlobalSearchAutofillTests
{
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

    [Fact]
    public void PageSearch_RejectsSavedLoginEmail()
    {
        var search = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpGlobalSearch.razor");
        var chrome = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");

        Assert.Contains("name=\"erp_page_search\"", search, StringComparison.Ordinal);
        Assert.Contains("value=\"\"", search, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"off\"", search, StringComparison.Ordinal);
        Assert.Contains("readonly=\"readonly\"", search, StringComparison.Ordinal);
        Assert.Contains("data-epc-anti-autofill=\"1\"", search, StringComparison.Ordinal);
        Assert.Contains("data-lpignore=\"true\"", search, StringComparison.Ordinal);
        Assert.Contains("data-1p-ignore=\"true\"", search, StringComparison.Ordinal);
        Assert.Contains("data-bwignore=\"true\"", search, StringComparison.Ordinal);
        Assert.Contains("data-form-type=\"other\"", search, StringComparison.Ordinal);
        Assert.Contains("looksLikeSavedEmail", search, StringComparison.Ordinal);
        Assert.Contains("epc-search-autofill", search, StringComparison.Ordinal);
        Assert.DoesNotContain("autocomplete=\"username\"", search, StringComparison.Ordinal);
        Assert.DoesNotContain("autocomplete=\"email\"", search, StringComparison.Ordinal);
        Assert.DoesNotContain("jew@", search, StringComparison.Ordinal);

        Assert.Contains("epc-search-autofill", chrome, StringComparison.Ordinal);
        Assert.Contains("name=\"erp_favourite_filter\"", chrome, StringComparison.Ordinal);
        Assert.Contains("data-epc-anti-autofill=\"1\"", chrome, StringComparison.Ordinal);
    }
}
