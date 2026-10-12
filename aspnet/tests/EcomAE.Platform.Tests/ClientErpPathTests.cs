using System.Data;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ClientErpPathTests
{
    [Theory]
    [InlineData("/cp/client-erp", "", "", true, false, false)]
    [InlineData("/cp/client-erp/", "", "", true, false, false)]
    [InlineData("/cp/client-erp/asap", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/asap/", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/ASAP!/", "asap", "", false, true, false)]
    [InlineData("/cp/client-erp/asap/erp", "asap", "erp", false, false, true)]
    [InlineData("/cp/client-erp/asap/erp/app", "asap", "erp/app", false, false, true)]
    public void Parse_MirrorsPhpRouter(string path, string key, string rest, bool bare, bool login, bool erp)
    {
        Assert.True(ClientErpPath.TryParse(path, out var parsed));
        Assert.Equal(key, parsed.SiteKey);
        Assert.Equal(rest, parsed.Rest);
        Assert.Equal(bare, parsed.IsBare);
        Assert.Equal(login, parsed.IsLoginRoot);
        Assert.Equal(erp, parsed.IsErpShell);
    }

    [Theory]
    [InlineData("/cp")]
    [InlineData("/cp/login")]
    [InlineData("/erp")]
    [InlineData("/erp/login")]
    public void Parse_RejectsSuperSurfaces(string path)
    {
        Assert.False(ClientErpPath.TryParse(path, out _));
        Assert.False(AdminSurfaceAuthGateMiddleware.IsClientErpLoginPath(path));
    }

    [Fact]
    public void AuthGate_AllowsPickerAndCompanyLogin_NotErpShell()
    {
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp"));
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap"));
        Assert.False(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/"));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/erp"));
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/cp/client-erp/asap/erp/app"));
    }

    [Fact]
    public void PrefixErpPath_KeepsChromeOnClientPrefix()
    {
        Assert.Equal("/cp/client-erp/asap/", ClientErpPath.PrefixErpPath("asap", "/erp/login"));
        Assert.Equal("/cp/client-erp/asap/erp", ClientErpPath.PrefixErpPath("asap", "/erp"));
        Assert.Equal("/cp/client-erp/asap/erp/app", ClientErpPath.PrefixErpPath("asap", "/erp/app"));
    }

    [Fact]
    public void ForErpOnly_IsCompanyMark_NotSuperCp()
    {
        var brand = LoginHostBrand.ForErpOnly("asap", "ASAP Autoparts", "auto_parts");
        Assert.Equal(LoginHostBrand.Kind.CompanyMark, brand.LogoKind);
        Assert.Equal("asap", brand.SiteKey);
        Assert.Equal("ASAP Autoparts", brand.Label);
        Assert.DoesNotContain("Operator Access", LoginIndustryContent.For(brand, "erp").FormTitle, StringComparison.Ordinal);
        Assert.Contains("Sign in to ERP", LoginIndustryContent.For(brand, "erp").FormTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRow_SurvivesMissingDedicatedColumns()
    {
        var table = new DataTable();
        table.Columns.Add("site_key", typeof(string));
        table.Columns.Add("trade_name", typeof(string));
        table.Columns.Add("industry_code", typeof(string));
        table.Columns.Add("hosted_on", typeof(string));
        table.Columns.Add("db_name", typeof(string));
        table.Columns.Add("db_user", typeof(string));
        table.Columns.Add("db_password", typeof(string));
        table.Columns.Add("is_demo", typeof(int));
        table.Columns.Add("erp_only_shared", typeof(int));
        table.Columns.Add("is_active", typeof(int));
        table.Rows.Add("asap", "ASAP Autoparts", "auto_parts", "platform", "ecomae_asap", "u", "p", 0, 1, 1);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var row = DbClientErpTenantLookup.ReadRow(reader);
        Assert.Equal("asap", row.SiteKey);
        Assert.Equal("ASAP Autoparts", row.TradeName);
        Assert.Equal("ecomae_asap", row.DatabaseName);
        Assert.True(row.IsActive);
        Assert.False(row.IsDemo);
        Assert.False(row.DedicatedDb);
        Assert.True(DbClientErpTenantLookup.IsUsableErpOnly(row));
        Assert.True(DbClientErpTenantLookup.IsErpOnlyType(true, "auto_parts", "platform"));
        Assert.False(DbClientErpTenantLookup.IsErpOnlyType(false, "auto_parts", "client"));
    }

    [Fact]
    public async Task Lookup_OpenRegistryThrow_ReturnsEmptyNotThrow()
    {
        var lookup = new DbClientErpTenantLookup(new ThrowingRegistryConnections());
        Assert.Null(await lookup.FindActiveErpOnlyAsync("asap"));
        Assert.Empty(await lookup.ListActiveErpOnlyAsync());
    }

    private sealed class ThrowingRegistryConnections : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("lookup-must-not-open-tenant");

        public Task<System.Data.Common.DbConnection> OpenAsync(
            string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("lookup-must-not-open-tenant");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(
            TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("lookup-must-not-open-tenant");

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("registry-unavailable");
    }
}
