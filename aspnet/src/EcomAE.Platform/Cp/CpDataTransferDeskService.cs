using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Pickup point from <c>shop_offices</c> offered as an export scope.</summary>
public sealed record CpDataTransferOffice(long Id, string Caption);

/// <summary>Customer group from <c>groups</c>, the price group the export is priced for.</summary>
public sealed record CpDataTransferGroup(long Id, string Caption);

/// <summary>Own warehouse (<c>shop_storages.interface_type = 1</c>) the signed-in admin may import into.</summary>
public sealed record CpDataTransferStorage(long Id, string Name);

/// <summary>Leaf category of <c>shop_catalogue_categories</c> with the properties a CSV column can be mapped to.</summary>
public sealed record CpDataTransferCategory(
    long Id,
    long ParentId,
    int Level,
    string Caption,
    bool Published,
    bool Leaf,
    IReadOnlyList<CpDataTransferProperty> Properties);

public sealed record CpDataTransferProperty(long Id, string Caption, long PropertyTypeId);

public sealed record CpDataTransferDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpDataTransferOffice> Offices,
    IReadOnlyList<CpDataTransferGroup> Groups,
    IReadOnlyList<CpDataTransferStorage> Storages,
    IReadOnlyList<CpDataTransferCategory> Categories)
{
    public static CpDataTransferDesk Unavailable(string message)
        => new(false, message, [], [], [], []);
}

/// <summary>
/// Typed twin of the PHP data-transfer pages (<c>cp/content/shop/data_transfer/pages/*</c> and
/// <c>catalogue_csv_import.php</c>): the option sources those pages render — offices, price group, own warehouses
/// and the catalogue tree with per-category properties.
/// </summary>
public interface ICpDataTransferDeskService
{
    Task<CpDataTransferDesk> LoadAsync(long adminUserId, CancellationToken cancellationToken = default);
}

public sealed class CpDataTransferDeskService : ICpDataTransferDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpDataTransferDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP checks the storage's <c>users</c> JSON array for the admin id before offering it for import.</summary>
    public static bool StorageAllowsUser(string usersJson, long adminUserId)
    {
        if (string.IsNullOrWhiteSpace(usersJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(usersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var value = element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetInt64(out var n) ? n : -1,
                    JsonValueKind.String => long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : -1,
                    _ => -1
                };

                if (value == adminUserId)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public async Task<CpDataTransferDesk> LoadAsync(long adminUserId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpDataTransferDesk.Unavailable("No database configured — catalogue export and import are unavailable.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var offices = new List<CpDataTransferOffice>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`caption`,'') FROM `shop_offices` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    offices.Add(new CpDataTransferOffice(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
                }
            }

            var groups = new List<CpDataTransferGroup>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`value`,'') FROM `groups` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add(new CpDataTransferGroup(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
                }
            }

            var storages = new List<CpDataTransferStorage>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`users`,'') FROM `shop_storages` WHERE `interface_type` = 1 ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!StorageAllowsUser(r.GetString(2), adminUserId))
                    {
                        continue;
                    }

                    storages.Add(new CpDataTransferStorage(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
                }
            }

            var categories = await LoadCategoriesAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpDataTransferDesk(true, "", offices, groups, storages, categories);
        }
        catch (DbException ex)
        {
            return CpDataTransferDesk.Unavailable("Catalogue transfer tables are unavailable: " + ex.Message);
        }
    }

    private static async Task<IReadOnlyList<CpDataTransferCategory>> LoadCategoriesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<(long Id, long ParentId, int Level, string Caption, bool Published)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`parent_id`,0), IFNULL(`level`,0), IFNULL(`caption`,''), IFNULL(`published_flag`,0) "
                              + "FROM `shop_catalogue_categories` ORDER BY `level`, `order`, `id`";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture),
                    r.GetString(3),
                    Convert.ToInt64(r.GetValue(4), CultureInfo.InvariantCulture) != 0));
            }
        }

        var properties = new Dictionary<long, List<CpDataTransferProperty>>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `category_id`, `id`, IFNULL(`value`,''), IFNULL(`property_type_id`,0) "
                              + "FROM `shop_categories_properties_map` ORDER BY `order`, `id`";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var categoryId = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                if (!properties.TryGetValue(categoryId, out var list))
                {
                    list = [];
                    properties[categoryId] = list;
                }

                list.Add(new CpDataTransferProperty(
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.GetString(2),
                    Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture)));
            }
        }

        var parents = rows.Select(row => row.ParentId).ToHashSet();
        return rows
            .Select(row => new CpDataTransferCategory(
                row.Id,
                row.ParentId,
                row.Level,
                row.Caption,
                row.Published,
                !parents.Contains(row.Id),
                properties.TryGetValue(row.Id, out var list) ? list : []))
            .ToList();
    }
}
