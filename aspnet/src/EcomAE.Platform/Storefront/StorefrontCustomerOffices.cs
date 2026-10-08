using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/get_customer_offices.php</c>: the pickup offices of the visitor's
/// geo node (<c>my_city</c> cookie, else the first <c>shop_geo</c> row), else the first office.
/// </summary>
public static class StorefrontCustomerOffices
{
    public const string CityCookie = "my_city";

    /// <summary>
    /// PHP binds the raw cookie string, so MySQL compares it numerically (<c>"5abc"</c> is geo 5).
    /// An empty cookie is <c>== NULL</c> in PHP and falls back to the first geo node.
    /// </summary>
    public static async Task<List<int>> LoadAsync(
        DbConnection connection,
        string? myCityCookie,
        CancellationToken cancellationToken,
        bool missingGeoTablesAsEmpty = false)
    {
        var offices = new List<int>();
        try
        {
            object? geoId = string.IsNullOrEmpty(myCityCookie) ? null : myCityCookie;
            if (geoId is null)
            {
                await using var min = connection.CreateCommand();
                min.CommandText = "SELECT MIN(`id`) AS `id` FROM `shop_geo`;";
                geoId = await min.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var map = connection.CreateCommand();
            map.CommandText = ErpDb.Positional("SELECT `office_id` FROM `shop_offices_geo_map` WHERE `geo_id` = ?;");
            ErpDb.AddParameters(map, geoId is null or DBNull ? DBNull.Value : geoId);
            await using var reader = await map.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offices.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex) when (missingGeoTablesAsEmpty && Migration.CpMissingSchema.IsMissing(ex))
        {
            offices.Clear();
        }

        if (offices.Count == 0)
        {
            await using var first = connection.CreateCommand();
            first.CommandText = "SELECT `id` FROM `shop_offices` ORDER BY `id` LIMIT 1;";
            var office = await first.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (office is not null and not DBNull)
            {
                offices.Add(Convert.ToInt32(office, CultureInfo.InvariantCulture));
            }
        }

        return offices;
    }
}
