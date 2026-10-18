using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRealEstateLawTests
{
    private static ErpRealEstateLawFile Sample(
        string emirate = "dubai",
        string kind = "lease",
        string use = "residential",
        bool registered = true,
        int term = 1,
        decimal current = 100000m,
        decimal average = 100000m,
        decimal proposed = 100000m,
        string escrow = "na",
        bool foreign = false,
        string designated = "unknown",
        string first = "unknown",
        string claimed = "",
        decimal amount = 0m)
        => new(emirate, kind, use, registered, term, current, average, proposed, escrow, foreign, designated, first, claimed, amount);

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(95, 100, 0)]
    [InlineData(85, 100, 0.05)]
    [InlineData(75, 100, 0.10)]
    [InlineData(65, 100, 0.15)]
    [InlineData(50, 100, 0.20)]
    public void Dubai_rent_cap_follows_decree_43(decimal current, decimal average, decimal cap)
    {
        Assert.Equal(cap, ErpRealEstateLaw.DubaiIncreaseRate(current, average));
    }

    [Fact]
    public void Dubai_renewal_above_the_index_is_blocked()
    {
        var decision = ErpRealEstateLaw.Check(Sample(current: 80000m, average: 100000m, proposed: 90000m));
        Assert.False(decision.Allowed);
        Assert.Contains(ErpRealEstateLaw.DubaiRent, decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dubai_renewal_inside_the_index_is_allowed()
    {
        var decision = ErpRealEstateLaw.Check(Sample(current: 80000m, average: 100000m, proposed: 84000m));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Abu_Dhabi_renewal_stops_at_five_percent()
    {
        var over = ErpRealEstateLaw.Check(Sample(emirate: "abu_dhabi", current: 100000m, proposed: 106000m));
        var inside = ErpRealEstateLaw.Check(Sample(emirate: "abu_dhabi", current: 100000m, proposed: 105000m));
        Assert.False(over.Allowed);
        Assert.Contains(ErpRealEstateLaw.AbuDhabiCap, over.Message, StringComparison.Ordinal);
        Assert.True(inside.Allowed);
    }

    [Fact]
    public void Unregistered_Dubai_sale_and_offplan_without_escrow_are_blocked()
    {
        var sale = ErpRealEstateLaw.Check(Sample(kind: "sale", registered: false));
        var offplan = ErpRealEstateLaw.Check(Sample(kind: "offplan", escrow: "no", registered: true));
        Assert.False(sale.Allowed);
        Assert.Contains("Ejari", sale.Message, StringComparison.Ordinal);
        Assert.False(offplan.Allowed);
        Assert.Contains(ErpRealEstateLaw.DubaiEscrow, offplan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ownership_outside_a_designated_area_is_blocked()
    {
        var decision = ErpRealEstateLaw.Check(Sample(kind: "sale", foreign: true, designated: "no"));
        Assert.False(decision.Allowed);
        Assert.Contains("designated investment area", decision.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bare_land", "sale", "no", "exempt")]
    [InlineData("residential", "lease", "no", "exempt")]
    [InlineData("residential", "sale", "yes", "zero")]
    [InlineData("residential", "sale", "no", "exempt")]
    [InlineData("commercial", "lease", "no", "standard")]
    [InlineData("hotel", "lease", "no", "standard")]
    public void Vat_follows_the_use_of_the_property(string use, string kind, string first, string code)
    {
        Assert.Equal(code, ErpRealEstateLaw.VatTreatment(use, kind, first).Code);
    }

    [Fact]
    public void A_wrong_vat_claim_is_blocked_and_the_civil_code_is_named()
    {
        var decision = ErpRealEstateLaw.Check(Sample(kind: "lease", use: "residential", claimed: "standard"));
        Assert.False(decision.Allowed);
        Assert.Contains("Exempt", decision.Message, StringComparison.Ordinal);
        Assert.Contains(ErpRealEstateLaw.VatLaw, decision.Message, StringComparison.Ordinal);
        Assert.Contains(ErpRealEstateLaw.CivilCode, string.Join(" ", ErpRealEstateLaw.Cards.Select(card => card.Body)), StringComparison.Ordinal);
        Assert.Contains("1 June 2026", ErpRealEstateLaw.CivilCodeFrom, StringComparison.Ordinal);
        Assert.Equal(55_000m, ErpRealEstateLaw.AmlLineAed);
    }

    [Fact]
    public void The_law_tab_is_on_the_guide_and_stays_with_real_estate()
    {
        var root = RepoRoot();
        Assert.Contains(ErpRealEstateWorkspace.Tabs, tab => tab.Key == "compliance");
        Assert.Contains(ErpRealEstateGuide.Steps, step => step.Key == "compliance");
        Assert.Equal("guide", ErpRealEstateWorkspace.Tabs[0].Key);
        var link = PhpModuleCatalog.AspNetErpTabs.Single(tab => tab.Id == "projects/real_estate_compliance");
        Assert.Equal("/erp/real-estate-app?tab=compliance", link.Href);
        Assert.True(ErpIndustryNav.IsRealEstateTab(link));
        Assert.True(ErpPhpTabRouteMap.TryMapTab("real_estate_compliance", out var href));
        Assert.Equal("/erp/real-estate-app?tab=compliance", href);
        var board = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/ErpRealEstateLawBoard.razor"));
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor"));
        Assert.Contains("ErpRealEstateLawBoard", page, StringComparison.Ordinal);
        Assert.Contains("Fetch real-estate legislation", board, StringComparison.Ordinal);
        Assert.Contains("/erp/real-estate/legislation-fetch", board, StringComparison.Ordinal);
        Assert.Contains("ErpRealEstateLaw.DubaiRent", board, StringComparison.Ordinal);
        Assert.Contains("ErpRealEstateLaw.CivilCode", board, StringComparison.Ordinal);
        Assert.Contains("#1f4e79 !important", board, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", board, StringComparison.Ordinal);
        Assert.DoesNotContain("@section", board, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", board, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", board, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", board, StringComparison.Ordinal);
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ErpRealEstateLawFetch = \"/erp/real-estate/legislation-fetch\"", routes, StringComparison.Ordinal);
        Assert.Contains("IErpRealEstateLawFetchWriteService", program, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
