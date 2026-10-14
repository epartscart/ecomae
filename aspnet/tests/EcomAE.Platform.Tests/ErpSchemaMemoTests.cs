using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSchemaMemoTests
{
    [Fact]
    public void DatabaseKey_SkipsWhenTheDatabaseIsUnknown()
    {
        Assert.Equal(string.Empty, ErpSchemaMemo.DatabaseKey("db.local", "  ", "company-context"));
        Assert.Equal(string.Empty, ErpSchemaMemo.DatabaseKey("db.local", "shop", ""));
    }

    [Fact]
    public void Remember_SkipsTheSameDatabaseOnTheNextCall()
    {
        var key = ErpSchemaMemo.DatabaseKey("db.local", "shop-speed-" + Guid.NewGuid().ToString("N"), "company-context");
        Assert.False(ErpSchemaMemo.IsReady(key));
        ErpSchemaMemo.Remember(key);
        Assert.True(ErpSchemaMemo.IsReady(key));
        ErpSchemaMemo.Remember(key);
        Assert.True(ErpSchemaMemo.IsReady(key));
    }

    [Fact]
    public void CompanyContext_ReturnsBeforeDdlWhenAlreadyReady()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        var start = text.IndexOf("public static async Task EnsureCompanyContextAsync", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = text[start..(start + 500)];
        Assert.Contains("ErpSchemaMemo.IsReady", body, StringComparison.Ordinal);
        Assert.Contains("return;", body, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatCreateTable_IsSkippedAfterSuccess()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpDb.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS", text, StringComparison.Ordinal);
        Assert.Contains("ErpSchemaMemo.IsReady", text, StringComparison.Ordinal);
        Assert.Contains("ErpSchemaMemo.Remember", text, StringComparison.Ordinal);
    }

    private static string Find(string relative)
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
