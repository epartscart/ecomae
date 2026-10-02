using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEinvoiceCountryParityTests
{
    [Fact]
    public void Seller_save_resolves_registered_country_before_writing_profile()
    {
        var service = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Erp/ErpEinvoiceProfileWriteService.cs"));

        Assert.Contains("epc_tax_toolkit_tenant_profile", service, StringComparison.Ordinal);
        Assert.Contains("company_country_code", service, StringComparison.Ordinal);
        Assert.Contains("ColumnExistsAsync(connection, \"epc_tax_toolkit_tenant_profile\", \"time_updated\"", service, StringComparison.Ordinal);
        Assert.Contains("SellerSchemaAvailableAsync", service, StringComparison.Ordinal);
        Assert.Contains("PHP-owned e-invoice seller schema is unavailable.", service, StringComparison.Ordinal);
        Assert.Contains("Registered tenant country is required for e-invoice compliance", service, StringComparison.Ordinal);
        Assert.Contains("[\"seller_country_code\"] = country", service, StringComparison.Ordinal);
        Assert.Contains("country == \"AE\" && string.IsNullOrWhiteSpace(request.SellerCity)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("var country = NormalizeCountry(request.SellerCountryCode)", service, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seller_save_does_not_accept_preview_country_without_configured_tenant_database()
    {
        var result = await new ErpEinvoiceProfileWriteService(new UnconfiguredConnections())
            .SaveSellerAsync(new ErpEinvoiceSellerWriteRequest(
                SellerName: "Non-AE tenant",
                SellerTrn: "123456789",
                SellerCountryCode: "AE"));

        Assert.False(result.Succeeded);
        Assert.Equal("db", result.Code);
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(
            CancellationToken cancellationToken = default)
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

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
