using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_compliance_add_obligation</c> / ajax
/// <c>bos_compliance_add_obligation</c> twin. UPSERT
/// <c>epc_bos_compliance_obligations</c> on unique <c>code</c>. Does not CREATE
/// tables. Disable is already ASP.NET-live. File, retention, seed, and schema
/// ensure stay PHP.
/// </summary>
public interface IErpBosComplianceAddObligationWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpBosComplianceAddObligationWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosComplianceAddObligationWriteRequest(
    string? Title = null,
    string? Code = null,
    string? Regime = null,
    string? Authority = null,
    string? Frequency = null,
    int LeadDays = 28,
    string? DocRequirements = null,
    long AdminId = 0);

public sealed class ErpBosComplianceAddObligationWriteService : IErpBosComplianceAddObligationWriteService
{
    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.CultureInvariant);

    internal static readonly HashSet<string> Frequencies = new(StringComparer.Ordinal)
    {
        "monthly", "quarterly", "annual", "one_off",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosComplianceAddObligationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpBosComplianceAddObligationWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = Clip((request.Title ?? string.Empty).Trim(), 160);
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = Clip((request.Code ?? string.Empty).Trim(), 48);
        if (code.Length == 0)
        {
            code = DefaultCode(title, now);
        }

        var freq = (request.Frequency ?? string.Empty).Trim();
        if (!Frequencies.Contains(freq))
        {
            freq = "monthly";
        }

        var regime = Clip((request.Regime ?? string.Empty).Trim(), 64);
        if (regime.Length == 0)
        {
            regime = "general";
        }

        var authority = Clip((request.Authority ?? string.Empty).Trim(), 120);
        var leadDays = request.LeadDays < 0 ? 0 : request.LeadDays;
        var docs = (request.DocRequirements ?? string.Empty).Trim();
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_compliance_obligations", "title", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_compliance_obligations", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Compliance obligation table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_compliance_obligations` (`code`,`title`,`regime`,`authority`,`frequency`,`lead_days`,`doc_requirements`,`is_seed`,`active`,`admin_id`,`time`) VALUES (?,?,?,?,?,?,?,0,1,?,?) ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `regime` = VALUES(`regime`), `authority` = VALUES(`authority`), `frequency` = VALUES(`frequency`), `lead_days` = VALUES(`lead_days`), `doc_requirements` = VALUES(`doc_requirements`), `active` = 1"),
            cancellationToken,
            code, title, regime, authority, freq, leadDays, docs, adminId, now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (inserted <= 0)
        {
            inserted = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_bos_compliance_obligations` WHERE `code`=? LIMIT 1"),
                cancellationToken,
                code).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Obligation saved", inserted);
    }

    public static string DefaultCode(string title, long now)
    {
        var slug = NonSlug.Replace(title.ToLowerInvariant(), "_").Trim('_');
        if (slug.Length > 36)
        {
            slug = slug[..36];
        }

        var suffix = now.ToString(CultureInfo.InvariantCulture);
        if (suffix.Length > 4)
        {
            suffix = suffix[^4..];
        }

        return Clip("obl_" + slug + "_" + suffix, 48);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
