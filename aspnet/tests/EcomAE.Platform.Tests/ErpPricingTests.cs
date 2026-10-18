using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPricingTests
{
    [Fact]
    public void CompanyStructure_MarksUpCostIntoFourPrices()
    {
        var quote = ErpPriceMath.Quote(100m, ErpMarginStructure.Standard, "Company structure");
        Assert.Equal(125m, quote.UnitPrice);
        Assert.Equal(112m, quote.OfferPrice);
        Assert.Equal(118m, quote.ActualPrice);
        Assert.Equal(108m, quote.MspPrice);
        Assert.Equal("AED", quote.CurrencyCode);
        Assert.Equal(string.Empty, quote.Note);
    }

    [Fact]
    public void OfferUnderTheFloor_RisesToTheMinimumSellingPrice()
    {
        var quote = ErpPriceMath.Quote(100m, new ErpMarginStructure(25m, 4m, 18m, 8m, "AED"), "Company structure");
        Assert.Equal(125m, quote.UnitPrice);
        Assert.Equal(108m, quote.OfferPrice);
        Assert.Equal(118m, quote.ActualPrice);
        Assert.Equal(108m, quote.MspPrice);
        Assert.Contains("raised", quote.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroCost_DoesNotInventAPrice()
    {
        var quote = ErpPriceMath.Resolve(ErpMarginStructure.Standard, [], new ErpPriceAsk(0m, "retail", "AE", "", "", "", 1m, "AED"));
        Assert.Equal(0m, quote.UnitPrice);
        Assert.Equal(0m, quote.MspPrice);
        Assert.Contains("cost", quote.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClosestLevelWins_AndABlankChannelDoesNotTakeAChannelRule()
    {
        var rules = new List<ErpPriceRule>
        {
            Level(1, channel: "wholesale", unit: 20m),
            Level(2, channel: "retail", unit: 40m),
            Level(3, country: "AE", unit: 15m),
            Level(4, group: "VIP", unit: 22m),
            Level(5, customer: "C1", unit: 10m, msp: 8m),
            Level(6, item: "SKU", unit: 30m),
            Level(7, item: "SKU", minQty: 1m, unit: 28m),
            Level(8, item: "SKU", minQty: 10m, unit: 18m),
            Level(9, currency: "USD", unit: 50m),
        };

        var customer = Quote(rules, channel: "wholesale", customer: "C1");
        Assert.Equal(110m, customer.UnitPrice);
        Assert.Equal(108m, customer.MspPrice);
        Assert.Contains("customer C1", customer.Level, StringComparison.Ordinal);

        var group = Quote(rules, group: "VIP", country: "AE");
        Assert.Equal(122m, group.UnitPrice);
        Assert.Contains("group VIP", group.Level, StringComparison.Ordinal);

        var country = Quote(rules, channel: "retail", country: "AE");
        Assert.Equal(115m, country.UnitPrice);
        Assert.Contains("country AE", country.Level, StringComparison.Ordinal);

        var item = Quote(rules, channel: "wholesale", item: "SKU", qty: 2m);
        Assert.Equal(128m, item.UnitPrice);
        Assert.Contains("from 1", item.Level, StringComparison.Ordinal);

        var band = Quote(rules, item: "SKU", qty: 12m);
        Assert.Equal(118m, band.UnitPrice);
        Assert.Contains("from 10", band.Level, StringComparison.Ordinal);

        var open = Quote(rules, channel: "");
        Assert.Equal(125m, open.UnitPrice);
        Assert.Equal("Company structure", open.Level);

        var otherCurrency = Quote(rules, currency: "AED");
        Assert.Equal(125m, otherCurrency.UnitPrice);
        var dollars = Quote(rules, currency: "USD");
        Assert.Equal(150m, dollars.UnitPrice);
        Assert.Equal("USD", dollars.CurrencyCode);
    }

    [Fact]
    public void SameScore_PrefersTheHigherQuantityThenTheLaterRule()
    {
        var earlier = Level(3, customer: "C1", unit: 11m);
        var later = Level(9, customer: "C1", unit: 16m);
        var quote = ErpPriceMath.Resolve(
            ErpMarginStructure.Standard,
            [earlier, later],
            new ErpPriceAsk(100m, "", "", "", "C1", "", 1m, "AED"));
        Assert.Equal(116m, quote.UnitPrice);
    }

    [Fact]
    public void Page_OffersTheFourPricesAndTheLevels()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPricingApp.razor"));
        Assert.Contains("@page \"/erp/pricing-app\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"wholesale\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"retail\"", page, StringComparison.Ordinal);
        Assert.Contains("Unit price", page, StringComparison.Ordinal);
        Assert.Contains("Offer price", page, StringComparison.Ordinal);
        Assert.Contains("Actual price", page, StringComparison.Ordinal);
        Assert.Contains(">MSP<", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);
        Assert.Contains("customer group", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.Equal("/erp/pricing-app", EcomAeRoutes.ErpPricingApp);
        Assert.Equal("/erp/pricing/save", EcomAeRoutes.ErpPricingSave);
        Assert.Equal("/erp/pricing-app", ErpPhpTabRouteMap.All["pricing"]);
        Assert.Contains(
            ErpProcessFlowCatalog.Areas.SelectMany(area => area.Steps),
            step => step.Label == "Pricing" && step.Href == "/erp/pricing-app");
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandlePricingSaveAsync", module, StringComparison.Ordinal);
        Assert.Contains("writesBlocked", module, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavedLevels_ResolveAndRejectAPriceUnderTheFloor()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_px_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using (var adminConn = new MySqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using var create = new MySqlCommand("CREATE DATABASE `" + database + "`", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var pricing = new ErpPricingService(new PricingConnections(cs));
            var seeded = await pricing.ReadAsync();
            Assert.Equal("live", seeded.Source);
            Assert.Equal(25m, seeded.Structure.UnitPercent);
            Assert.Equal(12m, seeded.Structure.OfferPercent);
            Assert.Equal(18m, seeded.Structure.ActualPercent);
            Assert.Equal(8m, seeded.Structure.MspPercent);
            Assert.Equal("AED", seeded.Structure.CurrencyCode);
            Assert.Empty(seeded.Rules);

            var structure = await pricing.SaveAsync(Save("structure", unit: 30m, offer: 14m, actual: 20m, msp: 9m, currency: "AED"));
            Assert.True(structure.Succeeded, structure.Message);

            var bare = await pricing.SaveAsync(Save("rule"));
            Assert.False(bare.Succeeded);
            Assert.Equal("level", bare.Code);

            var under = await pricing.SaveAsync(Save("rule", channel: "retail", ruleUnit: 5m, ruleMsp: 8m));
            Assert.False(under.Succeeded);
            Assert.Equal("margin", under.Code);

            var wholesale = await pricing.SaveAsync(Save("rule", channel: "wholesale", ruleUnit: 20m));
            Assert.True(wholesale.Succeeded, wholesale.Message);
            var customer = await pricing.SaveAsync(Save("rule", customer: "C1", ruleUnit: 10m, ruleMsp: 8m));
            Assert.True(customer.Succeeded, customer.Message);

            var live = await pricing.ReadAsync();
            Assert.Equal(30m, live.Structure.UnitPercent);
            Assert.Equal(2, live.Rules.Count);
            var customerRule = Assert.Single(live.Rules, rule => rule.CustomerCode == "C1");
            Assert.Equal(10m, customerRule.UnitPercent);
            Assert.Equal(-1m, customerRule.OfferPercent);
            Assert.Equal(8m, customerRule.MspPercent);
            Assert.Equal(0m, customerRule.MinQty);

            var forCustomer = ErpPriceMath.Resolve(
                live.Structure,
                live.Rules,
                new ErpPriceAsk(100m, "wholesale", "", "", "C1", "", 1m, "AED"));
            Assert.Equal(110m, forCustomer.UnitPrice);
            Assert.Equal(114m, forCustomer.OfferPrice);
            Assert.Equal(120m, forCustomer.ActualPrice);
            Assert.Equal(108m, forCustomer.MspPrice);

            var forWholesale = ErpPriceMath.Resolve(
                live.Structure,
                live.Rules,
                new ErpPriceAsk(100m, "wholesale", "", "", "", "", 1m, "AED"));
            Assert.Equal(120m, forWholesale.UnitPrice);
            Assert.Equal(114m, forWholesale.OfferPrice);
            Assert.Equal(109m, forWholesale.MspPrice);

            var turnedOff = await pricing.SaveAsync(Save("deactivate", ruleId: customerRule.Id));
            Assert.True(turnedOff.Succeeded, turnedOff.Message);
            var after = await pricing.ReadAsync();
            Assert.DoesNotContain(after.Rules, rule => rule.CustomerCode == "C1");
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static ErpPriceQuote Quote(
        IReadOnlyList<ErpPriceRule> rules,
        string channel = "",
        string country = "",
        string group = "",
        string customer = "",
        string item = "",
        decimal qty = 1m,
        string currency = "AED")
        => ErpPriceMath.Resolve(
            ErpMarginStructure.Standard,
            rules,
            new ErpPriceAsk(100m, channel, country, group, customer, item, qty, currency));

    private static ErpPriceRule Level(
        long id,
        string channel = "any",
        string country = "",
        string group = "",
        string customer = "",
        string item = "",
        decimal minQty = 0m,
        string currency = "",
        decimal unit = -1m,
        decimal offer = -1m,
        decimal actual = -1m,
        decimal msp = -1m)
        => new(id, channel, country, group, customer, item, minQty, currency, unit, offer, actual, msp, true, "");

    private static ErpPricingSaveRequest Save(
        string action,
        decimal unit = 0m,
        decimal offer = 0m,
        decimal actual = 0m,
        decimal msp = 0m,
        string currency = "",
        string channel = "any",
        string customer = "",
        decimal? ruleUnit = null,
        decimal? ruleOffer = null,
        decimal? ruleActual = null,
        decimal? ruleMsp = null,
        long ruleId = 0)
        => new(
            action,
            unit,
            offer,
            actual,
            msp,
            currency,
            channel,
            "",
            "",
            customer,
            "",
            0m,
            action == "rule" ? ruleUnit ?? -1m : -1m,
            action == "rule" ? ruleOffer ?? -1m : -1m,
            action == "rule" ? ruleActual ?? -1m : -1m,
            action == "rule" ? ruleMsp ?? -1m : -1m,
            "",
            ruleId,
            "");

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

    private sealed class PricingConnections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
