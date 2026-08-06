using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards live-tenant CP confidentiality: no guest-browse chrome; /cp ≡ /cp/control.
/// </summary>
public sealed class CpAuthGateParityTests
{
    [Fact]
    public void AdminGateMiddlewareIsWiredInProgram()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Program.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("AdminSurfaceAuthGateMiddleware", text, StringComparison.Ordinal);
        Assert.Contains("no guest-browse", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandCentreOwnsCpAndCpControl()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("@page \"/cp\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/control\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/app\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginFormHasNoGuestBrowseBypass()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/LegacyAdminLoginForm.razor");
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("Enter CP (no login)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("data-epc-guest-browse", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Browse the shell without credentials", text, StringComparison.Ordinal);
        // Wording is stack-neutral since the tenant UI hide-stack pass (no "Control"/framework names).
        Assert.Contains("Guest browse is disabled", text, StringComparison.Ordinal);
        Assert.Contains("disabled", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhpParityAuthGateEnforcesAdmin()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/PhpParityAuthGate.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("NavigateTo", text, StringComparison.Ordinal);
        Assert.Contains("RequireAdmin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Always render ChildContent", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceLockDisablesGuestBrowseShells()
    {
        var path = Find("docs/migration/evidence/presentation/classic-entry-aspnet-primary.json");
        var text = File.ReadAllText(path);
        Assert.Contains("\"guestBrowseShells\": false", text, StringComparison.Ordinal);
        Assert.Contains("\"adminAuthRequired\": true", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/cp", true)]
    [InlineData("/cp/control", true)]
    [InlineData("/cp/login", false)]
    public void RequiresAdminForControlSurfaces(string path, bool required)
    {
        Assert.Equal(required, AdminSurfaceAuthGateMiddleware.RequiresAdmin(path));
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
