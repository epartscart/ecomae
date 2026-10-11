using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Defects found by the fresh ERP-only tenant browser acceptance run: PHP lazily
/// provisions inventory and period-close tables, resolves the e-invoice seller
/// country from the CP portal settings row, and serves the purchase-order script.
/// </summary>
public sealed class ErpFreshTenantAcceptanceFixTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Fact]
    public void Purchase_orders_script_is_allowlisted_and_exists()
    {
        Assert.Contains("\"erp-purchase-orders.js\"", Read("aspnet/src/EcomAE.Platform/Erp/ErpAppAssets.cs"));
        Assert.True(File.Exists(Path.Combine(Root, "aspnet/src/EcomAE.Platform/wwwroot/erp/erp-purchase-orders.js")));
    }

    [Fact]
    public void Inventory_writer_ensures_schema_after_every_open()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpInventoryMovementWriteService.cs");
        var opens = source.Split("_connections.OpenAsync(cancellationToken)").Length - 1;
        var ensures = source.Split("ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken)").Length - 1;
        Assert.True(opens > 0);
        Assert.Equal(opens, ensures);
    }

    [Theory]
    [InlineData("epc_erp_inv_items")]
    [InlineData("epc_erp_inv_stock")]
    [InlineData("epc_erp_inv_movements")]
    [InlineData("epc_erp_inv_warehouses")]
    [InlineData("epc_erp_inv_serials")]
    [InlineData("epc_erp_periods")]
    [InlineData("epc_erp_period_close_log")]
    [InlineData("epc_erp_fiscal_locks")]
    public void Lazy_schema_creates_php_owned_tables(string table)
    {
        Assert.Contains("CREATE TABLE IF NOT EXISTS `" + table + "`", Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
    }

    [Fact]
    public void Period_schema_ready_provisions_before_checking()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpPeriodLockReopenWriteService.cs");
        var ensure = source.IndexOf("ErpLazySchema.EnsurePeriodCloseAsync(connection, ct)", StringComparison.Ordinal);
        var check = source.IndexOf("ColumnExistsAsync(connection, \"epc_erp_periods\", \"checklist_json\", ct)", StringComparison.Ordinal);
        Assert.True(ensure > 0 && ensure < check);
    }

    [Theory]
    [InlineData("AE", "AE")]
    [InlineData("United Arab Emirates", "AE")]
    [InlineData("uae", "AE")]
    [InlineData("United Kingdom", "GB")]
    [InlineData("usa", "US")]
    [InlineData("sa", "SA")]
    [InlineData("", "")]
    [InlineData("Somewhere", "")]
    public void Country_name_to_iso_matches_php(string input, string expected)
    {
        Assert.Equal(expected, EcomAE.Platform.Erp.ErpEinvoiceProfileWriteService.CountryNameToIso(input));
    }

    [Fact]
    public void Seller_country_falls_back_to_portal_settings_after_profile_and_company()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpEinvoiceProfileWriteService.cs");
        var profile = source.IndexOf("epc_tax_toolkit_tenant_profile` WHERE", StringComparison.Ordinal);
        var company = source.IndexOf("'company_country_code'", StringComparison.Ordinal);
        var portal = source.IndexOf("ResolvePortalCountryAsync(connection, cancellationToken)", StringComparison.Ordinal);
        Assert.True(profile > 0 && profile < company && company < portal);
    }

    [Fact]
    public void Cp_settings_reader_tolerates_missing_menu_tables()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Cp/CpIndustrySettingsService.cs");
        Assert.Contains("catch (DbException)", source);
    }

    [Fact]
    public void Inventory_schema_adds_php_phase8_reorder_level_and_on_hand_read_ensures_it()
    {
        Assert.Contains("AddColumnIfMissingAsync(c, \"epc_erp_inv_items\", \"reorder_level\", \"decimal(14,3) NOT NULL DEFAULT 0.000\"", Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        var reporter = Read("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        var ensure = reporter.IndexOf("ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken)", StringComparison.Ordinal);
        var query = reporter.IndexOf("LegacySurfaceDashboardSql.SelectErpInvOnHand", StringComparison.Ordinal);
        Assert.True(ensure > 0 && ensure < query);
    }

    [Fact]
    public void Pos_movement_date_is_epoch_seconds_like_php_int11_column()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpRtlPosSaleWriteService.cs");
        Assert.Contains("var movementDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();", source);
        Assert.DoesNotContain("ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture);\n                var movementDate", source);
    }

    [Fact]
    public void Seller_save_ensures_php_einvoice_settings_schema_before_availability_check()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Erp/ErpEinvoiceProfileWriteService.cs");
        var ensure = source.IndexOf("await EnsureSellerSchemaAsync(connection, cancellationToken)", StringComparison.Ordinal);
        var check = source.IndexOf("await SellerSchemaAvailableAsync(connection, cancellationToken)", StringComparison.Ordinal);
        Assert.True(ensure > 0 && ensure < check);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_einvoice_settings`", source);
    }

    [Theory]
    [InlineData("sellerLegalRegNo")]
    [InlineData("sellerLegalRegType")]
    [InlineData("sellerAuthorityName")]
    [InlineData("sellerAddressLine1")]
    [InlineData("sellerEmirate")]
    [InlineData("sellerPhone")]
    [InlineData("sellerEmail")]
    public void Native_seller_form_exposes_mandatory_php_fields(string field)
    {
        Assert.Contains("name=\"" + field + "\"", Read("aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor"));
    }

    [Fact]
    public void Seller_profile_readback_includes_legal_registration_and_address()
    {
        var sql = Read("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        foreach (var key in new[] { "seller_legal_reg_no", "seller_legal_reg_type", "seller_authority_name", "seller_address_line1", "seller_phone" })
        {
            Assert.Contains("'" + key + "'", sql);
        }
    }

    [Fact]
    public void Native_cash_accounts_page_posts_account_create_to_aspnet_route()
    {
        var razor = Read("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashAccountsApp.razor");
        Assert.Contains("action=\"@EcomAeRoutes.ErpCashAccountsCreate\"", razor);
        Assert.DoesNotContain("Writes stay Classic for account create", razor);
        var module = Read("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ReadJsonOrDefaultAsync<ErpCashAccountCreateBody>", module);
    }

    [Fact]
    public void Period_close_ensure_provisions_php_fiscal_year_tables_and_runs_before_fy_and_soft_close()
    {
        var schema = Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs");
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_fy_years`", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_fy_periods`", schema);
        foreach (var file in new[]
                 {
                     "aspnet/src/EcomAE.Platform/Erp/ErpFyCreateWriteService.cs",
                     "aspnet/src/EcomAE.Platform/Erp/ErpPeriodSoftCloseWriteService.cs",
                     "aspnet/src/EcomAE.Platform/Erp/ErpPeriodReadService.cs",
                 })
        {
            Assert.Contains("ErpLazySchema.EnsurePeriodCloseAsync(", Read(file));
        }
    }

    [Fact]
    public void Contract_status_fails_when_contract_does_not_exist()
    {
        var src = Read("aspnet/src/EcomAE.Platform/Erp/ErpContractStatusWriteService.cs");
        Assert.Contains("var affected = await ErpDb.ExecuteAsync(", src);
        Assert.Contains("Fail(\"not_found\", \"Contract not found\")", src);
    }

    [Fact]
    public void Default_legal_entity_seed_is_idempotent_on_code()
    {
        var src = Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs");
        Assert.Contains("WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_pm_legal_entities` WHERE `code` = 'MAIN')", src);
    }

    [Fact]
    public void Font_awesome_webfonts_are_served_from_bridged_vendor_folder()
    {
        var src = Read("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs");
        Assert.Contains("endpoints.MapGet(\"/webfonts/{fileName}\"", src);
        Assert.Contains("\"fontawesome\", \"webfonts\"", src);
    }

    [Fact]
    public void Erp_chrome_hides_control_panel_for_demo_tenants_without_cp_surface()
    {
        var razor = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");
        Assert.Contains("record?.ControlPanelEnabled ?? true", razor);
    }

    [Fact]
    public void Fresh_read_surfaces_provision_canonical_schemas_before_querying()
    {
        var src = Read("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        foreach (var helper in new[]
                 {
                     "ErpPurchaseInvoiceWriteService.EnsureSchemaAsync(",
                     "ErpCashWriteService.EnsureSchemaAsync(",
                     "ErpSalesInvoiceWriteService.EnsureSchemaAsync(",
                     "ErpPurchaseOrderWriteService.EnsureSchemaAsync(",
                 })
        {
            Assert.Contains(helper, src);
        }
    }

    [Fact]
    public void Erp_modules_are_not_routable_under_cp()
    {
        var pages = Directory.GetFiles(Path.Combine(Root, "aspnet/src/EcomAE.Platform/Components/Pages"), "*.razor");
        foreach (var slug in EcomAE.Platform.Middleware.CpErpModulePathRedirectMiddleware.ErpOnlySlugs)
        {
            Assert.DoesNotContain(pages, f => File.ReadAllText(f).Contains("@page \"/cp/" + slug + "\"", StringComparison.Ordinal));
            Assert.Contains(pages, f => File.ReadAllText(f).Contains("@page \"/erp/" + slug + "\"", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(EcomAE.Platform.Presentation.LegacyChromeNavCatalog.ControlPanelQuickActions, i => i.Href.StartsWith("/erp/", StringComparison.Ordinal));
        Assert.Contains("app.UseMiddleware<CpErpModulePathRedirectMiddleware>();", Read("aspnet/src/EcomAE.Platform/Program.cs"));
    }

    [Fact]
    public void Surface_capability_is_per_surface_for_cp_only_and_erp_only_users()
    {
        var cpOnly = new EcomAE.Platform.Auth.LegacySessionContext(
            EcomAE.Platform.Auth.LegacySessionKind.Admin, 7, "s", new[] { EcomAE.Platform.Security.EcomAePermissions.TenantCpAccess }, HasBackendAccess: true);
        var erpOnly = new EcomAE.Platform.Auth.LegacySessionContext(
            EcomAE.Platform.Auth.LegacySessionKind.Admin, 8, "s", new[] { EcomAE.Platform.Security.EcomAePermissions.TenantErpAccess }, HasBackendAccess: true);
        var both = new EcomAE.Platform.Auth.LegacySessionContext(
            EcomAE.Platform.Auth.LegacySessionKind.Admin, 9, "s",
            new[] { EcomAE.Platform.Security.EcomAePermissions.TenantCpAccess, EcomAE.Platform.Security.EcomAePermissions.TenantErpAccess }, HasBackendAccess: true);

        var gate = typeof(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware);
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/cp/orders-app", cpOnly));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/erp/fin-advanced-app", cpOnly));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/erp/pos-overview-app", cpOnly));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/erp/pos-overview-app", erpOnly));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/cp", erpOnly));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/cp/orders-app", both));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/erp/fin-advanced-app", both));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.HasSurfaceCapability("/bos/fleet", cpOnly));
        Assert.NotNull(gate);

        var pos = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor");
        Assert.Contains("session.Capabilities.Contains(\"erp\")", pos);
        Assert.DoesNotContain("session.Capabilities.Contains(\"cp\")", pos);
    }

    [Theory]
    [InlineData("Saudi Arabia", "SA")]
    [InlineData("KSA", "SA")]
    [InlineData("Oman", "OM")]
    [InlineData("Qatar", "QA")]
    [InlineData("United Arab Emirates", "AE")]
    [InlineData("India", "IN")]
    [InlineData("gb", "GB")]
    [InlineData("Atlantis", "")]
    public void Country_names_resolve_to_iso_for_seller_setup(string name, string expected)
        => Assert.Equal(expected, EcomAE.Platform.Erp.ErpEinvoiceProfileWriteService.CountryNameToIso(name));

    [Fact]
    public void Inventory_lazy_schema_guards_alters_and_adds_mfg_movement_types()
    {
        var schema = Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs");
        Assert.Contains("AddColumnIfMissingAsync(c, \"epc_erp_inv_items\", \"reorder_level\"", schema);
        Assert.Contains("'mfg_issue','mfg_receipt'", schema);
        Assert.DoesNotContain("TryExecuteAsync(c, \"ALTER TABLE `epc_erp_inv_items` ADD COLUMN", schema);
    }

    [Fact]
    public void Cp_surface_is_denied_on_erp_only_tenants()
    {
        var gate = Read("aspnet/src/EcomAE.Platform/Middleware/AdminSurfaceAuthGateMiddleware.cs");
        Assert.Contains("TenantMode.ErpOnlyTenant", gate);
        Assert.Contains("erp-only-tenant", gate);
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsControlPanelPath("/cp/orders"));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsControlPanelPath("/erp/fin-advanced-app"));
    }
}
