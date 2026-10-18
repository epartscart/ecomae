using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpMarginStructure(
    decimal UnitPercent,
    decimal OfferPercent,
    decimal ActualPercent,
    decimal MspPercent,
    string CurrencyCode)
{
    public static ErpMarginStructure Standard { get; } = new(25m, 12m, 18m, 8m, "AED");
}

public sealed record ErpPriceRule(
    long Id,
    string Channel,
    string CountryCode,
    string CustomerGroup,
    string CustomerCode,
    string ItemCode,
    decimal MinQty,
    string CurrencyCode,
    decimal UnitPercent,
    decimal OfferPercent,
    decimal ActualPercent,
    decimal MspPercent,
    bool Active,
    string Note);

public sealed record ErpPriceAsk(
    decimal Cost,
    string Channel,
    string CountryCode,
    string CustomerGroup,
    string CustomerCode,
    string ItemCode,
    decimal Quantity,
    string CurrencyCode);

public sealed record ErpPriceQuote(
    decimal Cost,
    decimal UnitPrice,
    decimal OfferPrice,
    decimal ActualPrice,
    decimal MspPrice,
    string Level,
    string Note,
    string CurrencyCode);

/// <summary>
/// Markup on cost. The minimum selling price is the floor. One matching level wins:
/// customer, then customer group, then country, then item, then wholesale or retail,
/// then a quantity band or currency. A blank margin keeps the company percent.
/// </summary>
public static class ErpPriceMath
{
    public const decimal Inherit = -1m;

    public static decimal Markup(decimal cost, decimal percent)
        => decimal.Round(cost * (1m + percent / 100m), 2, MidpointRounding.AwayFromZero);

    public static ErpMarginStructure Apply(ErpMarginStructure company, ErpPriceRule? rule)
    {
        if (rule is null)
        {
            return company;
        }

        return new ErpMarginStructure(
            rule.UnitPercent >= 0m ? rule.UnitPercent : company.UnitPercent,
            rule.OfferPercent >= 0m ? rule.OfferPercent : company.OfferPercent,
            rule.ActualPercent >= 0m ? rule.ActualPercent : company.ActualPercent,
            rule.MspPercent >= 0m ? rule.MspPercent : company.MspPercent,
            rule.CurrencyCode.Length > 0 ? rule.CurrencyCode : company.CurrencyCode);
    }

    public static ErpPriceQuote Quote(decimal cost, ErpMarginStructure margins, string level)
    {
        if (cost <= 0m)
        {
            return new ErpPriceQuote(0m, 0m, 0m, 0m, 0m, level, "Enter a cost above zero.", margins.CurrencyCode);
        }

        var msp = Markup(cost, margins.MspPercent);
        var unit = Raise(Markup(cost, margins.UnitPercent), msp);
        var offer = Raise(Markup(cost, margins.OfferPercent), msp);
        var actual = Raise(Markup(cost, margins.ActualPercent), msp);
        var note = unit > Markup(cost, margins.UnitPercent)
            || offer > Markup(cost, margins.OfferPercent)
            || actual > Markup(cost, margins.ActualPercent)
            ? "A price sat under the minimum selling price, so it was raised to that floor."
            : string.Empty;
        return new ErpPriceQuote(
            decimal.Round(cost, 2, MidpointRounding.AwayFromZero),
            unit,
            offer,
            actual,
            msp,
            level,
            note,
            margins.CurrencyCode);
    }

    public static ErpPriceQuote Resolve(ErpMarginStructure company, IReadOnlyList<ErpPriceRule> rules, ErpPriceAsk ask)
    {
        ErpPriceRule? winner = null;
        var best = int.MinValue;
        foreach (var rule in rules)
        {
            if (!rule.Active || !Matches(rule, ask))
            {
                continue;
            }

            var score = Score(rule);
            if (winner is null
                || score > best
                || (score == best && rule.MinQty > winner.MinQty)
                || (score == best && rule.MinQty == winner.MinQty && rule.Id > winner.Id))
            {
                winner = rule;
                best = score;
            }
        }

        var margins = Apply(company, winner);
        return Quote(ask.Cost, margins, winner is null ? "Company structure" : LevelName(winner));
    }

    public static int Score(ErpPriceRule rule)
    {
        var score = 1;
        if (rule.CustomerCode.Length > 0)
        {
            score += 1000;
        }

        if (rule.CustomerGroup.Length > 0)
        {
            score += 200;
        }

        if (rule.CountryCode.Length > 0)
        {
            score += 80;
        }

        if (rule.Channel is "wholesale" or "retail")
        {
            score += 20;
        }

        if (rule.ItemCode.Length > 0)
        {
            score += 40;
        }

        if (rule.CurrencyCode.Length > 0)
        {
            score += 5;
        }

        if (rule.MinQty > 0m)
        {
            score += 10;
        }

        return score;
    }

    public static bool Matches(ErpPriceRule rule, ErpPriceAsk ask)
    {
        if (rule.Channel is "wholesale" or "retail"
            && !string.Equals(rule.Channel, Clean(ask.Channel), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.CountryCode.Length > 0
            && !string.Equals(rule.CountryCode, Clean(ask.CountryCode), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.CustomerGroup.Length > 0
            && !string.Equals(rule.CustomerGroup, Clean(ask.CustomerGroup), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.CustomerCode.Length > 0
            && !string.Equals(rule.CustomerCode, Clean(ask.CustomerCode), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.ItemCode.Length > 0
            && !string.Equals(rule.ItemCode, Clean(ask.ItemCode), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.CurrencyCode.Length > 0
            && !string.Equals(rule.CurrencyCode, Clean(ask.CurrencyCode), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ask.Quantity >= rule.MinQty;
    }

    public static string LevelName(ErpPriceRule rule)
    {
        var parts = new List<string>();
        if (rule.CustomerCode.Length > 0)
        {
            parts.Add("customer " + rule.CustomerCode);
        }

        if (rule.CustomerGroup.Length > 0)
        {
            parts.Add("group " + rule.CustomerGroup);
        }

        if (rule.CountryCode.Length > 0)
        {
            parts.Add("country " + rule.CountryCode);
        }

        if (rule.Channel is "wholesale" or "retail")
        {
            parts.Add(rule.Channel);
        }

        if (rule.ItemCode.Length > 0)
        {
            parts.Add("item " + rule.ItemCode);
        }

        if (rule.MinQty > 0m)
        {
            parts.Add("from " + rule.MinQty.ToString("0.###", CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "Company structure" : string.Join(" · ", parts);
    }

    public static string Clean(string? value)
        => (value ?? string.Empty).Trim();

    private static decimal Raise(decimal price, decimal floor)
        => price < floor ? floor : price;
}
