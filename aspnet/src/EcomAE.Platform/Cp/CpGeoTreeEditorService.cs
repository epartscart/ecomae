using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpGeoTreeFlatNode(long Id, long Parent, int Level, int Order, string ValueKey, string Value);

public sealed record CpGeoTreePage(IReadOnlyList<CpGeoTreeFlatNode> Nodes, long NextId)
{
    /// <summary>Webix-shaped nested dump consumed by the PHP tree and by <see cref="CpGeoTreeWriteService"/>: <c>[{id,value,value_lang_str_id,from_server,data:[...]}]</c>.</summary>
    public string ToTreeJson()
    {
        var children = new Dictionary<long, List<CpGeoTreeFlatNode>>();
        foreach (var n in Nodes)
        {
            if (!children.TryGetValue(n.Parent, out var list))
            {
                children[n.Parent] = list = [];
            }

            list.Add(n);
        }

        JsonArray Build(long parent)
        {
            var arr = new JsonArray();
            if (!children.TryGetValue(parent, out var list))
            {
                return arr;
            }

            foreach (var n in list.OrderBy(x => x.Order).ThenBy(x => x.Id))
            {
                var langId = n.ValueKey.Length > 0 && n.ValueKey.All(char.IsDigit) ? n.ValueKey : "0";
                arr.Add(new JsonObject
                {
                    ["id"] = n.Id,
                    ["value"] = n.Value,
                    ["value_lang_str_id"] = langId,
                    ["from_server"] = 1,
                    ["data"] = Build(n.Id)
                });
            }

            return arr;
        }

        return Build(0).ToJsonString();
    }
}

public interface ICpGeoTreeEditorService
{
    Task<CpGeoTreePage> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP geo tree twin (<c>geo_tree.php</c> / <c>get_geo_tree.php</c>); writes stay in <see cref="ICpGeoTreeWriteService"/>.</summary>
public sealed class CpGeoTreeEditorService : ICpGeoTreeEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpGeoTreeEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpGeoTreePage> LoadAsync(CancellationToken cancellationToken = default)
    {
        var nodes = new List<CpGeoTreeFlatNode>();
        long nextId = 1;
        if (!_connections.IsConfigured)
        {
            return new(nodes, nextId);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = new List<(long Id, long Parent, int Level, int Order, string Key)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0), IFNULL(`level`,1), IFNULL(`order`,0), IFNULL(`value`,'') FROM `shop_geo` ORDER BY `level`, `order`, `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                        reader.GetString(4)));
                }
            }

            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            foreach (var r in raw)
            {
                nodes.Add(new CpGeoTreeFlatNode(r.Id, r.Parent, r.Level, r.Order, r.Key, await translate(r.Key).ConfigureAwait(false)));
            }

            nextId = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(MAX(`id`),0) + 1 FROM `shop_geo`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        return new(nodes, Math.Max(1, nextId));
    }
}
