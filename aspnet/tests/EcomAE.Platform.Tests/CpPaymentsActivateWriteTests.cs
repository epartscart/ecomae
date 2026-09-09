using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPaymentsActivateWriteTests
{
    [Fact]
    public void Route_exposes_payments_write()
    {
        Assert.Equal("/cp/payments/write", EcomAeRoutes.CpPaymentsWrite);
    }

    [Fact]
    public void Handler_sanitize_matches_php_allowlist()
    {
        Assert.Equal("stripe", CpPaymentsWriteService.SanitizeHandler("stripe"));
        Assert.Equal("jazzcash_wallet", CpPaymentsWriteService.SanitizeHandler("jazzcash_wallet"));
        Assert.Equal("tripe", CpPaymentsWriteService.SanitizeHandler("Stripe!"));
        Assert.Equal(string.Empty, CpPaymentsWriteService.SanitizeHandler("!!!"));
        Assert.Equal("Stripe", CpPaymentsWriteService.HandlerTitle("stripe"));
        Assert.Equal("Jazzcash wallet", CpPaymentsWriteService.HandlerTitle("jazzcash_wallet"));
    }

    [Fact]
    public void Dry_run_still_blocks_secret_actions()
    {
        var confirmSave = new CpPaymentsWriteDryRun().Evaluate(new CpPaymentsWriteRequest("save_config", true));
        Assert.Equal(0, confirmSave.Writes);
        Assert.True(confirmSave.WritesBlocked);
        Assert.False(confirmSave.CutoverAllowed);
        Assert.Equal("confirm_writes_refused", confirmSave.ValidationCode);

        var activatePreview = new CpPaymentsWriteDryRun().Evaluate(new CpPaymentsWriteRequest("activate", false));
        Assert.Equal(0, activatePreview.Writes);
        Assert.True(activatePreview.WouldWrite);
        Assert.Equal("ok", activatePreview.ValidationCode);
    }

    [Fact]
    public void Page_posts_native_activate()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPaymentGatewaysApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/payments/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"activate\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"handler\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters_values", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("credentials_json", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_activate_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/payments/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_payments.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_config", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_activate_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPaymentsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpPaymentsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("ActivateAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPaymentsWriteService.cs"));
        Assert.Contains("epc_payment_set_active", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("SET `active` = 0", service, StringComparison.Ordinal);
        Assert.Contains("SET `active` = 1", service, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters_values", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
