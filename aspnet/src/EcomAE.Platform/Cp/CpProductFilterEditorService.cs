using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpProductFilterRow(long Id, string Manufacturer, string Article, string Name, bool Active, int StorageCount, decimal MinPrice, decimal MaxPrice, int MinTime, int MaxTime);

public sealed record CpProductFilterList(IReadOnlyList<CpProductFilterRow> Rows, int Total, int Page, int PageSize)
{
    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record CpProductFilterStorage(long Id, string Name, bool Checked);

public sealed record CpProductFilterSetting(
    long Id,
    string Manufacturer,
    string Article,
    string Name,
    bool Active,
    decimal MinPrice,
    decimal MaxPrice,
    int MinTime,
    int MaxTime,
    IReadOnlyList<CpProductFilterStorage> Storages)
{
    /// <summary>PHP <c>setting.php</c> heading: MANUFACTURER - ARTICLE, name.</summary>
    public string Caption
    {
        get
        {
            var head = Manufacturer.ToUpperInvariant();
            if (Article.Length > 0)
            {
                head = head.Length == 0 ? Article : head + " - " + Article;
            }

            return head.Length == 0 && Name.Length == 0 ? "Filter #" + Id.ToString(CultureInfo.InvariantCulture) : head;
        }
    }
}

public interface ICpProductFilterEditorService
{
    Task<CpProductFilterList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CpProductFilterSetting?> OpenAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP product-filter twin (<c>shop/filter/filter_shop.php</c> + <c>setting.php</c>); writes stay in <see cref="ICpProductFilterWriteService"/>.</summary>
public sealed class CpProductFilterEditorService : ICpProductFilterEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpProductFilterEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpProductFilterList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = new List<CpProductFilterRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, page, pageSize);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_docpart_filter`", cancellationToken).ConfigureAwait(false);
            var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, pageCount);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`manufacturer`,''), IFNULL(`article`,''), IFNULL(`name`,''), IFNULL(`active`,0), IFNULL(`list_storages`,'[]'), IFNULL(`min_price`,0), IFNULL(`max_price`,0), IFNULL(`min_time`,0), IFNULL(`max_time`,0) "
                + "FROM `shop_docpart_filter` ORDER BY `id` DESC LIMIT ? OFFSET ?");
            ErpDb.AddParameters(cmd, pageSize, (page - 1) * pageSize);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpProductFilterRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1).Trim(),
                    reader.GetString(2).Trim(),
                    reader.GetString(3).Trim(),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                    ParseStorageIds(reader.GetString(5)).Count,
                    Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture)));
            }

            return new(rows, total, page, pageSize);
        }
        catch (DbException)
        {
            return new(rows, 0, page, pageSize);
        }
    }

    public async Task<CpProductFilterSetting?> OpenAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            CpProductFilterSetting? setting = null;
            HashSet<long> checkedIds = [];
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`manufacturer`,''), IFNULL(`article`,''), IFNULL(`name`,''), IFNULL(`active`,0), IFNULL(`list_storages`,'[]'), IFNULL(`min_price`,0), IFNULL(`max_price`,0), IFNULL(`min_time`,0), IFNULL(`max_time`,0) "
                    + "FROM `shop_docpart_filter` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(cmd, id);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                checkedIds = ParseStorageIds(reader.GetString(5));
                setting = new CpProductFilterSetting(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1).Trim(),
                    CpProductFilterWriteService.NormalizeArticle(reader.GetString(2)),
                    reader.GetString(3).Trim(),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0,
                    Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture),
                    []);
            }

            var storages = new List<CpProductFilterStorage>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,'') FROM `shop_storages` ORDER BY `id` ASC";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var sid = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    storages.Add(new CpProductFilterStorage(sid, reader.GetString(1), checkedIds.Contains(sid)));
                }
            }

            return setting with { Storages = storages };
        }
        catch (DbException)
        {
            return null;
        }
    }

    public static HashSet<long> ParseStorageIds(string? json)
    {
        var ids = new HashSet<long>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return ids;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n))
                {
                    ids.Add(n);
                }
                else if (e.ValueKind == JsonValueKind.String && long.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
                {
                    ids.Add(s);
                }
            }
        }
        catch (JsonException)
        {
        }

        return ids;
    }
}
