using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>prices_enclosure</c> / <c>shop_offices_storages_map.markup</c>:
/// customer sell = purchase + purchase × (markup/100).
/// </summary>
public static class StorefrontOfferMarkup
{
    public static StorefrontPartOfferDigest Apply(StorefrontPartOfferDigest row, decimal markupDecimal)
    {
        var purchase = row.PricePurchase > 0 ? row.PricePurchase : row.Price;
        var sell = Math.Round(purchase + purchase * markupDecimal, 2, MidpointRounding.AwayFromZero);
        var percent = (int)Math.Round(markupDecimal * 100m, MidpointRounding.AwayFromZero);
        return row with
        {
            PricePurchase = purchase,
            Price = sell,
            Markup = percent
        };
    }

    public static decimal PickMarkup(
        decimal purchase,
        IReadOnlyList<(int StorageId, int GroupId, decimal Min, decimal Max, decimal Markup)> ranges,
        int storageId,
        int groupId)
    {
        foreach (var r in ranges)
        {
            if (storageId > 0 && r.StorageId != storageId)
            {
                continue;
            }

            if (groupId > 0 && r.GroupId != groupId && r.GroupId != 0)
            {
                continue;
            }

            if (purchase >= r.Min && purchase <= r.Max)
            {
                return r.Markup;
            }
        }

        return 0m;
    }
}
