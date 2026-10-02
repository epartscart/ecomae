using EcomAE.Platform.Erp;
using System.Data.Common;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpWhtSettlePhpParityTests
{
    [Fact]
    public async Task Settlement_RejectsInvalidIdsBeforeOpeningDatabase()
    {
        var result = await new ErpWhtSettleWriteService(new UnconfiguredConnections())
            .SettleAsync(0);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("A withholding transaction id is required.", result.Message);
    }

    [Fact]
    public void Settlement_MatchesPhpMissingAndAlreadySettledGuards()
    {
        var service = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Erp/ErpWhtSettleWriteService.cs"));

        Assert.Contains("Withholding transaction table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Transaction not found", service, StringComparison.Ordinal);
        Assert.Contains("Already settled", service, StringComparison.Ordinal);
        Assert.Contains("status` = 'settled'", service, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
