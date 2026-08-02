namespace EcomAE.Platform.Api.Catalog;

public static class PriceOfferRowMapper
{
    public static PriceOfferRow FromLegacyPriceLookup(
        string? manufacturer,
        string? article,
        string? name,
        object? price,
        object? exist,
        string? storage,
        string? timeToExe)
    {
        return new PriceOfferRow(
            string.IsNullOrWhiteSpace(storage) ? "default" : storage,
            manufacturer ?? string.Empty,
            article ?? string.Empty,
            name ?? string.Empty,
            ConvertDecimal(price),
            ConvertInt(exist),
            timeToExe ?? string.Empty);
    }

    private static decimal ConvertDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0m;
        }

        return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int ConvertInt(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
