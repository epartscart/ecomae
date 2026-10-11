using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PlatformOperatorIdentityTests
{
    [Fact]
    public void Canonical_super_admin_is_yawer_and_keeps_gmail_as_alias()
    {
        Assert.Equal("yawer@ecomae.com", PlatformOperatorIdentity.CanonicalEmail);
        Assert.Equal("ecomaedxb@gmail.com", PlatformOperatorIdentity.LegacyEmail);
        Assert.True(PlatformOperatorIdentity.IsAlias("YAWER@ecomae.com"));
        Assert.True(PlatformOperatorIdentity.IsAlias("ecomaedxb@gmail.com"));
        Assert.False(PlatformOperatorIdentity.IsAlias("ops@acme.test"));
        Assert.Equal("yawer@ecomae.com", PlatformOperatorIdentity.Canonicalize("ecomaedxb@gmail.com"));
        Assert.Equal(["yawer@ecomae.com", "ecomaedxb@gmail.com"], PlatformOperatorIdentity.LookupEmails("ecomaedxb@gmail.com"));
        Assert.Contains("super-cp", PlatformOperatorIdentity.Surfaces);
        Assert.Contains("super-bos", PlatformOperatorIdentity.Surfaces);
        Assert.Contains("super-erp", PlatformOperatorIdentity.Surfaces);
    }

    [Fact]
    public void Login_sql_still_matches_confirmed_unlocked_email()
    {
        Assert.Contains("LOWER(`email`) = LOWER(@contact)", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        Assert.Contains("`email_confirmed` = 1", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        Assert.Contains("`unlocked` = 1", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        var login = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Auth/DbLegacyAdminLoginService.cs"));
        Assert.Contains("PlatformOperatorIdentity.IsAlias", login, StringComparison.Ordinal);
        Assert.Contains("SucceedOnConnectionAsync", login, StringComparison.Ordinal);
        Assert.Contains("superCpHost", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Succession_never_rewrites_the_password_column()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Auth/PlatformOperatorSuccessionService.cs"));
        Assert.Contains("SET `email` = @email", src, StringComparison.Ordinal);
        Assert.Contains("AND `password` = @hash", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SET `password`", src, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordUnchanged = false", src, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
