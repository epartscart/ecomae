using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// <see cref="CpNavTree"/> must reproduce PHP <c>epc_cp_build_nav_tabs()</c> over the real
/// <c>control_groups</c>/<c>control_items</c> rows (perf-cache snapshot in <c>content/files/epc_cache</c>).
/// </summary>
public sealed class CpNavTreePhpParityTests
{
    private static CpNavRows Snapshot()
    {
        var json = File.ReadAllText(FindRepoFile("content/files/epc_cache/epc_cp_menu_rows_v1_ecomae.json"));
        var rows = CpNavMenuService.ParseSnapshot(json, "test");
        Assert.NotNull(rows);
        return rows!;
    }

    private static string? NoTranslate(string _) => null;

    [Fact]
    public void Snapshot_ParsesPhpPerfCacheRows()
    {
        var rows = Snapshot();
        Assert.Equal(19, rows.Groups.Count);
        Assert.Equal(112, rows.Items.Count);
        Assert.Contains(rows.Items, i => i.Url == "/<backend>/users/usergroups" && i.GroupId == 2 && i.Caption == "747");
    }

    [Fact]
    public void SuperOperator_PrimaryThenRemainingThenAdvanced_NoEmptyGroups()
    {
        var rows = Snapshot();
        var groups = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.SuperOperator(), NoTranslate);

        Assert.NotEmpty(groups);
        Assert.All(groups, g => Assert.NotEmpty(g.Items));

        var keys = groups.Select(g => g.CaptionKey).ToList();
        var primary = keys.Where(k => CpNavTree.PrimaryGroupKeys.Contains(k)).ToList();
        var advanced = keys.Where(k => CpNavTree.AdvancedGroupKeys.Contains(k)).ToList();
        var remaining = keys.Except(primary).Except(advanced).ToList();

        Assert.Equal(CpNavTree.PrimaryGroupKeys.Where(keys.Contains).ToList(), primary);
        Assert.Equal(CpNavTree.AdvancedGroupKeys.Where(keys.Contains).ToList(), advanced);

