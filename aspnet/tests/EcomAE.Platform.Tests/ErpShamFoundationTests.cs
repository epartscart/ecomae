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

        // ASAU intentionally absent: blueprint marks its legal status unverified.
        Assert.DoesNotContain(entities, e => e.Code == "ASAU");
    }

    [Fact]
    public void Business_units_belong_to_asapl_and_cover_all_five()
    {
        var units = ErpShamFoundationService.BusinessUnits;
        Assert.Equal(5, units.Length);
        Assert.All(units, u => Assert.Equal("ASAPL", u.Entity));
        Assert.Equal(
            ["ASAPL-BOOK", "ASAPL-CLP", "ASAPL-CLS", "ASAPL-RTL", "ASAPL-WSL"],
            units.Select(u => u.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray());
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
        foreach (var tab in new[] { "org_entities", "org_business_units", "org_group_members", "org_user_access" })
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
