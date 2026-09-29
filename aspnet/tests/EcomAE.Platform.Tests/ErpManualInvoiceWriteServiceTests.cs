using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpManualInvoiceWriteServiceTests
{
    [Fact]
    public void InvoiceSaveRouteUsesLiveWriteServiceBehindConfirmGate()
    {
        var root = FindRepoRoot();
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpManualInvoiceWriteService.cs"));
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));

        Assert.Contains("IErpManualInvoiceWriteService", module, StringComparison.Ordinal);
        Assert.Contains("if (!body.ConfirmWrites)", module, StringComparison.Ordinal);
        Assert.Contains("epc_einvoice_documents", service, StringComparison.Ordinal);
        Assert.Contains("epc_einvoice_lines", service, StringComparison.Ordinal);
        Assert.Contains("Submitted invoices cannot be edited", service, StringComparison.Ordinal);
        Assert.Contains("epc_einvoice_events", service, StringComparison.Ordinal);
        Assert.Contains("paid_amount", service, StringComparison.Ordinal);
        Assert.Contains("transaction_type_code", service, StringComparison.Ordinal);
        Assert.Contains("payment_means_code", service, StringComparison.Ordinal);
        Assert.Contains("bank_account", service, StringComparison.Ordinal);
        Assert.Contains("quantity must be positive", service, StringComparison.Ordinal);
        Assert.Contains("unit price cannot be negative", service, StringComparison.Ordinal);
        Assert.Contains("IErpDimensionWriteService", module, StringComparison.Ordinal);
        Assert.Contains("IErpBosWfRaiseWriteService", module, StringComparison.Ordinal);
        Assert.Contains("IErpManualInvoiceWriteService", program, StringComparison.Ordinal);
        Assert.Contains("area=sales&tab=invoices&inv_id=", module, StringComparison.Ordinal);
        Assert.Contains("Invoice updated", module, StringComparison.Ordinal);
        Assert.Contains("BuildInvoiceLinesJson", module, StringComparison.Ordinal);
        Assert.Contains("item_description", module, StringComparison.Ordinal);
        Assert.Contains("issue_date", service, StringComparison.Ordinal);
        Assert.Contains("IssueDate", module, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
