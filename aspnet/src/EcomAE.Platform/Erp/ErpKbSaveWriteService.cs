using System.Data.Common;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_kb_save</c> / ajax <c>kb_save</c> twin.
/// INSERT <c>epc_erp_kb_articles</c>. Seed defaults and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpKbSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpKbSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpKbSaveWriteRequest(
    string? Title = null,
    string? Category = null,
    string? Summary = null,
    string? BodyHtml = null,
    long AdminId = 0);

public sealed class ErpKbSaveWriteService : IErpKbSaveWriteService
{
    private static readonly Regex SlugNoise = new("[^a-z0-9-]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpKbSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpKbSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = (request.Title ?? string.Empty).Trim();
        var invalid = Validate(title);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var slug = SlugFromTitle(title);
        title = Clip(title, 255);
        slug = Clip(slug, 128);
        var category = Clip((request.Category ?? "general").Trim(), 64);
        var summary = request.Summary ?? string.Empty;
        summary = summary.Trim();
        var body = request.BodyHtml ?? string.Empty;
        body = body.Trim();
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_kb_articles", "title", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_kb_articles", "slug", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Knowledge article table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_kb_articles` (`slug`, `title`, `category`, `summary`, `body_html`, `admin_id`, `time_created`, `time_updated`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            slug,
            title,
            category,
            summary,
            body,
            adminId,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Knowledge article published", id);
    }

    public static string? Validate(string title)
        => title.Length == 0 ? "Article title required" : null;

    /// <summary>PHP <c>preg_replace('/[^a-z0-9-]+/', '-', strtolower($title))</c>. Does not trim hyphens.</summary>
    public static string SlugFromTitle(string title)
        => SlugNoise.Replace(title.ToLowerInvariant(), "-");

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
