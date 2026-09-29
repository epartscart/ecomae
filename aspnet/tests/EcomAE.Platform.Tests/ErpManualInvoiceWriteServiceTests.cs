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
        Assert.Contains("IErpManualInvoiceWriteService", program, StringComparison.Ordinal);
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
