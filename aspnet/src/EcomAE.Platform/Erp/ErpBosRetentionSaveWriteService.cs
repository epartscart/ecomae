using System.Data.Common;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_retention_save</c> / ajax <c>bos_compliance_save_retention</c>
/// twin. UPSERT <c>epc_bos_retention_rules</c> on unique <c>doc_type</c>. Does
/// not CREATE tables. Disable is already ASP.NET-live. Add, file, seed, and
/// schema ensure stay PHP.
/// </summary>
public interface IErpBosRetentionSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpBosRetentionSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosRetentionSaveWriteRequest(
    string? Label = null,
    string? DocType = null,
    int RetentionYears = 5,
    string? Basis = null,
    string? LegalRef = null,
    long AdminId = 0);

public sealed class ErpBosRetentionSaveWriteService : IErpBosRetentionSaveWriteService
{
    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.CultureInvariant);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosRetentionSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpBosRetentionSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var label = Clip((request.Label ?? string.Empty).Trim(), 160);
        if (label.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Label required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var docType = Clip((request.DocType ?? string.Empty).Trim(), 64);
        if (docType.Length == 0)
        {
            docType = DefaultDocType(label);
        }

        var years = request.RetentionYears < 0 ? 0 : request.RetentionYears;
        var basis = Clip((request.Basis ?? string.Empty).Trim(), 160);
        var legalRef = Clip((request.LegalRef ?? string.Empty).Trim(), 160);
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_retention_rules", "label", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_retention_rules", "doc_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Retention rule table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_retention_rules` (`doc_type`,`label`,`retention_years`,`basis`,`legal_ref`,`is_seed`,`active`,`admin_id`,`time`) VALUES (?,?,?,?,?,0,1,?,?) ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `retention_years` = VALUES(`retention_years`), `basis` = VALUES(`basis`), `legal_ref` = VALUES(`legal_ref`), `active` = 1"),
            cancellationToken,
            docType, label, years, basis, legalRef, adminId, now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (inserted <= 0)
        {
            inserted = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_bos_retention_rules` WHERE `doc_type`=? LIMIT 1"),
                cancellationToken,
                docType).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Retention rule saved", inserted);
    }

    public static string DefaultDocType(string label)
    {
        var slug = NonSlug.Replace(label.ToLowerInvariant(), "_").Trim('_');
        return Clip(slug, 60);
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
