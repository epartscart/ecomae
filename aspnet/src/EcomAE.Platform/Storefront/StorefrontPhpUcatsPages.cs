using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    private static readonly (string Flag, string Href, string Css, string Caption)[] UcatsCatalogueTiles =
    [
        ("ucats_shiny", "/shop/katalogi-ucats/shiny", "new-cat-block-tires", "4585"),
        ("ucats_disks", "/shop/katalogi-ucats/kolesnye-diski", "new-cat-block-disks", "4586"),
        ("ucats_accessories", "/shop/katalogi-ucats/avtoaksessuary", "new-cat-block-accessories", "4587"),
        ("ucats_to", "/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya", "new-cat-block-to", "4588"),
        ("ucats_oil", "/shop/katalogi-ucats/avtoximiya", "new-cat-block-oil", "4589"),
        ("ucats_akb", "/shop/katalogi-ucats/akkumulyatory", "new-cat-block-akb", "4590"),
        ("ucats_caps", "/shop/katalogi-ucats/kolpaki", "new-cat-block-caps", "4591"),
        ("ucats_bolty", "/shop/katalogi-ucats/kolesnye-gajki-bolty-prostavki", "new-cat-block-bolts", "4592")
    ];

    public static string UcatsCatalogues(IReadOnlyDictionary<string, string> config)
    {
        if (config.Count == 0)
        {
            return UcatsConfigMissing;
        }

        var tiles = new StringBuilder();
        foreach (var tile in UcatsCatalogueTiles)
        {
            if (!config.TryGetValue(tile.Flag, out var value) || value.Length == 0)
            {
                continue;
            }

            tiles.Append("<div class=\"col-sm-6 col-md-4 col-lg-3 new-cat-block\"><a href=\"");
            tiles.Append(tile.Href);
            tiles.Append("\" class=\"ucats-h-1 ");
            tiles.Append(tile.Css);
            tiles.Append("\"><div class=\"new-cat-block-text navbar-inverse\">");
            tiles.Append(tile.Caption);
            tiles.Append("</div></a></div>");
        }

        if (tiles.Length == 0)
        {
            return string.Empty;
        }

        return "<div class=\"row\"><div class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\"><h2 class=\"section-title\">4584</h2><div class=\"row\" style=\"margin-right:-11px; margin-left:-11px; margin-top:-9px; margin-bottom:-10px;\">"
            + tiles
            + "</div></div></div>";
    }

    public static async Task<object> UcatsAuthControlAsync(
        DbConnection connection,
        string pageUrl,
        string clientIp,
        CancellationToken cancellationToken)
    {
        if (pageUrl.IndexOf("ucats", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return new RawHttp(string.Empty, "text/html; charset=utf-8");
        }

        try
        {
            await using (var bots = connection.CreateCommand())
            {
                bots.CommandText = "SELECT `from`, `to` FROM `bot_ips`";
                await using var reader = await bots.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var from = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
                    var to = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
                    if (IpInRange(clientIp, from, to))
                    {
                        return new RawHttp(UcatsForbidden, "text/html; charset=utf-8");
                    }
                }
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new RawHttp(BotAddressesMissing, "text/plain; charset=utf-8");
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long? rowId = null;
        long queries = 0;
        try
        {
            await using var check = connection.CreateCommand();
            check.CommandText = ErpDb.Positional(
                "SELECT `id`, `queries_count` FROM `shop_ucats_auth_control` WHERE `ip` = ? AND `time` > ? - 86400 LIMIT 1");
            ErpDb.AddParameters(check, clientIp, now);
            await using var reader = await check.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rowId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                queries = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new RawHttp(UcatsAccessControlMissing, "text/plain; charset=utf-8");
        }

        if (rowId is long id)
        {
            if (queries > 100)
            {
                return new RawHttp(UcatsForbidden, "text/html; charset=utf-8");
            }

            await using var update = connection.CreateCommand();
            update.CommandText = ErpDb.Positional("UPDATE `shop_ucats_auth_control` SET `queries_count` = `queries_count` + 1 WHERE `id` = ?");
            ErpDb.AddParameters(update, id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new RawHttp(string.Empty, "text/html; charset=utf-8");
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = ErpDb.Positional(
            "INSERT INTO `shop_ucats_auth_control` (`time`, `ip`, `user_id`, `queries_count`) VALUES (?, ?, ?, ?)");
        ErpDb.AddParameters(insert, now, clientIp, 0, 1);
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new RawHttp(UcatsAccessControlMissing, "text/plain; charset=utf-8");
        }

        return new RawHttp(string.Empty, "text/html; charset=utf-8");
    }

    private static bool IpInRange(string userIp, string begin, string end)
        => TryIp2Long(userIp, out var user) && TryIp2Long(begin, out var from) && TryIp2Long(end, out var to) && user >= from && user <= to;

    private static bool TryIp2Long(string ip, out long value)
    {
        value = 0;
        if (!IPAddress.TryParse(ip, out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        value = ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
        return true;
    }
}
