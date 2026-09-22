using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

public sealed record CpSitemapContentNode(long Id, long Parent, int Level, string Value, string Url, bool SystemFlag, bool MainFlag, bool Published);

public sealed record CpSitemapPage(IReadOnlyList<CpSitemapContentNode> Nodes, string OutputDirectory, bool OutputWritable, string DomainPath, string ProductUrlMode)
{
    /// <summary>Webix-shaped nested dump consumed by the PHP tree (<c>content_tree</c>): <c>[{id,value,url,system_flag,main_flag,published_flag,data:[...]}]</c>.</summary>
    public string ToTreeJson()
    {
        var byParent = Nodes.GroupBy(n => n.Parent).ToDictionary(g => g.Key, g => g.ToList());
        JsonArray Build(long parent)
        {
            var arr = new JsonArray();
            if (!byParent.TryGetValue(parent, out var children))
            {
                return arr;
            }

            foreach (var n in children)
            {
                var node = new JsonObject
                {
                    ["id"] = n.Id,
                    ["value"] = n.Value,
                    ["url"] = n.Url,
                    ["system_flag"] = n.SystemFlag ? 1 : 0,
                    ["main_flag"] = n.MainFlag ? 1 : 0,
                    ["published_flag"] = n.Published ? 1 : 0,
                    ["open"] = true,
                };
                var kids = Build(n.Id);
                if (kids.Count > 0)
                {
                    node["data"] = kids;
                }

                arr.Add(node);
            }

            return arr;
        }

        return Build(0).ToJsonString();
    }
}

public interface ICpSitemapEditorService
{
    Task<CpSitemapPage> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>PHP <c>ajax_create_sitemap.php</c>: writes <c>sitemapN.xml</c> chunks plus the <c>sitemap.xml</c> index under the PHP docroot.</summary>
    Task<ErpSimpleWriteResult> CreateAsync(string? urlListJson, CancellationToken cancellationToken = default);
}

public sealed class CpSitemapEditorService : ICpSitemapEditorService
{
    private const int MaxUrlsPerFile = 30000;
    private const long MaxBytesPerFile = 5242880;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly PhpReferenceOptions _reference;

    public CpSitemapEditorService(IErpWriteConnectionFactory connections, IOptions<PhpReferenceOptions> reference)
    {
        _connections = connections;
        _reference = reference.Value;
    }

