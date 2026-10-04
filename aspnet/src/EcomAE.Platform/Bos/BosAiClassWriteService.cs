using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>ai_classification</c> <c>review</c> / <c>epc_ai_review</c>
/// and <c>seed_hs</c> / <c>epc_ai_seed_hs_codes</c>.
/// Classify-and-store, batch, and schema-ensure stay Classic.
/// Dedicated <c>/bos/ajax/seed-hs</c> stays refuse-confirm.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// PHP review always returns <c>{ok:true}</c> — no invented not-found when <c>classification_id</c> is 0.
/// </summary>
public interface IBosAiClassWriteService
{
    Task<ErpSimpleWriteResult> ReviewAsync(
        long classificationId,
        string? category,
        string? subcategory,
        string? hsCode,
        long reviewerId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SeedHsAsync(CancellationToken cancellationToken = default);
}

public sealed class BosAiClassWriteService : IBosAiClassWriteService
{
    /// <summary>PHP <c>epc_ai_seed_hs_codes</c> hard-coded INSERT IGNORE rows.</summary>
    public static readonly (string Code, string Description, string Chapter, string Section, decimal DutyRate, string Keywords)[] SeedCodes =
    [
        ("8409", "Parts for spark-ignition engines", "84", "Machinery", 5.00m, "engine piston crankshaft camshaft valve cylinder"),
        ("8421", "Filtering or purifying machinery", "84", "Machinery", 5.00m, "filter oil air fuel cabin"),
        ("8511", "Electrical ignition equipment", "85", "Electrical", 5.00m, "alternator starter ignition spark plug"),
        ("8708", "Parts for motor vehicles", "87", "Vehicles", 5.00m, "brake suspension body bumper fender"),
        ("8471", "Automatic data processing machines", "84", "Machinery", 5.00m, "computer laptop tablet monitor"),
        ("6109", "T-shirts, singlets and vests", "61", "Textiles", 5.00m, "shirt t-shirt vest singlet"),
        ("6403", "Footwear with outer soles of rubber", "64", "Footwear", 5.00m, "shoe boot sneaker sandal"),
        ("7113", "Articles of jewellery", "71", "Precious metals", 5.00m, "gold silver ring necklace bracelet earring"),
        ("9101", "Wrist-watches", "91", "Clocks/watches", 5.00m, "watch wristwatch chronograph timepiece"),
        ("8544", "Insulated wire and cable", "85", "Electrical", 5.00m, "wire cable wiring harness connector"),
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public BosAiClassWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(int)</c> on a leading optional sign + digits token.</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    public async Task<ErpSimpleWriteResult> ReviewAsync(
        long classificationId,
        string? category,
        string? subcategory,
        string? hsCode,
        long reviewerId,
        CancellationToken cancellationToken = default)
    {
        var cat = category ?? string.Empty;
        var sub = subcategory ?? string.Empty;
        var hs = hsCode ?? string.Empty;

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_ai_classifications`
                    SET `category` = ?, `subcategory` = ?, `hs_code` = ?, `reviewed` = 1, `reviewed_by` = ?, `method` = 'manual'
                    WHERE `id` = ?
                    """),
                cancellationToken, cat, sub, hs, reviewerId, classificationId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("AI classification reviewed", classificationId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "AI classifications table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SeedHsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var inserted = 0;
            foreach (var code in SeedCodes)
            {
                inserted += await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        INSERT IGNORE INTO `epc_hs_codes` (`code`, `description`, `chapter`, `section`, `duty_rate`, `keywords`)
                        VALUES (?, ?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    code.Code, code.Description, code.Chapter, code.Section, code.DutyRate, code.Keywords)
                    .ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok("HS codes seeded", inserted);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "HS codes table is missing — schema-ensure stays Classic.");
        }
    }
}
