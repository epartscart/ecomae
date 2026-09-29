using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEinvoiceSubmitWriteServiceTests
{
    [Fact]
    public void ManualSubmissionServiceClaimsValidatedDocumentAndRecordsEvents()
    {
        var service = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Erp/ErpEinvoiceSubmitWriteService.cs"));

        Assert.Contains("status` = 'queued'", service, StringComparison.Ordinal);
        Assert.Contains("asp_name", service, StringComparison.Ordinal);
        Assert.Contains("asp_reference", service, StringComparison.Ordinal);
        Assert.Contains("asp_manual", service, StringComparison.Ordinal);
        Assert.Contains("fta_report", service, StringComparison.Ordinal);
        Assert.Contains("status` IN ('draft','validated')", service, StringComparison.Ordinal);
    }

    [Fact]
    public void SubmitEndpointUsesConfirmedLiveWriteAndKeepsDryRunPath()
    {
        var module = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));

        Assert.Contains("IErpEinvoiceSubmitWriteService writes", module, StringComparison.Ordinal);
        Assert.Contains("dryRun.Evaluate(new ErpEinvoiceSubmitRequest(id, false))", module, StringComparison.Ordinal);
        Assert.Contains("writes.SubmitAsync(id, cancellationToken)", module, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