    private string OutputDirectory => (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();

    private (string DomainPath, string ProductUrlMode) ReadConfig()
    {
        var domain = _reference.TenantPhpBaseUrl.TrimEnd('/') + "/";
        var mode = "alias";
        var root = OutputDirectory;
        if (root.Length == 0)
        {
            return (domain, mode);
        }

        var path = Path.Combine(root, "config.php");
        if (!File.Exists(path))
        {
            return (domain, mode);
        }

        try
        {
            var values = PhpConfigFile.Values(PhpConfigFile.Parse(File.ReadAllText(path)));
            if (values.TryGetValue("domain_path", out var d) && !string.IsNullOrWhiteSpace(d))
            {
                domain = d.Trim();
            }

            if (values.TryGetValue("product_url", out var p) && !string.IsNullOrWhiteSpace(p))
            {
                mode = p.Trim();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return (domain, mode);
    }

    public async Task<CpSitemapPage> LoadAsync(CancellationToken cancellationToken = default)
    {
        var (domain, mode) = ReadConfig();
        var dir = OutputDirectory;
        var writable = dir.Length > 0 && Directory.Exists(dir);
        var nodes = new List<CpSitemapContentNode>();
        if (!_connections.IsConfigured)
        {
            return new(nodes, dir, writable, domain, mode);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = new List<(long Id, long Parent, int Level, string Value, string Url, bool Sys, bool Main, bool Pub)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0), IFNULL(`level`,1), IFNULL(`value`,''), IFNULL(`url`,''), IFNULL(`system_flag`,0), IFNULL(`main_flag`,0), IFNULL(`published_flag`,0) FROM `content` WHERE `is_frontend` = 1 ORDER BY `level`, `order`, `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        reader.GetString(3),
                        reader.GetString(4),
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) != 0));
                }
            }

            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            foreach (var r in raw)
            {
                var caption = await translate(r.Value).ConfigureAwait(false);
                nodes.Add(new CpSitemapContentNode(r.Id, r.Parent, r.Level, caption.Length == 0 ? r.Url : caption, r.Url, r.Sys, r.Main, r.Pub));
            }
        }
        catch (DbException)
        {
            nodes.Clear();
        }

        return new(nodes, dir, writable, domain, mode);
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(string? urlListJson, CancellationToken cancellationToken = default)
    {
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(urlListJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(urlListJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        string? url = item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString(),
                            JsonValueKind.Object when item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String => u.GetString(),
                            _ => null,
                        };
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            urls.Add(url.Trim());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return new ErpSimpleWriteResult(false, "invalid-url-list", "url_list must be a JSON array of {url} objects.", 0, 0);
            }
        }

        if (urls.Count == 0)
        {
            return new ErpSimpleWriteResult(false, "empty", "Select at least one page for the sitemap.", 0, 0);
        }

        var dir = OutputDirectory;
        if (dir.Length == 0 || !Directory.Exists(dir))
        {
            return new ErpSimpleWriteResult(false, "docroot-missing", "PHP docroot is not reachable (EcomAE:PhpReference:PhpDocRoot); sitemap files cannot be written from ASP.NET.", 0, 0);
        }

        if (!_connections.IsConfigured)
        {
            return new ErpSimpleWriteResult(false, "migration", "TenantRegistry DB is not configured.", 0, 0);
        }

        var (domain, mode) = ReadConfig();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            urls.AddRange(await CatalogueUrlsAsync(connection, mode, cancellationToken).ConfigureAwait(false));
        }
        catch (DbException ex)
        {
            return new ErpSimpleWriteResult(false, "db", ex.Message, 0, 0);
        }

        try
        {
            var files = WriteFiles(dir, domain, urls);
            return new ErpSimpleWriteResult(true, "ok", $"Ok · {urls.Count.ToString(CultureInfo.InvariantCulture)} url(s) in {files.ToString(CultureInfo.InvariantCulture)} sitemap file(s)", 0, files + 1);
        }
        catch (IOException ex)
        {
            return new ErpSimpleWriteResult(false, "io", ex.Message, 0, 0);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ErpSimpleWriteResult(false, "io", ex.Message, 0, 0);
        }
    }

    private static async Task<List<string>> CatalogueUrlsAsync(DbConnection connection, string productUrlMode, CancellationToken cancellationToken)
    {
        var categories = new List<(long Id, long Parent, string Url, bool Published)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0), IFNULL(`url`,''), IFNULL(`published_flag`,0) FROM `shop_catalogue_categories` ORDER BY `level`, `order`";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                categories.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.GetString(2),
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0));
            }
        }

        var hidden = new HashSet<long>();
        var result = new List<string>();
        foreach (var c in categories)
        {
            if (!c.Published || hidden.Contains(c.Parent))
            {
                hidden.Add(c.Id);
                continue;
            }

            result.Add(c.Url);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`alias`,'') FROM `shop_catalogue_products` WHERE `category_id` = ? AND IFNULL(`published_flag`,0) <> 0");
            ErpDb.AddParameters(cmd, c.Id);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                var alias = reader.GetString(1);
                result.Add(c.Url + "/" + (string.Equals(productUrlMode, "id", StringComparison.OrdinalIgnoreCase) ? id : alias));
            }
        }

        return result;
    }

    private static int WriteFiles(string dir, string domain, IReadOnlyList<string> urls)
    {
        for (var i = 1; File.Exists(Path.Combine(dir, $"sitemap{i}.xml")); i++)
        {
            File.Delete(Path.Combine(dir, $"sitemap{i}.xml"));
        }

        var fileNo = 0;
        var count = 0;
        StringBuilder? sb = null;
        void Flush()
        {
            if (sb is null)
            {
                return;
            }

            sb.Append("</urlset>");
            File.WriteAllText(Path.Combine(dir, $"sitemap{fileNo}.xml"), sb.ToString(), new UTF8Encoding(false));
            sb = null;
        }

        foreach (var url in urls)
        {
            if (sb is null)
            {
                fileNo++;
                count = 0;
                sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            }

            sb.Append("<url>\n<loc>").Append(Escape(domain + url)).Append("</loc>\n<changefreq>monthly</changefreq>\n<priority>1</priority>\n</url>\n");
            count++;
            if (count >= MaxUrlsPerFile || Encoding.UTF8.GetByteCount(sb.ToString()) > MaxBytesPerFile)
            {
                Flush();
            }
        }

        Flush();

        var index = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        for (var i = fileNo; i > 0; i--)
        {
            index.Append("<sitemap>\n<loc>").Append(Escape(domain + "sitemap" + i.ToString(CultureInfo.InvariantCulture) + ".xml")).Append("</loc>\n</sitemap>\n");
        }

        index.Append("</sitemapindex>");
        File.WriteAllText(Path.Combine(dir, "sitemap.xml"), index.ToString(), new UTF8Encoding(false));
        return fileNo;
    }

    private static string Escape(string s) => s.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
