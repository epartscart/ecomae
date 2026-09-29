using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpQualityResultRecordingParityTests
{
    [Fact]
    public void QualityPagePostsPersistedResultForm()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpQualityApp.razor"));
        Assert.Contains("ErpQualityOrderRecordForm", page, StringComparison.Ordinal);
        Assert.Contains("order_id", page, StringComparison.Ordinal);
    }

    [Fact]
    public void QualityWriteServiceReplacesResultsAndUpdatesVerdict()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpQmOrderRecordWriteService.cs"));
        Assert.Contains("DELETE FROM `epc_qm_result`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_qm_result`", service, StringComparison.Ordinal);
        Assert.Contains("status`='completed'", service, StringComparison.Ordinal);
        Assert.Contains("verdict", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, relative);
            if (File.Exists(candidate)) return candidate;
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }
        throw new FileNotFoundException(relative);
    }
}
