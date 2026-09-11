using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosBillingCreatePlanWriteTests
{
    [Fact]
    public void Route_exposes_create_plan()
    {
        Assert.Equal("/bos/billing/create-plan", EcomAeRoutes.BosBillingCreatePlan);
    }

    [Fact]
    public void Php_plan_data_matches_php_defaults()
    {
        var missing = BosBillingWriteService.ParsePlanData(null);
        Assert.Equal("", missing.PlanCode);
        Assert.Equal("monthly", missing.BillingCycle);
        Assert.Equal(0m, missing.BasePrice);
        Assert.Equal("AED", missing.Currency);
        Assert.Equal(0L, missing.TrialDays);
        Assert.Equal(0m, missing.SetupFee);
        Assert.Equal("[]", missing.FeaturesJson);
        Assert.Equal("[]", missing.UsageLimitsJson);

        var invalid = BosBillingWriteService.ParsePlanData("not-json");
        Assert.Equal("monthly", invalid.BillingCycle);
        Assert.Equal("AED", invalid.Currency);

        var blankPrice = BosBillingWriteService.ParsePlanData("{\"base_price\":\"\"}");
        Assert.Equal(0m, blankPrice.BasePrice);

        var parsed = BosBillingWriteService.ParsePlanData(
            "{\"plan_code\":\"pro-1\",\"name\":\"Pro\",\"description\":\"Full\",\"billing_cycle\":\"annual\",\"base_price\":\"99.5\",\"currency\":\"USD\",\"trial_days\":\"14days\",\"setup_fee\":\"10.25\",\"features\":[\"sso\"],\"usage_limits\":{\"seats\":5}}");
        Assert.Equal("PRO-1", parsed.PlanCode);
        Assert.Equal("Pro", parsed.Name);
        Assert.Equal("Full", parsed.Description);
        Assert.Equal("annual", parsed.BillingCycle);
        Assert.Equal(99.5m, parsed.BasePrice);
        Assert.Equal("USD", parsed.Currency);
        Assert.Equal(14L, parsed.TrialDays);
        Assert.Equal(10.25m, parsed.SetupFee);
        Assert.Equal("[\"sso\"]", parsed.FeaturesJson);
        Assert.Contains("seats", parsed.UsageLimitsJson, StringComparison.Ordinal);

        var nested = BosBillingWriteService.ParsePlanData("{\"features\":\"[\\\"api\\\"]\"}");
        Assert.Equal("[\"api\"]", nested.FeaturesJson);
    }

    [Fact]
    public void Page_posts_native_create_plan()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/billing/create-plan\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"plan_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"billing_cycle\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_plan_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/billing/create-plan");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_billing_create_plan", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_billing_plans", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_plan_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosBillingCreatePlan", module, StringComparison.Ordinal);
        Assert.Contains("CreatePlanAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosBillingWriteService.cs"));
        Assert.Contains("epc_billing_cancel", service, StringComparison.Ordinal);
        Assert.Contains("epc_billing_record_payment", service, StringComparison.Ordinal);
        Assert.Contains("epc_billing_create_plan", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_billing_plans`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
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
