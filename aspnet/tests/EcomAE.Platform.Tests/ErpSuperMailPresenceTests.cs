using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSuperMailPresenceTests
{
    [Fact]
    public void SummarizeLogins_CountsPeopleNotSessions_AndKeepsLastLoginOnly()
    {
        var presence = ErpTenantUserAdminService.SummarizeLogins(
        [
            Person(1, "ada@tenant.test", "Ada", openSessions: 2, lastVisit: 1_700_000_000),
            Person(2, "bo@tenant.test", "", openSessions: 0, lastVisit: 1_600_000_000),
            Person(3, "cy@tenant.test", "Cy", openSessions: 1, lastVisit: 0),
        ]);

        Assert.Equal(3, presence.UserCount);
        Assert.Equal(2, presence.SignedInCount);
        Assert.Equal(1_700_000_000, presence.LatestLoginUnix);
        Assert.Equal("tenant-users", presence.Source);
        Assert.Equal("", presence.Message);
        Assert.Equal(["Ada", "Cy", "bo@tenant.test"], presence.People.Select(row => row.Name).ToArray());
        Assert.True(presence.People[0].SignedIn);
        Assert.False(presence.People[2].SignedIn);
        Assert.Equal("bo@tenant.test", presence.People[2].Email);
    }

    [Fact]
    public void SummarizeLogins_EmptyCompany_SaysNobodyCanSignIn()
    {
        var presence = ErpTenantUserAdminService.SummarizeLogins([]);
        Assert.Equal(0, presence.UserCount);
        Assert.Equal(0, presence.SignedInCount);
        Assert.Equal(0, presence.LatestLoginUnix);
        Assert.Empty(presence.People);
        Assert.Contains("No people can sign in", presence.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperMailPage_ShowsUserCountSignedInAndLastLoginOnly()
    {
        var razor = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSuperMailApp.razor"));
        Assert.Contains("LoginPresenceAsync", razor, StringComparison.Ordinal);
        Assert.Contains("Who is signed in", razor, StringComparison.Ordinal);
        Assert.Contains("last 8 hours", razor, StringComparison.Ordinal);
        Assert.Contains(">Users<", razor, StringComparison.Ordinal);
        Assert.Contains("Signed in now", razor, StringComparison.Ordinal);
        Assert.Contains(">Last login<", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Platform mailboxes", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.Contains("ErpOnlyLaunchClients.SuperMailbox", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("phone", razor, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", razor, StringComparison.OrdinalIgnoreCase);
    }

    private static ErpTenantUserRow Person(long id, string email, string name, int openSessions, long lastVisit)
        => new(
            id,
            email,
            "0500000000",
            name,
            "Sales",
            "sales",
            "Sales",
            true,
            true,
            false,
            openSessions,
            lastVisit,
            1);

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

        throw new DirectoryNotFoundException(relative);
    }
}
