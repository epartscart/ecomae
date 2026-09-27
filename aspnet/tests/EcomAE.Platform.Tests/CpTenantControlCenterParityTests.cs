using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantControlCenterParityTests
{
    [Fact]
    public void Tenant_type_follows_php_precedence()
    {
        Assert.Equal("demo", CpTenantControlCenterService.TenantType(true, false, "auto_parts", "platform"));
        Assert.Equal("demo_erp_only", CpTenantControlCenterService.TenantType(true, true, "auto_parts", "platform"));
        Assert.Equal("erp_only", CpTenantControlCenterService.TenantType(false, true, "auto_parts", "client"));
        Assert.Equal("erp_only", CpTenantControlCenterService.TenantType(false, false, "erp_standalone", "platform"));
        Assert.Equal("commerce", CpTenantControlCenterService.TenantType(false, false, "erp_standalone", "client"));
        Assert.Equal("commerce", CpTenantControlCenterService.TenantType(false, false, "jewellery", "platform"));
    }

    [Fact]
    public void Type_labels_and_badges_match_php_maps()
    {
        Assert.Equal("Commerce", CpTenantControlCenterService.TypeLabel("commerce"));
        Assert.Equal("ERP-only demo", CpTenantControlCenterService.TypeLabel("demo_erp_only"));
        Assert.Equal("ERP-only", CpTenantControlCenterService.TypeLabel("erp_only"));
        Assert.Equal("Commerce", CpTenantControlCenterService.TypeLabel("unknown"));

        Assert.Equal("label-primary", CpTenantControlCenterService.BadgeClass("commerce"));
        Assert.Equal("label-info", CpTenantControlCenterService.BadgeClass("demo"));
        Assert.Equal("label-warning", CpTenantControlCenterService.BadgeClass("erp_only"));
        Assert.Equal("label-default", CpTenantControlCenterService.BadgeClass("unknown"));
    }

    [Fact]
    public void Commerce_host_normalises_scheme_path_and_www()
    {
        Assert.Equal("www.shop.example", CpTenantControlCenterService.CommerceHost("https://WWW.shop.example/en/"));
        Assert.Equal("www.shop.example", CpTenantControlCenterService.CommerceHost("shop.example"));
        Assert.Equal(string.Empty, CpTenantControlCenterService.CommerceHost("localhost"));
        Assert.Equal(string.Empty, CpTenantControlCenterService.CommerceHost(null));
    }

    [Fact]
    public void Admin_email_prefers_demo_contact_then_cp_operator_fields_then_from_email()
    {
        Assert.Equal(
            "demo@example.com",
            CpTenantControlCenterService.AdminEmail(true, "Demo@Example.com", """{"admin_cp_email":"ops@example.com"}""", "from@example.com"));

        Assert.Equal(
            "ops@example.com",
            CpTenantControlCenterService.AdminEmail(false, "demo@example.com", """{"admin_email":"sales@example.com","admin_cp_email":"ops@example.com"}""", "from@example.com"));

        Assert.Equal(
            "login@example.com",
            CpTenantControlCenterService.AdminEmail(false, "", """{"operator_login_email":"login@example.com"}""", "from@example.com"));

        // A business admin_email in intro_json is not a CP login in PHP.
        Assert.Equal(
            "from@example.com",
            CpTenantControlCenterService.AdminEmail(false, "", """{"admin_email":"sales@example.com"}""", "from@example.com"));

        Assert.Equal("from@example.com", CpTenantControlCenterService.AdminEmail(false, "", "not json", "from@example.com"));
        Assert.Equal(string.Empty, CpTenantControlCenterService.AdminEmail(false, "", "", "not-an-email"));
    }

    [Fact]
    public void Urls_follow_php_erp_only_router_and_commerce_host()
    {
        var erp = CpTenantControlCenterService.Urls("erp_only", "indus", "indus.example");
        Assert.Equal(string.Empty, erp.Storefront);
        Assert.Equal("https://www.ecomae.com/cp/client-erp/indus/", erp.Erp);

        var commerce = CpTenantControlCenterService.Urls("commerce", "shop", "shop.example");
        Assert.Equal("https://www.shop.example/en/", commerce.Storefront);
        Assert.Equal("https://www.shop.example/cp/", commerce.Cp);
        Assert.Equal(string.Empty, commerce.Erp);
    }

    [Fact]
    public void Site_keys_are_normalised_like_php()
    {
        Assert.Equal("induslive", CpTenantControlCenterService.NormalizeSiteKey(" Indus-Live! "));
        Assert.Equal("indus_live", CpTenantControlCenterService.NormalizeSiteKey(" Indus_Live "));
        Assert.Equal(string.Empty, CpTenantControlCenterService.NormalizeSiteKey("---"));
    }

    [Fact]
    public void Row_ordering_places_demo_first_then_erp_only_then_commerce()
    {
        Assert.Equal(0, Row("demo").TypeOrder);
        Assert.Equal(0, Row("demo_erp_only").TypeOrder);
        Assert.Equal(1, Row("erp_only").TypeOrder);
        Assert.Equal(2, Row("commerce").TypeOrder);
    }

    [Fact]
    public async Task Unconfigured_registry_returns_typed_empty_view()
    {
        var view = await new CpTenantControlCenterService(new UnconfiguredConnections()).LoadAsync();

        Assert.Empty(view.Tenants);
        Assert.Equal("unconfigured", view.Source);
        Assert.Equal(0, view.RegistryCount);
        Assert.Equal("ecomae", view.PlatformDb);
        Assert.NotEqual(string.Empty, view.Error);
    }

    [Fact]
    public void Service_reads_registry_columns_without_carrying_plaintext_passwords()
    {
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpTenantControlCenterService.cs"));
        Assert.Contains("FROM `epc_portal_tenants`", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY `is_demo` DESC, `erp_only_shared` DESC, `site_key` ASC", service, StringComparison.Ordinal);
        Assert.Contains("HasStoredPassword", service, StringComparison.Ordinal);
        Assert.DoesNotContain("string StoredPassword", service, StringComparison.Ordinal);
        Assert.DoesNotContain("db_password", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_renders_the_php_control_center_columns_and_registration()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantsApp.razor"));
        Assert.Contains("ICpTenantControlCenterService", razor, StringComparison.Ordinal);
        Assert.Contains("Tenant control center", razor, StringComparison.Ordinal);
        foreach (var column in new[] { ">Type<", ">ERP pack<", ">Login email<", ">ERP login URL<", ">Password<", ">Links<" })
        {
            Assert.Contains(column, razor, StringComparison.Ordinal);
        }

        Assert.Contains("In registry", razor, StringComparison.Ordinal);
        Assert.Contains("Accessible", razor, StringComparison.Ordinal);
        Assert.Contains("tenant_set_active", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PortalTenantDigest", razor, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTenantControlCenterService, EcomAE.Platform.Cp.CpTenantControlCenterService", program, StringComparison.Ordinal);
    }

    private static CpTenantControlRow Row(string type)
        => new(
            "key", "host", "Trade", type, CpTenantControlCenterService.TypeLabel(type),
            CpTenantControlCenterService.BadgeClass(type), "db", "", "active", "active", "", "", "", "",
            true, true, false, false, false, type.StartsWith("demo", StringComparison.Ordinal));

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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

        throw new InvalidOperationException("Repo root not found");
    }
}
