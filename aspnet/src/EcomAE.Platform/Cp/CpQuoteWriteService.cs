using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>quote_requests.php</c> twins for admin_note, line quoting, and send_quote.
/// </summary>
public interface ICpQuoteWriteService
{
    Task<ErpSimpleWriteResult> SaveAdminNoteAsync(long quoteId, string? adminNote, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveLinesAsync(
        long quoteId,
        string? adminNote,
        string? linesJson,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendQuoteAsync(long quoteId, CancellationToken cancellationToken = default);
}

public sealed class CpQuoteWriteService : ICpQuoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpQuoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAdminNoteAsync(
        long quoteId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A quote id is required.");
        }

        var note = (adminNote ?? string.Empty).Trim();
        if (note.Length > 4000)
        {
            note = note[..4000];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `admin_note` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            note, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), quoteId);
        return ErpSimpleWriteResult.Ok("Saved", quoteId);
    }

    public async Task<ErpSimpleWriteResult> SaveLinesAsync(
        long quoteId,
        string? adminNote,
        string? linesJson,
        CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A quote id is required.");
        }

        var parsed = ParseLines(linesJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one quote line is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var note = (adminNote ?? string.Empty).Trim();
        if (note.Length > 4000)
        {
            note = note[..4000];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var ownerId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            quoteId);
        if (ownerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quotes are only for registered customers");
        }

        var writes = 0;
        foreach (var line in parsed.Lines)
        {
            writes += await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `shop_quote_items` SET
                        `quoted_price` = ?,
                        `quoted_time_to_exe` = ?,
                        `line_admin_note` = ?,
                        `offer_alternative` = ?,
                        `alt_manufacturer` = ?,
                        `alt_article` = ?,
                        `alt_article_show` = ?,
                        `alt_name` = ?,
                        `alt_count_need` = ?,
                        `alt_quoted_price` = ?,
                        `alt_storage_id` = ?
                    WHERE `id` = ? AND `quote_id` = ?
                    """),
                cancellationToken,
                line.QuotedPrice,
                line.QuotedTimeToExe,
                line.LineAdminNote,
                line.OfferAlternative,
                line.AltManufacturer,
                line.AltArticle,
                line.AltArticleShow,
                line.AltName,
                line.AltCountNeed,
                line.AltQuotedPrice,
                line.AltStorageId,
                line.Id,
                quoteId);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `admin_note` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            note, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), quoteId);
        writes++;
        return new ErpSimpleWriteResult(true, "ok", "Saved", quoteId, writes);
    }

    public static (IReadOnlyList<QuoteLineWrite> Lines, string? Error) ParseLines(string? linesJson)
    {
        var raw = (linesJson ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return ([], null);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "Quote lines must be a JSON array.");
            }

            var lines = new List<QuoteLineWrite>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var built = TryBuildLine(item);
                if (built.Error is not null)
                {
                    return ([], built.Error);
                }

                if (built.Line is not null)
                {
                    lines.Add(built.Line);
                }
            }

            return (lines, null);
        }
        catch (JsonException)
        {
            return ([], "Quote lines must be valid JSON.");
        }
    }

    private static (QuoteLineWrite? Line, string? Error) TryBuildLine(JsonElement item)
    {
        var id = ReadLong(item, "id", "lineId", "line_id");
        if (id <= 0)
        {
            return (null, "Each quote line needs an id.");
        }

        var note = ReadString(item, "lineAdminNote", "line_admin_note") ?? string.Empty;
        if (note.Length > 4000)
        {
            note = note[..4000];
        }

        var offerAlt = ReadFlag(item, "offerAlternative", "offer_alternative");
        decimal? quotedPrice = ReadDecimal(item, "quotedPrice", "quoted_price");
        int? quotedTime = ReadInt(item, "quotedTimeToExe", "quoted_time_to_exe");
        string? altMfr = null;
        string? altArt = null;
        string? altShow = null;
        string? altName = null;
        int? altQty = null;
        decimal? altPrice = null;
        long? altStorage = null;

        if (offerAlt)
        {
            altMfr = CleanBrand(ReadString(item, "altManufacturer", "alt_manufacturer"));
            var altArtRaw = ReadString(item, "altArticle", "alt_article");
            if (string.IsNullOrEmpty(altMfr) || string.IsNullOrWhiteSpace(altArtRaw))
            {
                return (null, "Alternative offer on line #" + id.ToString(CultureInfo.InvariantCulture) + " needs brand and article");
            }

            altArt = CleanArticle(altArtRaw);
            altShow = altArt;
            var name = (ReadString(item, "altName", "alt_name") ?? string.Empty).Trim();
            altName = name.Length > 0 ? name : altMfr + " " + altShow + " (alternative)";
            altQty = Math.Max(1, ReadInt(item, "altCountNeed", "alt_count_need") ?? 1);
            altPrice = ReadDecimal(item, "altQuotedPrice", "alt_quoted_price");
            if (altPrice is null or <= 0)
            {
                return (null, "Alternative offer on line #" + id.ToString(CultureInfo.InvariantCulture) + " needs a positive price");
            }

            altStorage = ReadLong(item, "altStorageId", "alt_storage_id");
            if (altStorage <= 0)
            {
                return (null, "Alternative offer on line #" + id.ToString(CultureInfo.InvariantCulture) + " needs a supplier warehouse");
            }

            quotedPrice = altPrice;
        }

        return (new QuoteLineWrite(
            id,
            quotedPrice,
            quotedTime,
            note,
            offerAlt ? 1 : 0,
            altMfr,
            altArt,
            altShow,
            altName,
            altQty,
            altPrice,
            altStorage), null);
    }

    internal static string CleanArticle(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        var builder = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().ToUpperInvariant();
    }

    internal static string CleanBrand(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static long ReadLong(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static int? ReadInt(JsonElement item, params string[] names)
    {
        var value = ReadLong(item, names);
        return value == 0 && !HasProperty(item, names) ? null : (int)value;
    }

    private static decimal? ReadDecimal(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                var raw = (prop.GetString() ?? string.Empty).Trim().Replace(',', '.');
                if (raw.Length > 0 && decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static bool ReadFlag(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind is JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind is JsonValueKind.False)
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
            {
                return n == 1;
            }

            var raw = prop.ValueKind == JsonValueKind.String ? (prop.GetString() ?? string.Empty).Trim() : string.Empty;
            if (raw is "1" or "true" or "True" or "on" or "yes")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasProperty(JsonElement item, params string[] names)
        => names.Any(name => item.TryGetProperty(name, out _));

    public sealed record QuoteLineWrite(
        long Id,
        decimal? QuotedPrice,
        int? QuotedTimeToExe,
        string LineAdminNote,
        int OfferAlternative,
        string? AltManufacturer,
        string? AltArticle,
        string? AltArticleShow,
        string? AltName,
        int? AltCountNeed,
        decimal? AltQuotedPrice,
        long? AltStorageId);

    public async Task<ErpSimpleWriteResult> SendQuoteAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A quote id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var ownerId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            quoteId);
        if (ownerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quotes are only for registered customers");
        }

        var lineCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?"),
            cancellationToken,
            quoteId);
        if (lineCount < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one quote line before publishing");
        }

        var incomplete = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ? AND (
                    (`offer_alternative` = 1 AND (`alt_quoted_price` IS NULL OR `alt_quoted_price` <= 0 OR `alt_manufacturer` IS NULL OR `alt_manufacturer` = '' OR `alt_article` IS NULL OR `alt_article` = '' OR `alt_storage_id` IS NULL OR `alt_storage_id` <= 0))
                    OR
                    (`offer_alternative` = 0 AND (`quoted_price` IS NULL OR `quoted_price` <= 0))
                )
                """),
            cancellationToken,
            quoteId);
        if (incomplete > 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Set a positive price on every line (or complete each alternative: part, warehouse, qty, price)");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'quoted', `time_updated` = ? WHERE `id` = ? AND `status` IN ('submitted','quoted')"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), quoteId);
        return ErpSimpleWriteResult.Ok("Quote sent to customer", quoteId);
    }
}
