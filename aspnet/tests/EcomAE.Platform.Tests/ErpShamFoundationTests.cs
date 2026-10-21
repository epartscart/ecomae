using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpShamFoundationTests
{
    [Fact]
    public void Entity_register_matches_blueprint_perimeter()
    {
        var entities = ErpShamFoundationService.Entities;
        Assert.Equal(11, entities.Length);

        var consolidated = entities.Where(e => e.Consolidated).Select(e => e.Code).OrderBy(c => c, StringComparer.Ordinal);
        Assert.Equal(["ALMA", "ASAPL", "CENT", "IMPE", "INDP", "SPDC"], consolidated.ToArray());

        var managed = entities.Where(e => !e.Consolidated).Select(e => e.Code).OrderBy(c => c, StringComparer.Ordinal);
        Assert.Equal(["AAAR", "AAPF", "ALTC", "PTAP", "RVAS"], managed.ToArray());

        // ASAU is a BU under ASAPL with its own cost centre — not a legal entity
        // (owner confirmed).
        Assert.DoesNotContain(entities, e => e.Code == "ASAU");
        Assert.Contains(ErpShamFoundationService.BusinessUnits, u => u.Code == "ASAU");
    }

    [Fact]
    public void Business_units_belong_to_asapl_and_cover_all_six_including_asau()
    {
        var units = ErpShamFoundationService.BusinessUnits;
        Assert.Equal(6, units.Length);
        Assert.All(units, u => Assert.Equal("ASAPL", u.Entity));
        Assert.Equal(
            ["ASAPL-BOOK", "ASAPL-CLP", "ASAPL-CLS", "ASAPL-RTL", "ASAPL-WSL", "ASAU"],
            units.Select(u => u.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray());
        var asau = units.Single(u => u.Code == "ASAU");
        Assert.Equal("CC-ASAU", asau.CostCentre);
        Assert.All(units, u => Assert.False(string.IsNullOrWhiteSpace(u.CostCentre)));
    }

    [Fact]
    public void Schema_sql_creates_bu_and_access_tables_and_entity_columns()
    {
        var file = Find("aspnet/src/EcomAE.Platform/Erp/ErpShamFoundationService.cs");
        Assert.Contains("epc_erp_org_business_units", file, StringComparison.Ordinal);
        Assert.Contains("epc_erp_org_user_access", file, StringComparison.Ordinal);
        Assert.Contains("consolidated", file, StringComparison.Ordinal);
        Assert.Contains("UNIQUE KEY `ux_access`", file, StringComparison.Ordinal);
        Assert.Contains("epc_entity_groups", file, StringComparison.Ordinal);
        Assert.Contains("epc_entity_members", file, StringComparison.Ordinal);
        Assert.Contains("epc_erp_org_entity_accounts", file, StringComparison.Ordinal);
        Assert.Contains("AccountActiveForEntityAsync", file, StringComparison.Ordinal);
        Assert.Contains("cost_centre", file, StringComparison.Ordinal);
        Assert.Contains("admin@alshamaligroup.com", file, StringComparison.Ordinal);
        Assert.Contains("users_groups_bind", file, StringComparison.Ordinal);
        Assert.Contains("RotatePasswordAsync", file, StringComparison.Ordinal);
        Assert.Contains("LegacyLoginSecurity.HashPassword", file, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_credential_constants_match_owner_instruction()
    {
        Assert.Equal("admin@alshamaligroup.com", ErpShamFoundationService.AdminEmail);
        Assert.Equal("Admin$2026", ErpShamFoundationService.InitialAdminPassword);
        // The seed never overwrites an existing password — rotation provision.
        var file = Find("aspnet/src/EcomAE.Platform/Erp/ErpShamFoundationService.cs");
        Assert.DoesNotContain("UPDATE `users` SET `password`=? WHERE LOWER(`email`)=LOWER(?) LIMIT", file, StringComparison.Ordinal);
    }

    [Fact]
    public void Sham_journal_post_route_is_gated_and_scoped()
    {
        var routes = Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs");
        Assert.Contains("ErpShamJournalPost = \"/erp/sham/journal-post\"", routes, StringComparison.Ordinal);

        var module = Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpShamJournalPost, HandleShamJournalPostAsync", module, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", module, StringComparison.Ordinal);

        var service = Find("aspnet/src/EcomAE.Platform/Erp/ErpShamJournalPostService.cs");
        // Gate order pins: entity → BU-in-entity → access grant → per-account activation.
        Assert.Contains("AccessAllowedAsync", service, StringComparison.Ordinal);
        Assert.Contains("AccountActiveForEntityAsync", service, StringComparison.Ordinal);
        Assert.Contains("journal-post", service, StringComparison.Ordinal);
        Assert.Contains("`entity_id`=?", service, StringComparison.Ordinal);
        Assert.Contains("BusinessUnitId = buId", service, StringComparison.Ordinal);
        Assert.Contains("LegalEntityId", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Sham_stock_reserve_route_is_gated_and_scoped()
    {
        var routes = Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs");
        Assert.Contains("ErpShamStockReserve = \"/erp/sham/stock-reserve\"", routes, StringComparison.Ordinal);

        var module = Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpShamStockReserve, HandleShamStockReserveAsync", module, StringComparison.Ordinal);

        var service = Find("aspnet/src/EcomAE.Platform/Erp/ErpShamStockReserveService.cs");
        // Gate order pins: entity → BU-in-entity + default warehouse → access grant.
        Assert.Contains("AccessAllowedAsync", service, StringComparison.Ordinal);
        Assert.Contains("stock-reserve", service, StringComparison.Ordinal);
        Assert.Contains("default_warehouse_id", service, StringComparison.Ordinal);
        Assert.Contains("OwnerCompanyId = scope.EntityId", service, StringComparison.Ordinal);
        Assert.Contains("WarehouseId = scope.WarehouseId", service, StringComparison.Ordinal);
        Assert.Contains("ErpInventoryReservationWriteService", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Sham_invoice_post_route_is_gated_and_scoped()
    {
        var routes = Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs");
        Assert.Contains("ErpShamInvoicePost = \"/erp/sham/invoice-post\"", routes, StringComparison.Ordinal);

        var module = Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpShamInvoicePost, HandleShamInvoicePostAsync", module, StringComparison.Ordinal);

        var service = Find("aspnet/src/EcomAE.Platform/Erp/ErpShamInvoicePostService.cs");
        // Gate order pins: entity -> BU-in-entity (pm id) -> access grant -> COA activation -> consume holds -> post.
        Assert.Contains("AccessAllowedAsync", service, StringComparison.Ordinal);
        Assert.Contains("invoice-post", service, StringComparison.Ordinal);
        Assert.Contains("AccountActiveForEntityAsync", service, StringComparison.Ordinal);
        Assert.Contains("ConsumeBySourceAsync", service, StringComparison.Ordinal);
        Assert.Contains("CompanyId = entityId", service, StringComparison.Ordinal);
        Assert.Contains("BusinessUnitId = buId", service, StringComparison.Ordinal);
        Assert.Contains("LegalEntityId = entityId", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Foundation_seed_route_is_gated_and_registered()
    {
        Assert.Equal("/erp/sham/foundation-seed", EcomAeRoutes.ErpShamFoundationSeed);
        var module = Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpShamFoundationSeed, HandleShamFoundationSeedAsync", module, StringComparison.Ordinal);
        Assert.Contains("confirmWrites=true to seed the SHAM foundation", module, StringComparison.Ordinal);
        var program = Find("aspnet/src/EcomAE.Platform/Program.cs");
        Assert.Contains("IErpShamFoundationService, EcomAE.Platform.Erp.ErpShamFoundationService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Org_tabs_registered_in_catalog_and_workspace_specs()
    {
        foreach (var tab in new[] { "org_entities", "org_business_units", "org_group_members", "org_user_access", "org_entity_accounts" })
        {
            Assert.True(ErpD365WorkspaceCatalog.TryGet(tab, out var page), tab);
            Assert.Equal("enterprise", page.Area);
            Assert.True(ErpWorkspaceReadService.Specs.ContainsKey(tab), tab);
        }
    }

    [Fact]
    public void Org_specs_query_real_tables_with_joins()
    {
        var entities = ErpOrgWorkspaceSpecs.Tabs["org_entities"];
        Assert.Contains("epc_erp_pm_legal_entities", entities.Sql, StringComparison.Ordinal);
        var bus = ErpOrgWorkspaceSpecs.Tabs["org_business_units"];
        Assert.Contains("epc_erp_org_business_units", bus.Sql, StringComparison.Ordinal);
        var access = ErpOrgWorkspaceSpecs.Tabs["org_user_access"];
        Assert.Contains("epc_erp_org_user_access", access.Sql, StringComparison.Ordinal);
        var members = ErpOrgWorkspaceSpecs.Tabs["org_group_members"];
        Assert.Contains("epc_entity_members", members.Sql, StringComparison.Ordinal);
        var accounts = ErpOrgWorkspaceSpecs.Tabs["org_entity_accounts"];
        Assert.Contains("epc_erp_org_entity_accounts", accounts.Sql, StringComparison.Ordinal);
        Assert.Contains("epc_erp_coa_accounts", accounts.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Bu_query_selector_mirrors_cmp_resolution()
    {
        var units = new List<ErpHostContext.ErpBusinessUnitDigest>
        {
            new(1, "ASAPL-WSL", "ASAPL Wholesale", 10),
            new(2, "ASAPL-RTL", "ASAPL Retail", 10),
        };
        var request = new DefaultHttpContext().Request;
        request.QueryString = new QueryString("?bu=ASAPL-RTL");
        var active = ErpHostContext.ActiveBusinessUnitFromQuery(units, request);
        Assert.NotNull(active);
        Assert.Equal(2, active!.Id);

        request.QueryString = new QueryString("?bu=9");
        active = ErpHostContext.ActiveBusinessUnitFromQuery(units, request);
        Assert.NotNull(active);
        Assert.Equal(1, active!.Id); // unknown id falls back to first, like cmp

        request.QueryString = new QueryString("");
        Assert.Null(ErpHostContext.ActiveBusinessUnitFromQuery(units, request));
        Assert.Null(ErpHostContext.ActiveBusinessUnitFromQuery([], request));
    }

    private static string Find(string relative)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, relative)))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!, relative));
    }
}
