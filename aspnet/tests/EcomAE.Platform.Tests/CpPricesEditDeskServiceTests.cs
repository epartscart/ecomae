using System.Linq;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPricesEditDeskServiceTests
{
    [Fact]
    public void Empty_filter_produces_no_where_clause()
    {
        var (where, args) = CpPricesEditDeskService.BuildWhere(new CpPricesEditFilter());
        Assert.Equal(string.Empty, where);
        Assert.Empty(args);
    }

    [Fact]
    public void Exact_filters_mirror_php_get_where()
    {
        var (where, args) = CpPricesEditDeskService.BuildWhere(new CpPricesEditFilter(
            PriceId: 7,
            Article: "gk-1 234",
            Manufacturer: " bosch ",
            NoManufacturer: true));

        Assert.Equal("WHERE (`price_id` IN(?)) AND (`article` LIKE ?) AND (`manufacturer` LIKE ?) AND (`manufacturer` LIKE ?)", where);
        Assert.Equal(7L, args[0]);
        Assert.Equal("GK1234", args[1]);
        Assert.Equal("BOSCH", args[2]);
        Assert.Equal("", args[3]);
    }

    [Fact]
    public void Fuzzy_search_only_uses_terms_of_three_or_more_chars_across_three_columns()
    {
        var (where, args) = CpPricesEditDeskService.BuildWhere(new CpPricesEditFilter(SearchText: "ab brake pad"));

        Assert.Equal(
            "WHERE (((`article` LIKE ?) AND (`article` LIKE ?))"
            + " OR ((`manufacturer` LIKE ?) AND (`manufacturer` LIKE ?))"
            + " OR ((`name` LIKE ?) AND (`name` LIKE ?)))",
            where);
        Assert.Equal(new object?[] { "%brake%", "%pad%", "%brake%", "%pad%", "%brake%", "%pad%" }, args);
    }

    [Fact]
    public void Site_url_matches_php_part_search_link()
    {
        Assert.Equal("/shop/part_search?article=GK1234", CpPricesEditDeskService.SiteUrl("GK1234", " "));
        Assert.Equal("/shop/part_search?article=GK1234&brend=BOSCH", CpPricesEditDeskService.SiteUrl("GK1234", "BOSCH"));
    }

    [Fact]
    public void Paging_matches_php_page_size()
    {
        Assert.Equal(20, CpPricesEditFilter.PageSize);
        Assert.Equal(1, new CpPricesEditFilter(Page: 0).SafePage);
        Assert.Equal(3, new CpPricesEditFilter(Page: 3).SafePage);

        var desk = new CpPricesEditDesk(true, "", [], [], [], 41, 1, "Site price");
        Assert.Equal(3, desk.PageCount);
        Assert.Equal(1, CpPricesEditDesk.Unavailable("db down").PageCount);
        Assert.False(CpPricesEditDesk.Unavailable("db down").Available);
    }
}
