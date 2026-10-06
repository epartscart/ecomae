using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CrossSearchStaysClassic = "Cross search stays on the classic lookup.";
    public const string CrossCatalogStaysClassic = "Full catalog import stays on the classic lookup.";
    public const string CrossRepairStaysClassic = "Empty-brand repair stays on the classic cross helper.";

    public static async Task<object> CrossCpAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        ICpCrossWriteService crosses,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Access denied"),
            (_, token) => CrossBodyAsync(connection, fields, crosses, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> CrossBodyAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> fields,
        ICpCrossWriteService crosses,
        CancellationToken cancellationToken)
    {
        var request = CrossRequest(fields);
        var action = JsonText(request["action"]);
        var article = CrossDecode(JsonText(request["article"]));
        var brand = CrossDecode(JsonText(request["manufacturer"]));
        if (action is "lookup_crosses" or "verify_crosses" or "sync_from_crossbase")
        {
            return new FlagBody(false, CrossSearchStaysClassic);
        }

        if (action == "import_full_catalog")
        {
            return new FlagBody(false, CrossCatalogStaysClassic);
        }

        if (action == "repair_empty_brands")
        {
            return new FlagBody(false, CrossRepairStaysClassic);
        }

        if (action == "add_cross_link")
        {
            return await CrossAddOneAsync(
                connection,
                crosses,
                article,
                brand,
                CrossDecode(JsonText(request["ref_article"])),
                CrossDecode(JsonText(request["ref_brand"])),
                cancellationToken).ConfigureAwait(false);
        }

        if (action == "add_cross_bulk")
        {
            return await CrossAddBulkAsync(connection, crosses, article, brand, request, cancellationToken).ConfigureAwait(false);
        }

        return new FlagBody(false, "Unknown action");
    }

    private static async Task<object> CrossAddOneAsync(
        DbConnection connection,
        ICpCrossWriteService crosses,
        string article,
        string brand,
        string referenceArticle,
        string referenceBrand,
        CancellationToken cancellationToken)
    {
        ErpSimpleWriteResult written;
        try
        {
            written = await crosses.AddAsync(article, brand, referenceArticle, referenceBrand, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CrossLinksMissing);
        }

        if (!written.Succeeded && written.Code == "db")
        {
            return new FlagBody(false, written.Message);
        }

        var (inserted, already, skipped, reason) = MapCross(written);
        return await CrossWritePayloadAsync(connection, article, inserted, already, skipped, reason, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> CrossAddBulkAsync(
        DbConnection connection,
        ICpCrossWriteService crosses,
        string article,
        string brand,
        JsonObject request,
        CancellationToken cancellationToken)
    {
        var inserted = 0;
        var already = 0;
        var skipped = 0;
        var filter = JsonText(request["source_filter"]);
        if (request["references"] is JsonArray references)
        {
            foreach (var reference in references)
            {
                if (reference is not JsonObject row)
                {
                    skipped++;
                    continue;
                }

                if (filter.Length > 0)
                {
                    var source = JsonText(row["source"]);
                    if (!string.Equals(source, filter, StringComparison.Ordinal) && !source.Contains(filter, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                ErpSimpleWriteResult written;
                try
                {
                    written = await crosses.AddAsync(article, brand, JsonText(row["article"]), JsonText(row["brand"]), cancellationToken).ConfigureAwait(false);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, CrossLinksMissing);
                }

                if (!written.Succeeded && written.Code == "db")
                {
                    return new FlagBody(false, written.Message);
                }

                var mapped = MapCross(written);
                inserted += mapped.Inserted;
                already += mapped.Already;
                skipped += mapped.Skipped;
            }
        }

        return await CrossWritePayloadAsync(connection, article, inserted, already, skipped, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonObject> CrossWritePayloadAsync(
        DbConnection connection,
        string article,
        int inserted,
        int already,
        int skipped,
        string reason,
        CancellationToken cancellationToken)
        => new()
        {
            ["status"] = true,
            ["inserted"] = inserted,
            ["already"] = already,
            ["skipped"] = skipped,
            ["reason"] = reason,
            ["cp_links_for_article"] = await CrossLinkCountAsync(connection, article, cancellationToken).ConfigureAwait(false),
        };

    private static async Task<int> CrossLinkCountAsync(DbConnection connection, string article, CancellationToken cancellationToken)
    {
        var clean = CpCrossWriteService.CleanArticle(article);
        if (clean.Length == 0)
        {
            return 0;
        }

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?"),
                cancellationToken,
                clean,
                clean).ConfigureAwait(false);
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static (int Inserted, int Already, int Skipped, string Reason) MapCross(ErpSimpleWriteResult written)
    {
        if (written.Succeeded)
        {
            return (1, 0, 0, string.Empty);
        }

        if (written.Code == "already")
        {
            return (0, 1, 0, "already_linked");
        }

        if (written.Message.Contains("itself", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0, 1, "same_part_same_brand");
        }

        if (written.Message.Contains("brand", StringComparison.OrdinalIgnoreCase))
        {
            return (0, 0, 1, "missing_brand");
        }

        return (0, 0, 1, written.Code == "invalid" ? "invalid_article" : "insert_failed");
    }

    private static JsonObject CrossRequest(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("request_object", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(raw) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string CrossDecode(string raw)
    {
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(raw.Replace("+", " ", StringComparison.Ordinal));
        }
        catch (UriFormatException)
        {
            return raw;
        }
    }
}