        var lastPrimary = primary.Count == 0 ? -1 : keys.IndexOf(primary[^1]);
        var firstAdvanced = advanced.Count == 0 ? keys.Count : keys.IndexOf(advanced[0]);
        Assert.All(remaining, r => Assert.InRange(keys.IndexOf(r), lastPrimary + 1, firstAdvanced - 1));
        Assert.All(groups, g => Assert.Equal(CpNavTree.AdvancedGroupKeys.Contains(g.CaptionKey), g.IsAdvanced));
    }

    [Fact]
    public void Build_ReplacesBackendPlaceholder_AndDedupesByPathIgnoringQuery()
    {
        var groups = new[] { new CpNavRawGroup(1, "740", 1) };
        var items = new[]
        {
            new CpNavRawItem(1, 1, "Orders", "/<backend>/shop/orders", 1, "fa-shopping-cart", false),
            new CpNavRawItem(2, 1, "Orders again", "/<backend>/shop/orders?tab=new", 2, "", false),
            new CpNavRawItem(3, 1, "Carts", "/<backend>/shop/carts", 3, "", false),
        };

        var result = CpNavTree.Build(groups, items, CpNavPolicy.SuperOperator(), NoTranslate);

        var g = Assert.Single(result);
        Assert.Equal(new[] { "/cp/shop/orders", "/cp/shop/carts" }, g.Items.Select(i => i.Url).ToArray());
        Assert.DoesNotContain(g.Items, i => i.Url.Contains("<backend>", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_KeepsDbOrder_ForItemsAndRemainingGroups()
    {
        var groups = new[]
        {
            new CpNavRawGroup(3, "742", 30),
            new CpNavRawGroup(1, "740", 10),
            new CpNavRawGroup(2, "741", 20),
        };
        var items = new[]
        {
            new CpNavRawItem(1, 1, "b", "/<backend>/b", 2, "", false),
            new CpNavRawItem(2, 1, "a", "/<backend>/a", 1, "", false),
            new CpNavRawItem(3, 2, "c", "/<backend>/c", 1, "", false),
            new CpNavRawItem(4, 3, "d", "/<backend>/d", 1, "", false),
        };

        var result = CpNavTree.Build(groups, items, CpNavPolicy.SuperOperator(), NoTranslate);

        Assert.Equal(new[] { "740", "741", "742" }, result.Select(g => g.CaptionKey).ToArray());
        Assert.Equal(new[] { "/cp/a", "/cp/b" }, result[0].Items.Select(i => i.Url).ToArray());
    }

    [Fact]
    public void Build_OmitsGroupsWhoseItemsAreAllFiltered()
    {
        var groups = new[] { new CpNavRawGroup(1, "740", 1), new CpNavRawGroup(2, "741", 2) };
        var items = new[]
        {
            new CpNavRawItem(1, 1, "x", "/<backend>/shop/orders", 1, "", false),
            new CpNavRawItem(2, 2, "y", "/<backend>/control/portal/epc_tenant_features", 1, "", false),
        };

        var result = CpNavTree.Build(groups, items, CpNavPolicy.Tenant(), NoTranslate);

        Assert.Single(result);
        Assert.Equal("740", result[0].CaptionKey);
    }

    [Fact]
    public void Acl_DeniedItemsHidden_UnlessShowAnyway_OrSuperAdmin()
    {
        var groups = new[] { new CpNavRawGroup(1, "740", 1) };
        var items = new[]
        {
            new CpNavRawItem(1, 1, "Orders", "/<backend>/shop/orders", 1, "", false),
            new CpNavRawItem(2, 1, "Carts", "/<backend>/shop/carts", 2, "", true),
            new CpNavRawItem(3, 1, "Users", "/<backend>/users/usermanager", 3, "", false),
        };

        var tenant = CpNavPolicy.Tenant(acl: url => url == "users/usermanager");
        var seen = CpNavTree.Build(groups, items, tenant, NoTranslate).Single().Items.Select(i => i.Url).ToArray();
        Assert.Equal(new[] { "/cp/shop/carts", "/cp/users/usermanager" }, seen);

        var super = CpNavTree.Build(groups, items, CpNavPolicy.SuperOperator(), NoTranslate).Single().Items;
        Assert.Equal(3, super.Count);
    }

    [Fact]
    public void ContentUrl_StripsBackendPrefixAndQuery_LikePhpAclLookup()
    {
        Assert.Equal("shop/orders", CpNavTree.ContentUrl("/cp/shop/orders?tab=1"));
        Assert.Equal("users/usermanager/user", CpNavTree.ContentUrl("/cp/users/usermanager/user"));
    }

    [Fact]
    public void Tenant_HidesSuperOnly_TenantFeatures_AndOperatorGroup()
    {
        var rows = Snapshot();
        var tenant = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.Tenant(), NoTranslate);
        var urls = tenant.SelectMany(g => g.Items).Select(i => i.Url).ToList();

        Assert.DoesNotContain(urls, u => u.Contains("epc_super_cp_", StringComparison.Ordinal));
        Assert.DoesNotContain(urls, u => u.Contains("epc_tenant_features", StringComparison.Ordinal));
        Assert.DoesNotContain(urls, u => u.Contains("epc_pos_tenant_manage", StringComparison.Ordinal));
        Assert.DoesNotContain(tenant, g => g.CaptionKey == "epc_cp_group_operator");
        Assert.Contains(urls, u => u.Contains("epc_tenant_email_settings", StringComparison.Ordinal));
    }

    [Fact]
    public void SuperHost_HidesTenantEmailSettings_ShowsOperatorGroup()
    {
        var rows = Snapshot();
        var super = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.SuperOperator(), NoTranslate);
        var urls = super.SelectMany(g => g.Items).Select(i => i.Url).ToList();

        Assert.DoesNotContain(urls, u => u.Contains("epc_tenant_email_settings", StringComparison.Ordinal));
        Assert.Contains(super, g => g.CaptionKey == "epc_cp_group_operator");
        Assert.Contains(urls, u => u.Contains("epc_super_cp_", StringComparison.Ordinal));
    }

    [Fact]
    public void Packs_CorePackOnly_DropsErpPackRoutes_AllPacksKeepsThem()
    {
        var rows = Snapshot();
        var core = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.Tenant(packs: CpNavPolicy.TenantDefaultPacks), NoTranslate);
        var all = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.Tenant(packs: CpNavTree.AllTenantPackKeys), NoTranslate);

        var coreUrls = core.SelectMany(g => g.Items).Select(i => i.Url).ToHashSet(StringComparer.Ordinal);
        var allUrls = all.SelectMany(g => g.Items).Select(i => i.Url).ToHashSet(StringComparer.Ordinal);

        Assert.True(allUrls.Count > coreUrls.Count, "full pack set must expose more PHP items than core only");
        Assert.True(coreUrls.IsSubsetOf(allUrls));
        Assert.Contains(coreUrls, u => u.EndsWith("/control/config", StringComparison.Ordinal));
        Assert.DoesNotContain(coreUrls, u => u.Contains("/shop/orders", StringComparison.Ordinal));
        Assert.Contains(allUrls, u => u.Contains("/shop/orders", StringComparison.Ordinal));
    }

    [Fact]
    public void Features_DisabledIntegrationHidesItsMenuPatterns()
    {
        var groups = new[] { new CpNavRawGroup(1, "epc_cp_group_integrations", 1) };
        var items = new[]
        {
            new CpNavRawItem(1, 1, "WhatsApp", "/<backend>/control/portal/epc_whatsapp_settings", 1, "", false),
            new CpNavRawItem(2, 1, "Hub", "/<backend>/control/portal/epc_integrations_hub", 2, "", false),
        };

        var enabled = CpNavTree.Build(groups, items, CpNavPolicy.Tenant(), NoTranslate).Single().Items.Count;

        var disabledFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (feature, patterns) in CpNavTree.FeatureMenuPatterns)
        {
            if (patterns.Any(p => "/cp/control/portal/epc_whatsapp_settings".Contains(p, StringComparison.Ordinal)))
            {
                disabledFeatures.Add(feature);
            }
        }

        Assert.NotEmpty(disabledFeatures);
        var policy = CpNavPolicy.Tenant() with { DisabledFeatures = disabledFeatures };
        var disabled = CpNavTree.Build(groups, items, policy, NoTranslate).Single().Items;

        Assert.Equal(2, enabled);
        Assert.Single(disabled);
        Assert.EndsWith("epc_integrations_hub", disabled[0].Url, StringComparison.Ordinal);
    }

    [Fact]
    public void HiddenGroupsAndItems_FromTenantCpMenuPolicy_AreRemoved()
    {
        var groups = new[] { new CpNavRawGroup(1, "740", 1), new CpNavRawGroup(2, "741", 2) };
        var items = new[]
        {
            new CpNavRawItem(10, 1, "a", "/<backend>/a", 1, "", false),
            new CpNavRawItem(11, 1, "b", "/<backend>/b", 2, "", false),
            new CpNavRawItem(12, 2, "c", "/<backend>/c", 1, "", false),
        };

        var policy = CpNavPolicy.Tenant() with { HiddenGroups = new HashSet<int> { 2 }, HiddenItems = new HashSet<int> { 11 } };
        var result = CpNavTree.Build(groups, items, policy, NoTranslate);

        var g = Assert.Single(result);
        Assert.Equal(new[] { "/cp/a" }, g.Items.Select(i => i.Url).ToArray());
    }

    [Fact]
    public void Labels_NamedKeyAndNumericIdResolveViaTranslator_FallbackHumanises()
    {
        string? Translate(string key) => key switch
        {
            "747" => "User groups",
            "epc_cp_group_customers" => "Customers",
            _ => null,
        };

        Assert.Equal("User groups", CpNavTree.ResolveLabel("747", "/cp/users/usergroups", Translate));
        Assert.Equal("Customers", CpNavTree.ResolveLabel("epc_cp_group_customers", string.Empty, Translate));
        Assert.Equal("Plain caption", CpNavTree.ResolveLabel("Plain caption", "/cp/x", Translate));

        var fallback = CpNavTree.ResolveLabel("999", "/cp/control/portal/epc_price_management", Translate);
        Assert.DoesNotContain("999", fallback, StringComparison.Ordinal);
        Assert.Contains("Price", fallback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Groups_UsePhpIconsShortLabelsAndSubtitles()
    {
        var rows = Snapshot();
        var groups = CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.SuperOperator(), NoTranslate);

        Assert.All(groups, g =>
        {
            Assert.False(string.IsNullOrWhiteSpace(g.Icon));
            Assert.False(string.IsNullOrWhiteSpace(g.Short));
            Assert.False(string.IsNullOrWhiteSpace(g.Caption));
            Assert.False(string.IsNullOrWhiteSpace(g.Key));
        });

        var erp = groups.Single(g => g.CaptionKey == "epc_cp_group_erp");
        Assert.Equal(CpNavTree.GroupIcons["epc_cp_group_erp"], erp.Icon);
        Assert.Equal(CpNavTree.GroupShortLabels["epc_cp_group_erp"], erp.Short);
    }

    [Fact]
    public void Chunk_SplitsIntoColumnsOfEight_LikePhpArrayChunk()
    {
        var items = Enumerable.Range(1, 19)
            .Select(i => new CpNavItem(i, "c" + i, "L" + i, "/cp/x" + i, string.Empty, false))
            .ToArray();

        var cols = CpNavTree.Chunk(items);

        Assert.Equal(3, cols.Count);
        Assert.Equal(8, cols[0].Count);
        Assert.Equal(8, cols[1].Count);
        Assert.Equal(3, cols[2].Count);
        Assert.Equal("/cp/x9", cols[1][0].Url);
    }

    [Fact]
    public void Build_NeverInventsLinks_EveryUrlComesFromControlItems()
    {
        var rows = Snapshot();
        var source = rows.Items.Select(i => CpNavTree.ResolveUrl(i.Url)).ToHashSet(StringComparer.Ordinal);

        foreach (var policy in new[] { CpNavPolicy.SuperOperator(), CpNavPolicy.Tenant() })
        {
            var groups = CpNavTree.Build(rows.Groups, rows.Items, policy, NoTranslate);
            Assert.All(groups.SelectMany(g => g.Items), i => Assert.Contains(i.Url, source));

            foreach (var group in groups)
            {
                var keys = group.Items.Select(i => CpNavTree.DedupeKey(i.Url)).ToList();
                Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
            }
        }
    }

    [Fact]
    public void SiteKey_MatchesPhpIntegrationsSiteKey()
    {
        Assert.Equal("ecomae-com", CpNavMenuService.SiteKey("www.ecomae.com"));
        Assert.Equal("cp-ecomae-com", CpNavMenuService.SiteKey("cp.ecomae.com"));
        Assert.Equal("", CpNavMenuService.SiteKey(null));
    }

    [Fact]
    public void CpChrome_RendersCpNavTree_NotStaticCatalog()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));

        Assert.Contains("ICpNavMenuService", src);
        Assert.Contains("CpNavTree.Chunk(group.Items)", src);
        Assert.Contains("PhpSurfaceLinkMap.AspNetPrimaryHref(link.Url)", src);
        Assert.Contains("epc-cp-topnav-panel-hub", src);
        Assert.DoesNotContain("LegacyDesktopChromeCatalog.ControlPanelTopnav", src);
        Assert.DoesNotContain("QuickAction", src);
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

        throw new FileNotFoundException(relative);
    }
}
