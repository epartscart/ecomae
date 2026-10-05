using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Pins the PHP einvoice_poll_asp port contract (epc_einvoice_poll_all_pending):
/// DI registration, live-gated catalog row, verbatim status transitions/backoff/log anchors.
/// </summary>
public sealed class ErpEinvoiceAspPollPhpParityTests
{
    private static string RepoFile(params string[] parts)
        => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", Path.Combine(parts));

    [Fact]
    public void PollService_IsRegistered()
    {
        var program = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Program.cs"));
        Assert.Contains("IErpEinvoiceAspPollService, EcomAE.Platform.Erp.ErpEinvoiceAspPollService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_Handler_UsesThePollService()
    {
        var module = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Modules", "ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpAjaxEinvoicePollAsp, HandleEinvoicePollAspAsync", module, StringComparison.Ordinal);
        Assert.Contains("IErpEinvoiceAspPollService", module, StringComparison.Ordinal);
        Assert.Contains("Polled {0} · accepted {1} · rejected {2} · still pending {3} · errors {4}", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_PortsThePhpAnchors()
    {
        var service = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Erp", "ErpEinvoiceAspPollService.cs"));
        Assert.Contains("epc_einvoice_asp_submissions", service, StringComparison.Ordinal);
        Assert.Contains("'submitted','retry_pending'", service, StringComparison.Ordinal);
        Assert.Contains("next_poll_at", service, StringComparison.Ordinal);
        Assert.Contains("retry_count` < s.`max_retries", service, StringComparison.Ordinal);
        Assert.Contains("asp_api_key", service, StringComparison.Ordinal);
        Assert.Contains("RETRY-", service, StringComparison.Ordinal);
        Assert.Contains("300 * (int)Math.Pow(2", service, StringComparison.Ordinal);
        Assert.Contains("3600", service, StringComparison.Ordinal);
        Assert.Contains("asp_retry_ok", service, StringComparison.Ordinal);
        Assert.Contains("asp_retry_fail", service, StringComparison.Ordinal);
        Assert.Contains("asp_retry_exhausted", service, StringComparison.Ordinal);
        Assert.Contains("asp_accepted", service, StringComparison.Ordinal);
        Assert.Contains("asp_rejected", service, StringComparison.Ordinal);
        Assert.Contains("epc_einvoice_events", service, StringComparison.Ordinal);
        Assert.Contains("fta_report_status", service, StringComparison.Ordinal);
        Assert.Contains("/invoices/", service, StringComparison.Ordinal);
        Assert.Contains("/status", service, StringComparison.Ordinal);
        Assert.Contains("X-Invoice-UUID", service, StringComparison.Ordinal);
        Assert.Contains("X-Submission-Reference", service, StringComparison.Ordinal);
        Assert.Contains("Max retries reached", service, StringComparison.Ordinal);
        Assert.Contains("Invoice accepted by ASP/FTA", service, StringComparison.Ordinal);
        Assert.Contains("BuildInvoiceXml", service, StringComparison.Ordinal);
    }
}
