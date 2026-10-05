using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Pins the PHP ai_query / ai_assistant_query port contract:
/// DI registration, live-gated catalog rows, verbatim SQL/intent anchors, no writes.
/// </summary>
public sealed class ErpAiReadPhpParityTests
{
    private static string RepoFile(params string[] parts)
        => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", Path.Combine(parts));

    [Fact]
    public void AiReadService_IsRegistered()
    {
        var program = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Program.cs"));
        Assert.Contains("IErpAiReadService, EcomAE.Platform.Erp.ErpAiReadService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_Rows_AreLiveGated()
    {
        var catalog = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        var aiQuery = catalog.IndexOf("\"/erp/ajax/ai-query\"", StringComparison.Ordinal);
        var aiAssistant = catalog.IndexOf("\"/erp/ajax/ai-assistant-query\"", StringComparison.Ordinal);
        Assert.True(aiQuery > 0 && aiAssistant > 0);
        Assert.Contains("write-live-gated", catalog.Substring(aiQuery, 1400), StringComparison.Ordinal);
        Assert.Contains("confirm_writes=true", catalog.Substring(aiQuery, 1400), StringComparison.Ordinal);
        Assert.Contains("write-live-gated", catalog.Substring(aiAssistant, 1400), StringComparison.Ordinal);
        Assert.Contains("confirm_writes=true", catalog.Substring(aiAssistant, 1400), StringComparison.Ordinal);
    }

    [Fact]
    public void Module_Handlers_UseTheLiveReadService()
    {
        var module = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Modules", "ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpAjaxAiQuery, HandleAiQueryAsync", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxAiAssistantQuery, HandleAiAssistantQueryAsync", module, StringComparison.Ordinal);
        Assert.Contains("IErpAiReadService", module, StringComparison.Ordinal);
        Assert.Contains("No question provided", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_PortsThePhpAnchors()
    {
        var service = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Erp", "ErpAiReadService.cs"));
        // ai_assistant_query verbatim anchors
        Assert.Contains("epc_erp_inv_items", service, StringComparison.Ordinal);
        Assert.Contains("jw_weight_on_hand", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_jw_repairs", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_sales_orders", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchase_orders", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_jw_weight_ledger", service, StringComparison.Ordinal);
        Assert.Contains("epc_org_company_settings", service, StringComparison.Ordinal);
        Assert.Contains("erp_industry_profile", service, StringComparison.Ordinal);
        // epc_bos_ai_answer anchors
        Assert.Contains("epc_erp_inv_movements", service, StringComparison.Ordinal);
        Assert.Contains("sale_out", service, StringComparison.Ordinal);
        Assert.Contains("transfer_out", service, StringComparison.Ordinal);
        Assert.Contains("return_out", service, StringComparison.Ordinal);
        Assert.Contains("Projected cash dips below zero within 3 months", service, StringComparison.Ordinal);
        Assert.Contains("No red flags detected", service, StringComparison.Ordinal);
        // Read-only twin: never creates schema or writes.
        Assert.DoesNotContain("INSERT INTO", service, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE `", service, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
    }
}
